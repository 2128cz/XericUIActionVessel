using UnityEngine;
using XericLibrary.Runtime.UIGraph;
using PrimitiveType = XericLibrary.Runtime.UIGraph.PrimitiveType;
using XericUI.Core;
using XericUI.XChart.Mesh;
using XericUI.XChart.Space;

namespace XericUI.XChart.Pseudo3D
{
    /// <summary>
    /// 散点元素：把 3D 数据点画进图的空间里。
    ///
    /// <para><b>数据从哪儿来</b>：只认图表给的中转接口 <see cref="IXChartDataSource"/>
    /// （<see cref="AutoFromData"/> 为真时按维度下标逐条取，范围用聚合量的 min/max）。
    /// 元素不直接摸记录数组，所以以后要换桶/环形缓冲也不用改这里。
    /// <see cref="AutoFromData"/> 为假时用脚本喂进来的 <see cref="SetCubePoints"/>（立方体单位）。</para>
    ///
    /// <para><b>投影从哪儿来</b>：图里的 <see cref="ChartToolSpace"/>，和背景/线框/轴线<b>同一个</b>。
    /// 所以"点落在盒子里"是结构保证——数据归一化到 [0,1] 再映到立方体局部 [-1,1]，
    /// 然后走和背景一样的 <c>基变换 ∘ 工具变换</c>。</para>
    ///
    /// <para><b>什么时候重算</b>：数据版本号或空间版本号变了才重投影，其余帧什么都不做。</para>
    ///
    /// 子节点结构：
    ///   [ScatterPoints]
    ///     └─ PointRenderer (XScatterPointRenderer)
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.Chart + "/Pseudo3D/Scatter Points Tool")]
    public class ChartToolScatterPoints : XChartToolBase
    {
        // ── 数据装配 ──────────────────────────────────────────────

        /// <summary>真=从图的数据源按维度下标读；假=用 <see cref="SetCubePoints"/> 喂的点</summary>
        public bool AutoFromData { get; set; } = true;

        /// <summary>三个维度在数据源里的下标（绑定：点云画哪三路数据；范围归 chart 的维度行）</summary>
        public int DimensionX { get; set; }
        public int DimensionY { get; set; } = 1;
        public int DimensionZ { get; set; } = 2;

        /// <summary>点云自己的变换（立方体单位），用来把点整体缩进盒子里或挪一角</summary>
        public XChartToolTransform Transform { get; set; } = XChartToolTransform.Identity;

        /// <summary>
        /// 桶采样目标点数：<c>0</c> 或 ≥ 记录数＝不聚合；否则把记录按桶归约成这么多个点。
        /// 目的是"几万条记录也只画 N 个点"，同时让每个点代表它那一段时间。
        /// </summary>
        public int BucketTargetPoints { get; set; }

        /// <summary>桶内取哪个统计量当这个点（抽稀 First/Last 不造新值；趋势 Mean/Median；包络 Minimum/Maximum）</summary>
        public XChartAggregateKind BucketReduce { get; set; } = XChartAggregateKind.Last;

        // ── 外观 ──────────────────────────────────────────────────

        public float PointSize { get => m_PointSize; set { m_PointSize = Mathf.Max(0.5f, value); if (IsInitialized) SyncShapeParams(); } }
        private float m_PointSize = 8f;

        public PrimitiveType PointType { get => m_PointType; set { m_PointType = value; if (IsInitialized) SyncShapeParams(); } }
        private PrimitiveType m_PointType = PrimitiveType.Polygon;

        public SizeMode PointSizeMode { get => m_PointSizeMode; set { m_PointSizeMode = value; if (IsInitialized) SyncShapeParams(); } }
        private SizeMode m_PointSizeMode = SizeMode.InscribedCircle;

        public Color PointColor { get => m_PointColor; set { m_PointColor = value; if (IsInitialized) SyncShapeParams(); } }
        private Color m_PointColor = new Color(0.33f, 0.67f, 1f, 1f);

        public Color CenterColor { get => m_CenterColor; set { m_CenterColor = value; if (IsInitialized) SyncShapeParams(); } }
        private Color m_CenterColor = Color.white;

        public Color BorderColor { get => m_BorderColor; set { m_BorderColor = value; if (IsInitialized) SyncShapeParams(); } }
        private Color m_BorderColor = new Color(0.2f, 0.2f, 0.2f, 1f);

        public float BorderThickness { get => m_BorderThickness; set { m_BorderThickness = Mathf.Max(0f, value); if (IsInitialized) SyncShapeParams(); } }
        private float m_BorderThickness = 1f;

        public int SideCount { get => m_SideCount; set { m_SideCount = Mathf.Max(3, value); if (IsInitialized) SyncShapeParams(); } }
        private int m_SideCount = 8;

        public float ChamferSize { get => m_ChamferSize; set { m_ChamferSize = Mathf.Clamp01(value); if (IsInitialized) SyncShapeParams(); } }
        private float m_ChamferSize;

        public float PointAngle { get => m_PointAngle; set { m_PointAngle = value; if (IsInitialized) SyncShapeParams(); } }
        private float m_PointAngle;

        /// <summary>按某一维的数值做颜色渐变</summary>
        public bool EnableColorMapping { get; set; }

        public Color ColorMapMin { get; set; } = Color.blue;
        public Color ColorMapMax { get; set; } = Color.red;

        /// <summary>拿哪一维的数值做映射</summary>
        public int ColorMapDimension { get; set; } = 2;

        // ── 内部 ──────────────────────────────────────────────────

        public XScatterPointRenderer PointRenderer { get; private set; }

        private Matrix4x4 m_Composed = Matrix4x4.identity;

        private Vector3[] m_CubePoints = new Vector3[0];
        /// <summary>中位数归约用的暂存：长度按记录数上限，渲染路径里不分配</summary>
        private double[] m_BucketScratch;
        /// <summary>本帧桶数（=-1 表示还没算过）；与源记录数一起进报表——"少画了多少"要能看到</summary>
        private int m_BucketCount = -1;
        private int m_SourceRecords;
        private Vector2[] m_Projected = new Vector2[0];
        private Color[] m_PointColors;
        private float[] m_ColorValues;

        private bool m_PointsDirty = true;
        private long m_LastDataVersion = -1;
        private int m_LastSpaceVersion = -1;

        protected override void OnInitialize()
        {
            PointRenderer = CreateChildComponent<XScatterPointRenderer>("PointRenderer");
            if (PointRenderer != null)
            {
                var rt = PointRenderer.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            SyncShapeParams();
        }

        private void SyncShapeParams()
        {
            if (PointRenderer == null) return;
            PointRenderer.pointType = m_PointType;
            PointRenderer.sizeMode = m_PointSizeMode;
            PointRenderer.pointSize = m_PointSize;
            PointRenderer.fillColor = m_PointColor;
            PointRenderer.centerColor = m_CenterColor;
            PointRenderer.borderColor = m_BorderColor;
            PointRenderer.borderThickness = m_BorderThickness;
            PointRenderer.sideCount = m_SideCount;
            PointRenderer.chamferSize = m_ChamferSize;
            PointRenderer.chamferSegments = 4;
            PointRenderer.angle = m_PointAngle;

            if (m_CubePoints.Length > 0) PointRenderer.UpdateShapeParams();
        }

        /// <summary>脚本喂点（立方体单位，[-1,1] 就是盒子内部）。配合 <see cref="AutoFromData"/>=false 用</summary>
        public void SetCubePoints(Vector3[] cubePoints)
        {
            m_CubePoints = cubePoints ?? new Vector3[0];
            m_Projected = new Vector2[m_CubePoints.Length];
            m_PointColors = null;
            m_ColorValues = null;
            m_BucketCount = 0;                       // 脚本喂点这条路没有桶
            m_SourceRecords = m_CubePoints.Length;
            m_PointsDirty = true;
        }

        /// <summary>数据或变换变了（版本号比对才是真判据，这里只负责"再算一次"）</summary>
        public void MarkDataDirty() => m_PointsDirty = true;

        // ── 渲染 ──────────────────────────────────────────────────

        public override void OnRender()
        {
            base.OnRender();
            if (PointRenderer == null) return;

            var space = Context?.Space;
            if (space == null) return;

            bool spaceChanged = space.ProjectionVersion != m_LastSpaceVersion;
            bool dataChanged = Data != null && Data.Version != m_LastDataVersion;
            if (!m_PointsDirty && !spaceChanged && !dataChanged) return;

            if (spaceChanged || dataChanged) m_PointsDirty = true;
            m_LastSpaceVersion = space.ProjectionVersion;
            m_LastDataVersion = Data != null ? Data.Version : -1;
            m_Composed = space.Compose(Transform);

            if (m_PointsDirty)
            {
                if (AutoFromData) BuildPointsFromDataSource(space);
                RebuildProjection(space);
                m_PointsDirty = false;
            }

            if (m_Projected.Length == 0)
            {
                PointRenderer.ClearAll();
                return;
            }

            if (EnableColorMapping && m_PointColors != null) PointRenderer.SetPoints(m_Projected, m_PointColors);
            else PointRenderer.SetPoints(m_Projected);
        }

        /// <summary>立方体单位点 → 图表局部像素</summary>
        private void RebuildProjection(ChartToolSpace space)
        {
            if (m_Projected == null || m_Projected.Length != m_CubePoints.Length)
                m_Projected = new Vector2[m_CubePoints.Length];

            for (int i = 0; i < m_CubePoints.Length; i++)
                m_Projected[i] = space.Project(m_CubePoints[i], m_Composed);

            if (EnableColorMapping) ApplyColorMapping();
        }

        /// <summary>
        /// 从数据源逐条取值，按每维范围归一化到 [0,1]，再映到立方体局部 [-1,1]。
        /// 范围优先用配置里给的手动范围，没给就用该维的聚合 min/max。
        /// </summary>
        private void BuildPointsFromDataSource(ChartToolSpace space)
        {
            var source = Data;
            if (source == null || !source.HasData)
            {
                ClearPoints();
                return;
            }

            int count = source.RecordCount;

            // 桶采样：0 或 ≥ 记录数＝不聚合（桶数就是记录数，行为与以前完全一致）
            int buckets = XChartBucketMath.BucketCount(count, BucketTargetPoints);
            m_BucketCount = buckets;
            m_SourceRecords = count;

            if (m_CubePoints.Length < buckets)
            {
                m_CubePoints = new Vector3[buckets];
                m_Projected = new Vector2[buckets];
            }
            if (EnableColorMapping && (m_PointColors == null || m_PointColors.Length < buckets))
                m_PointColors = new Color[buckets];
            if (EnableColorMapping && (m_ColorValues == null || m_ColorValues.Length < buckets))
                m_ColorValues = new float[buckets];
            if (BucketReduce == XChartAggregateKind.Median && (m_BucketScratch == null || m_BucketScratch.Length < count))
                m_BucketScratch = new double[count];

            double minX, maxX, minY, maxY, minZ, maxZ;
            ResolveRange(DimensionX, out minX, out maxX);
            ResolveRange(DimensionY, out minY, out maxY);
            ResolveRange(DimensionZ, out minZ, out maxZ);
            double colorMin, colorMax;
            ResolveRange(ColorMapDimension, out colorMin, out colorMax);

            int written = 0;
            for (int b = 0; b < buckets; b++)
            {
                int first, end;
                if (!XChartBucketMath.TryBucketRange(count, b, buckets, out first, out end)) continue;

                double x, y, z;
                if (!XChartBucketMath.TryReduce(source, DimensionX, first, end, BucketReduce, m_BucketScratch, out x)
                    || !XChartBucketMath.TryReduce(source, DimensionY, first, end, BucketReduce, m_BucketScratch, out y)
                    || !XChartBucketMath.TryReduce(source, DimensionZ, first, end, BucketReduce, m_BucketScratch, out z))
                {
                    // 整桶坏值：这个桶不画，也不拿别的桶的值顶替（那会造出一个不在数据里的点）
                    continue;
                }

                m_CubePoints[written] = ChartToolSpace.NormalizedToCube(new Vector3(
                    Normalize(x, minX, maxX), Normalize(y, minY, maxY), Normalize(z, minZ, maxZ)));

                if (EnableColorMapping)
                {
                    double color;
                    if (!XChartBucketMath.TryReduce(source, ColorMapDimension, first, end, BucketReduce, m_BucketScratch, out color))
                        continue;
                    m_ColorValues[written] = Normalize(color, colorMin, colorMax);
                }
                written++;
            }

            if (written != m_CubePoints.Length) TrimPoints(written);
        }

        private void ClearPoints()
        {
            m_CubePoints = new Vector3[0];
            m_Projected = new Vector2[0];
            m_PointColors = null;
            m_ColorValues = null;
            m_BucketCount = 0;
            m_SourceRecords = 0;
        }

        /// <summary>
        /// 只保留前 <paramref name="count"/> 个点：渲染器<b>按数组长度</b>提交，
        /// 不裁掉尾巴就会画出一批"上一轮的旧点"。
        /// </summary>
        private void TrimPoints(int count)
        {
            System.Array.Resize(ref m_CubePoints, count);
            System.Array.Resize(ref m_Projected, count);
            if (m_PointColors != null) System.Array.Resize(ref m_PointColors, count);
            if (m_ColorValues != null) System.Array.Resize(ref m_ColorValues, count);
        }

        /// <summary>
        /// 显示范围只问上下文要（覆盖优先、否则该维聚合；规则在 <see cref="XChartDomainMath"/>）。
        /// <b>范围本身归 chart 的维度行</b>（数据板块），元素只说要哪一维——
        /// 元素自己再存一份范围就是"点和轴对不上"的来路。
        /// </summary>
        private void ResolveRange(int dimension, out double min, out double max)
        {
            var context = Context;
            if (context != null && context.TryResolveDomain(dimension, out min, out max)) return;
            min = 0.0;
            max = 1.0;
        }

        private static float Normalize(double value, double min, double max)
        {
            double span = max - min;
            if (span <= 0.0) return 0.5f;
            return Mathf.Clamp01((float)((value - min) / span));
        }

        private void ApplyColorMapping()
        {
            if (m_ColorValues == null || m_PointColors == null) return;
            for (int i = 0; i < m_PointColors.Length && i < m_ColorValues.Length; i++)
                m_PointColors[i] = Color.Lerp(ColorMapMin, ColorMapMax, m_ColorValues[i]);
        }

        protected override void OnDispose()
        {
            PointRenderer = null;
            m_CubePoints = null;
            m_Projected = null;
            m_PointColors = null;
            m_ColorValues = null;
        }

#if UNITY_EDITOR
        /// <summary>报表里点名这个组件这一帧提交了多少顶点——按点数长网格的元素靠它自证没撞 65535</summary>
        internal override void AppendDebugLine(System.Text.StringBuilder sb)
        {
            base.AppendDebugLine(sb);
            sb.Append(" points=").Append(m_CubePoints != null ? m_CubePoints.Length : 0)
              .Append(" pointVerts=").Append(PointRenderer != null ? PointRenderer.LiveVertexCount : 0)
              // 桶采样读数：画了几个点、源有多少条、取的是哪个统计量——"少画了多少"必须能看到
              .Append(" buckets=").Append(m_BucketCount)
              .Append(" records=").Append(m_SourceRecords)
              .Append(" reduce=").Append(BucketReduce);
        }
#endif
    }
}
