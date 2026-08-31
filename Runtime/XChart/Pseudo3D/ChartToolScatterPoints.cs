using UnityEngine;
using XericLibrary.Runtime.UIGraph;
using PrimitiveType = XericLibrary.Runtime.UIGraph.PrimitiveType;

namespace XericUI.XChart.Pseudo3D
{
    /// <summary>
    /// 散点渲染工具。
    ///
    /// 使用 XScatterPointRenderer 渲染 3D 散点数据在 2D 平面上的投影。
    /// 数据点从 3D 立方体空间投影到 UI 坐标，每个点用一个图元表示。
    ///
    /// 子节点结构：
    ///   [ChartTool_ScatterPoints]
    ///     └─ PointRenderer (XScatterPointRenderer)
    /// </summary>
    [AddComponentMenu("Xeric/XChart/Pseudo3D/Scatter Points Tool")]
    public class ChartToolScatterPoints : XChartToolBase
    {
        // ── 配置 ──────────────────────────────────────────────────

        /// <summary>点的大小（像素）</summary>
        public float PointSize
        {
            get => m_PointSize;
            set { m_PointSize = Mathf.Max(0.5f, value); if (IsInitialized) SyncShapeParams(); }
        }
        private float m_PointSize = 8f;

        /// <summary>图元类型</summary>
        public PrimitiveType PointType
        {
            get => m_PointType;
            set { m_PointType = value; if (IsInitialized) SyncShapeParams(); }
        }
        private PrimitiveType m_PointType = PrimitiveType.Polygon;

        /// <summary>尺寸模式</summary>
        public SizeMode SizeMode
        {
            get => m_SizeMode;
            set { m_SizeMode = value; if (IsInitialized) SyncShapeParams(); }
        }
        private SizeMode m_SizeMode = SizeMode.InscribedCircle;

        /// <summary>填充颜色</summary>
        public Color PointColor
        {
            get => m_PointColor;
            set { m_PointColor = value; if (IsInitialized) SyncShapeParams(); }
        }
        private Color m_PointColor = new Color(0.33f, 0.67f, 1f, 1f);

        /// <summary>中心渐变颜色</summary>
        public Color CenterColor
        {
            get => m_CenterColor;
            set { m_CenterColor = value; if (IsInitialized) SyncShapeParams(); }
        }
        private Color m_CenterColor = Color.white;

        /// <summary>边框颜色</summary>
        public Color BorderColor
        {
            get => m_BorderColor;
            set { m_BorderColor = value; if (IsInitialized) SyncShapeParams(); }
        }
        private Color m_BorderColor = new Color(0.2f, 0.2f, 0.2f, 1f);

        /// <summary>边框宽度</summary>
        public float BorderThickness
        {
            get => m_BorderThickness;
            set { m_BorderThickness = Mathf.Max(0f, value); if (IsInitialized) SyncShapeParams(); }
        }
        private float m_BorderThickness = 1f;

        /// <summary>多边形边数</summary>
        public int SideCount
        {
            get => m_SideCount;
            set { m_SideCount = Mathf.Max(3, value); if (IsInitialized) SyncShapeParams(); }
        }
        private int m_SideCount = 8;

        /// <summary>倒角大小</summary>
        public float ChamferSize
        {
            get => m_ChamferSize;
            set { m_ChamferSize = Mathf.Clamp01(value); if (IsInitialized) SyncShapeParams(); }
        }
        private float m_ChamferSize = 0f;

        /// <summary>图元旋转角度</summary>
        public float PointAngle
        {
            get => m_PointAngle;
            set { m_PointAngle = value; if (IsInitialized) SyncShapeParams(); }
        }
        private float m_PointAngle = 0f;

        /// <summary>是否使用颜色渐变（按数据范围）</summary>
        public bool EnableColorMapping { get; set; } = false;

        /// <summary>颜色映射渐变起始色</summary>
        public Color ColorMapMin { get; set; } = Color.blue;

        /// <summary>颜色映射渐变结束色</summary>
        public Color ColorMapMax { get; set; } = Color.red;

        /// <summary>颜色映射轴（0=X, 1=Y, 2=Z）</summary>
        public int ColorMapAxis { get; set; } = 2;

        // ── 内部组件 ──────────────────────────────────────────────

        /// <summary>散点渲染器</summary>
        public XScatterPointRenderer PointRenderer { get; private set; }

        /// <summary>对 CubeBg 工具的引用（用于投影）</summary>
        public ChartToolCubeBg CubeBg { get; set; }

        // ── 数据缓存 ──────────────────────────────────────────────

        private Vector3[] m_NormalizedPoints = new Vector3[0];
        private Vector2[] m_ProjectedPoints = new Vector2[0];
        private bool m_DataDirty = true;
        private int m_LastProjectionVersion = -1;

        // ── 初始化 ────────────────────────────────────────────────

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

        // ── 形状参数同步 ──────────────────────────────────────────

        private void SyncShapeParams()
        {
            if (PointRenderer == null) return;
            PointRenderer.pointType = m_PointType;
            PointRenderer.sizeMode = m_SizeMode;
            PointRenderer.pointSize = m_PointSize;
            PointRenderer.fillColor = m_PointColor;
            PointRenderer.centerColor = m_CenterColor;
            PointRenderer.borderColor = m_BorderColor;
            PointRenderer.borderThickness = m_BorderThickness;
            PointRenderer.sideCount = m_SideCount;
            PointRenderer.chamferSize = m_ChamferSize;
            PointRenderer.chamferSegments = 4;
            PointRenderer.angle = m_PointAngle;

            // 如果已有数据，触发形状重建
            if (m_NormalizedPoints.Length > 0)
            {
                PointRenderer.UpdateShapeParams();
            }
        }

        // ── 数据更新 ──────────────────────────────────────────────

        /// <summary>
        /// 设置要渲染的 3D 点（归一化到 [-1, 1] 立方体空间）。
        /// </summary>
        public void SetNormalizedPoints(Vector3[] normalizedPoints)
        {
            m_NormalizedPoints = normalizedPoints ?? new Vector3[0];
            m_ProjectedPoints = new Vector2[m_NormalizedPoints.Length];
            m_DataDirty = true;
        }

        /// <summary>标记数据需要重新投影</summary>
        public void MarkDataDirty()
        {
            m_DataDirty = true;
        }

        // ── 渲染 ──────────────────────────────────────────────────

        public override void OnRender()
        {
            base.OnRender();
            if (PointRenderer == null || CubeBg == null) return;
            if (m_NormalizedPoints.Length == 0) return;

            bool projectionChanged = CubeBg.ProjectionVersion != m_LastProjectionVersion;

            // 如果数据脏了或立方体投影发生了变化，重新投影所有点
            if (m_DataDirty || projectionChanged)
            {
                m_LastProjectionVersion = CubeBg.ProjectionVersion;

                // 使用 CubeBg 的投影能力将 3D 点投影到 2D
                for (int i = 0; i < m_NormalizedPoints.Length; i++)
                {
                    m_ProjectedPoints[i] = CubeBg.ProjectPoint3D(m_NormalizedPoints[i]);
                }

                // 颜色映射
                if (EnableColorMapping)
                {
                    ApplyColorMapping();
                }

                // 批量更新到渲染器
                if (EnableColorMapping && m_PointColors != null)
                {
                    PointRenderer.SetPoints(m_ProjectedPoints, m_PointColors);
                }
                else
                {
                    PointRenderer.SetPoints(m_ProjectedPoints);
                }

                m_DataDirty = false;
            }
        }

        // ── 颜色映射 ──────────────────────────────────────────────

        private Color[] m_PointColors;

        private void ApplyColorMapping()
        {
            if (m_NormalizedPoints.Length == 0) return;

            if (m_PointColors == null || m_PointColors.Length != m_NormalizedPoints.Length)
                m_PointColors = new Color[m_NormalizedPoints.Length];

            for (int i = 0; i < m_NormalizedPoints.Length; i++)
            {
                float value = m_NormalizedPoints[i][ColorMapAxis];
                // 从 [-1, 1] 映射到 [0, 1]
                float t = Mathf.Clamp01((value + 1f) * 0.5f);
                m_PointColors[i] = Color.Lerp(ColorMapMin, ColorMapMax, t);
            }
        }

        // ── 清理 ──────────────────────────────────────────────────

        protected override void OnDispose()
        {
            PointRenderer = null;
            CubeBg = null;
            m_NormalizedPoints = null;
            m_ProjectedPoints = null;
            m_PointColors = null;
        }
    }
}
