using System;
using System.Collections.Generic;
using UnityEngine;
using XericLibrary.Runtime.UIGraph;
using XericUI.Core;
using XericUI.XChart.Space;

namespace XericUI.XChart.Mesh
{
    /// <summary>
    /// 圆柱元素：两端圆盘（各自圆心/法线）+ 半径 → <b>视角下的外轮廓包边</b> + 轮廓填充
    /// + 可选端面十字 + 可选内部母线。
    ///
    /// <para><b>为什么不再"把两端顶点都连起来"</b>：扫掠体在人眼里的样子是它的<b>外轮廓</b>——
    /// 圆在正交投影下压成椭圆，"过圆心的长轴方向"那两条桥接线才是侧面的可见边界；
    /// 把相邻端面点一片片连成四边形，画出来的是"所有母线都可见"的线框，不是包边。
    /// 现在轮廓＝两片椭圆点集的凸包（对凸截面是精确解，平行/斜置/两端不同法线同一套代码），
    /// 填充就是同一个多边形。几何在 <see cref="XChartCylinderGeometry"/>（纯数学、离线可验）。</para>
    ///
    /// <para><b>投影只做三次</b>：每片端面只投影"圆心 + 两根基"，椭圆在 2D 上按参数式展开——
    /// 不再对盘上每点做 3D 投影（这是他要的"从 2D 上算，最大限度复用结果"）。</para>
    ///
    /// <para><b>弧走曲线渲染器、直线走折线渲染器</b>：库里 <c>Generation/Curve</c> 那套内部就是贝塞尔细分
    /// （所以放大也不会露出折点），轮廓的弧段与两个端面圆环都交给它，控制点由
    /// <see cref="XChartCylinderGeometry.BuildArcPiece"/> 算（我不自己切段）；十字与母线是直线，
    /// 留在折线渲染器上——他一 2026-10-23 的原话是"折线渲染器适合用来显示直线"。</para>
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.Chart + "/Mesh/Cylinder Tool")]
    public sealed class ChartToolCylinder : XChartToolBase
    {
        // ── 几何（立方体单位，±1 就是盒子边界） ───────────────────

        public Vector3 Start { get; set; } = new Vector3(-0.6f, -0.6f, -0.6f);
        public Vector3 End { get; set; } = new Vector3(0.6f, 0.6f, 0.6f);
        public float Radius { get; set; } = 0.22f;
        public int SegmentCount { get; set; } = 48;

        /// <summary>起点端面的法线。<b>零向量＝沿两端连线</b>（与旧行为一致）</summary>
        public Vector3 StartNormal { get; set; }
        /// <summary>终点端面的法线。零向量＝沿两端连线</summary>
        public Vector3 EndNormal { get; set; }

        /// <summary>圆柱自己的变换（叠在图的空间之上）</summary>
        public XChartToolTransform Transform { get; set; } = XChartToolTransform.Identity;

        // ── 几何来源：常量 / 数据（一组记录一根圆柱） ──

        /// <summary>关＝用 <see cref="Start"/>/<see cref="End"/> 的常量坐标；开＝从数据源取</summary>
        public bool DriveByData { get; set; }
        public int DimensionX { get; set; }
        public int DimensionY { get; set; } = 1;
        public int DimensionZ { get; set; } = 2;
        /// <summary>几条记录凑一根圆柱；2＝两两一组（记录 0-1、2-3…，不重叠也不并入上一组）</summary>
        public int GroupSize { get; set; } = 2;
        /// <summary>最多画几根</summary>
        public int MaxCylinders { get; set; } = 64;

        // ── 呈现（各有开关） ──────────────────────────────────────

        /// <summary>外轮廓包边</summary>
        public bool ShowOutline { get; set; } = true;
        public Color OutlineColor { get; set; } = new Color(0.75f, 0.85f, 1f, 0.9f);
        public float OutlineThickness { get; set; } = 2f;

        /// <summary>轮廓填充（同一个凸包多边形）</summary>
        public bool ShowFill { get; set; } = true;
        public Color FillColor { get; set; } = new Color(0.34f, 0.55f, 0.85f, 0.35f);

        /// <summary>端面十字：用端面自己的采样顶点连，所以它**永远与实际端面对得上**，能看出滚转</summary>
        public bool ShowCapCross { get; set; }
        public Color CrossColor { get; set; } = new Color(0.9f, 0.95f, 1f, 0.85f);
        public float CrossThickness { get; set; } = 1.5f;

        /// <summary>起点端面的闭合圆环（画满一整圈端面椭圆，＝端面自己的边界）</summary>
        public bool ShowStartRing { get; set; }
        /// <summary>终点端面的闭合圆环</summary>
        public bool ShowEndRing { get; set; }

        /// <summary>内部母线数量（把两端对应点连起来；0＝不画）。<b>默认 0</b>：轮廓之外的东西要显式要</summary>
        public int GeneratrixCount { get; set; }
        public Color GeneratrixColor { get; set; } = new Color(0.75f, 0.85f, 1f, 0.35f);

        // ── 内部 ──────────────────────────────────────────────────

        public XChartMeshFillRenderer FillRenderer { get; private set; }
        /// <summary>曲线渲染器（贝塞尔细分）：弧段走它。折线渲染器只留给直线（十字/母线）</summary>
        public UICurveRenderer CurveRenderer { get; private set; }
        public UILineRendererV2 WireRenderer { get; private set; }

        private Matrix4x4 m_Composed = Matrix4x4.identity;
        private int m_LastSpaceVersion = -1;
        private bool m_Dirty = true;

        private Vector2[] m_Scratch = new Vector2[0];
        private Vector2[] m_Outline = new Vector2[0];
        private byte[] m_Tags = new byte[0];
        private int[] m_RunStarts = new int[0];
        private int[] m_RunEnds = new int[0];
        private byte[] m_RunTags = new byte[0];
        private int m_RunCount;
        private readonly Vector2[] m_Bezier = new Vector2[4];
        private readonly float[] m_BezierWidths = new float[4];
        private readonly Vector2[] m_Cross = new Vector2[3];
        private int m_CurveEntries;
        private int m_RingArcs;
        private int m_RingFullCaps;
        private XChartCylinderDisc2D m_DiscStart;
        private XChartCylinderDisc2D m_DiscEnd;
        private int m_OutlineCount;

        // ── 多根圆柱：一组记录一根，组与组不重叠（尾巴凑不满一组就不画） ──
        private Vector3[] m_CylinderStart = new Vector3[0];
        private Vector3[] m_CylinderEnd = new Vector3[0];
        private float[] m_CylinderDepth = new float[0];
        private int[] m_CylinderOrder = new int[0];
        private int m_CylinderCount;
        private int m_GroupSkipped;
        private bool m_GroupCapped;

        /// <summary>数据版本：数据模式下几何随数据变，脏判定要把它算进来</summary>
        private long m_LastDataVersion = long.MinValue;
        /// <summary>本帧几何是怎么来的（报表用）：literal / data(groups …) / data-skipped(原因)</summary>
        private string m_GeometryNote = "literal";

        public void MarkDirty() => m_Dirty = true;

        protected override void OnInitialize()
        {
            FillRenderer = CreateChildComponent<XChartMeshFillRenderer>("CylinderFill");
            if (FillRenderer != null) Stretch(FillRenderer.rectTransform);

            // 弧段走曲线渲染器：它内部就是贝塞尔细分，所以椭圆弧不会像折线那样在接缝上露断口
            CurveRenderer = CreateChildComponent<UICurveRenderer>("CylinderCurve");
            if (CurveRenderer != null)
            {
                CurveRenderer.autoRebuild = false;   // 控制点由本工具每帧喂，别让它自己重建默认那条
                Stretch(CurveRenderer.rectTransform);
            }

            WireRenderer = CreateChildComponent<UILineRendererV2>("CylinderWire");
            if (WireRenderer != null)
            {
                WireRenderer.defaultColor = OutlineColor;
                WireRenderer.defaultThickness = OutlineThickness;
                Stretch(WireRenderer.rectTransform);
            }
            SetVisible(true);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public override void OnRender()
        {
            base.OnRender();
            if (FillRenderer == null) return;

            var space = Context?.Space;
            if (space == null) return;

            var data = Context?.Data;
            long dataVersion = data != null ? data.Version : 0L;
            if (!m_Dirty && space.ProjectionVersion == m_LastSpaceVersion
                && (!DriveByData || dataVersion == m_LastDataVersion)) return;
            m_Dirty = false;
            m_LastSpaceVersion = space.ProjectionVersion;
            m_LastDataVersion = dataVersion;
            m_Composed = space.Compose(Transform);

            int segments = Mathf.Clamp(SegmentCount, 8, 256);
            EnsureBuffers(segments);

            string note;
            if (!TryCollectCylinders(data, out note))
            {
                // 这一帧给不出几何：把上一帧收干净。留着旧几何是最坏的结果——那几根圆柱
                // 会被读成"当前值"。（不能走绘制路径：端面圆环吃的是上一帧的圆盘。）
                m_GeometryNote = note;
                ClearGeometry();
                return;
            }
            m_GeometryNote = note;

            SortCylindersByDepth(space);   // DepthOf 越大越近 ⇒ 先画远的
            BeginGeometry();
            for (int i = 0; i < m_CylinderCount; i++)
                DrawCylinder(space, segments, m_CylinderOrder[i]);
            EndGeometry();
        }

        private void BeginGeometry()
        {
            FillRenderer.ClearFaces();
            if (CurveRenderer != null) CurveRenderer.ClearAll();
            if (WireRenderer != null) WireRenderer.ClearAll();
            m_CurveEntries = 0;
            m_RingArcs = 0;
            m_RingFullCaps = 0;
        }

        private void EndGeometry()
        {
            FillRenderer.Apply();
            if (CurveRenderer != null) CurveRenderer.RebuildAll();
        }

        /// <summary>一根圆柱的全部落笔：两片端面 → 轮廓 → 填充 → 弧（轮廓/圆环）→ 直线（十字/母线）。
        /// 三个渲染器都是"先清一次、循环里追加、最后统一提交"，所以这里只追加、不提交。</summary>
        private void DrawCylinder(ChartToolSpace space, int segments, int index)
        {
            Vector3 start = m_CylinderStart[index];
            Vector3 end = m_CylinderEnd[index];
            float radius = Mathf.Max(1e-4f, Radius);

            XChartCylinderGeometry.ResolveNormals(StartNormal, EndNormal, start, end,
                out Vector3 startNormal, out Vector3 endNormal);
            bool built = BuildDisc(space, start, startNormal, radius, segments, out m_DiscStart)
                && BuildDisc(space, end, endNormal, radius, segments, out m_DiscEnd);

            m_OutlineCount = built
                ? XChartCylinderGeometry.BuildOutline(m_DiscStart, m_DiscEnd, segments, m_Scratch, m_Outline)
                : 0;

            AppendFill();
            AppendCurves(segments);
            AppendStraightWires();
        }

        /// <summary>
        /// 把三个渲染器清空到"什么都没画"，并且不动圆盘缓冲——给"这一帧拿不到几何"用。
        /// </summary>
        private void ClearGeometry()
        {
            m_OutlineCount = 0;
            m_CurveEntries = 0;
            m_RingArcs = 0;
            m_RingFullCaps = 0;

            FillRenderer.ClearFaces();
            FillRenderer.Apply();
            if (CurveRenderer != null)
            {
                CurveRenderer.ClearAll();
                CurveRenderer.RebuildAll();
            }
            if (WireRenderer != null) WireRenderer.ClearAll();
        }

        /// <summary>
        /// 这一帧要画哪几根圆柱。常量模式＝一根（配置里那两个坐标）；数据模式＝<b>按组</b>：
        /// 组 <c>g</c> 覆盖记录 <c>[g*GroupSize, g*GroupSize + GroupSize - 1]</c>，两端取<b>组首与组尾</b>，
        /// 组与组严格不重叠、也<b>不把上一组并进来</b>；凑不满一组的尾巴不画（记进 <see cref="m_GroupSkipped"/>）。
        ///
        /// <para><b>下标只有一个权威</b>（他 2026-10-24 的要求）：记录下标 0 = 最早、n-1 = 最新，
        /// 与数据窗口的行号同一套下标，所以这里只按<b>绝对下标</b>取记录——"往回数第几条"那种
        /// 相对量会让同一份数据在窗口里是第 1 条、在工具里却成了第 n-2 条（现场就是它）。</para>
        ///
        /// <para>数据值必须先用该维值域归一化到立方体单位才进几何（不归一化的话数据值可能上千，
        /// 直接飞出盒子）。每一步失败都给出可点名的原因，报表里印出来。</para>
        /// </summary>
        private bool TryCollectCylinders(IXChartDataSource data, out string note)
        {
            m_CylinderCount = 0;
            m_GroupSkipped = 0;
            m_GroupCapped = false;

            if (!DriveByData)
            {
                EnsureCylinderCapacity(1);
                Store(0, Start, End);
                note = "literal";
                return true;
            }

            if (data == null) { note = "data-skipped(no-source)"; return false; }

            int groupSize = Mathf.Max(2, GroupSize);
            int available = XChartBindingMath.CompleteGroupCount(data.RecordCount, groupSize);
            if (available == 0)
            {
                note = "data-skipped(need " + groupSize + " records per group, have " + data.RecordCount + ")";
                return false;
            }

            int groups = Mathf.Min(available, Mathf.Max(1, MaxCylinders));
            m_GroupCapped = groups < available;

            double minX, maxX, minY, maxY, minZ, maxZ;
            if (!TryResolveDomains(out minX, out maxX, out minY, out maxY, out minZ, out maxZ))
            {
                note = "data-skipped(no-domain)";
                return false;
            }

            EnsureCylinderCapacity(groups);
            for (int g = 0; g < groups; g++)
            {
                int first, last;
                if (!XChartBindingMath.TryResolveGroup(data.RecordCount, g, groupSize, out first, out last))
                {
                    m_GroupSkipped++;
                    continue;
                }

                Vector3 head, tail;
                if (!TryResolveNormalized(data, first, minX, maxX, minY, maxY, minZ, maxZ, out head)
                    || !TryResolveNormalized(data, last, minX, maxX, minY, maxY, minZ, maxZ, out tail))
                {
                    m_GroupSkipped++;
                    continue;
                }

                Store(m_CylinderCount, head, tail);
            }

            if (m_CylinderCount == 0) { note = "data-skipped(all-groups-rejected)"; return false; }

            int drawnRecords = m_CylinderCount * groupSize;
            note = "data(groups " + m_CylinderCount + "/" + available + ", size " + groupSize
                + ", rec 0->" + (drawnRecords - 1)
                + (m_GroupCapped ? ", capped" : string.Empty)
                + (m_GroupSkipped > 0 ? ", skip " + m_GroupSkipped : string.Empty) + ")";
            return true;
        }

        private void Store(int index, Vector3 start, Vector3 end)
        {
            m_CylinderStart[index] = start;
            m_CylinderEnd[index] = end;
            m_CylinderCount = index + 1;
        }

        private void EnsureCylinderCapacity(int count)
        {
            if (m_CylinderStart.Length >= count) return;
            m_CylinderStart = new Vector3[count];
            m_CylinderEnd = new Vector3[count];
            m_CylinderDepth = new float[count];
            m_CylinderOrder = new int[count];
        }

        private bool TryResolveNormalized(IXChartDataSource data, int record,
            double minX, double maxX, double minY, double maxY, double minZ, double maxZ, out Vector3 unit)
        {
            unit = Vector3.zero;
            Vector3 raw;
            if (!XChartBindingMath.TryResolvePosition(data, record, DimensionX, DimensionY, DimensionZ, out raw))
                return false;

            float x, y, z;
            if (!XChartBindingMath.TryNormalize(raw.x, minX, maxX, out x)
                || !XChartBindingMath.TryNormalize(raw.y, minY, maxY, out y)
                || !XChartBindingMath.TryNormalize(raw.z, minZ, maxZ, out z)) return false;

            unit = new Vector3(x, y, z);
            return true;
        }

        /// <summary>
        /// 按深度从远到近排序（<see cref="ChartToolSpace.DepthOf"/> 越大越靠近观察者）：
        /// 同一个工具内的多根圆柱靠<b>提交顺序</b>决定画家序（三个渲染器都不按深度自己排），
        /// 不排就会让近的被远的盖住。
        /// </summary>
        private void SortCylindersByDepth(ChartToolSpace space)
        {
            for (int i = 0; i < m_CylinderCount; i++)
            {
                m_CylinderDepth[i] = (space.DepthOf(m_CylinderStart[i], m_Composed)
                    + space.DepthOf(m_CylinderEnd[i], m_Composed)) * 0.5f;
                m_CylinderOrder[i] = i;
            }

            // 插入排序：根数有上限（MaxCylinders），且渲染路径里不分配
            for (int i = 1; i < m_CylinderCount; i++)
            {
                int key = m_CylinderOrder[i];
                float depth = m_CylinderDepth[key];
                int j = i - 1;
                while (j >= 0 && m_CylinderDepth[m_CylinderOrder[j]] > depth)
                {
                    m_CylinderOrder[j + 1] = m_CylinderOrder[j];
                    j--;
                }
                m_CylinderOrder[j + 1] = key;
            }
        }

        private bool TryResolveDomains(out double minX, out double maxX,
            out double minY, out double maxY, out double minZ, out double maxZ)
        {
            minX = maxX = minY = maxY = minZ = maxZ = 0d;
            var context = Context;
            if (context == null) return false;
            return context.TryResolveDomain(DimensionX, out minX, out maxX)
                && context.TryResolveDomain(DimensionY, out minY, out maxY)
                && context.TryResolveDomain(DimensionZ, out minZ, out maxZ);
        }

        /// <summary>
        /// 一片端面 → 2D 椭圆：<b>只投影圆心与两根基</b>。基向量的像素像就是椭圆的半轴，
        /// 半径并进半轴里，之后所有采样都在 2D 上做。
        /// </summary>
        private bool BuildDisc(ChartToolSpace space, Vector3 center, Vector3 normal, float radius,
            int segments, out XChartCylinderDisc2D disc)
        {
            disc = default(XChartCylinderDisc2D);
            Vector3 u, v;
            if (!XChartCylinderGeometry.TryOrthogonalBasis(normal, out u, out v)) return false;

            Vector2 centerPixels = space.Project(center, m_Composed);
            Vector2 uPixels = space.Project(center + u, m_Composed) - centerPixels;
            Vector2 vPixels = space.Project(center + v, m_Composed) - centerPixels;

            return XChartCylinderGeometry.TryProjectDisc(centerPixels, uPixels, vPixels, radius, out disc);
        }

        /// <summary>
        /// 填充＝轮廓多边形本身，用"扇形"喂进去（库里只有四边形图元）：每片是
        /// (中心, 第 i 点, 第 i+1 点, 第 i+1 点) 的退化三角形。整片轮廓在一处连续提交，
        /// 所以它相对其它元素是<b>整块</b>参与画家序，内部不再分片排序。
        /// </summary>
        private void AppendFill()
        {
            if (!ShowFill || m_OutlineCount < 3) return;

            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < m_OutlineCount; i++) centroid += m_Outline[i];
            centroid /= m_OutlineCount;

            for (int i = 0; i < m_OutlineCount; i++)
            {
                Vector2 a = m_Outline[i];
                Vector2 b = m_Outline[(i + 1) % m_OutlineCount];
                FillRenderer.AddFace(centroid, a, b, b, FillColor, FillColor);
            }
        }

        /// <summary>弧线交给曲线渲染器（贝塞尔细分），直线留在折线渲染器上——他 2026-10-23 的原话：
        /// "你现在用的折线渲染器就是分段的，适合用来显示直线"。</summary>
        private void AppendCurves(int segments)
        {
            if (CurveRenderer == null) return;

            ComputeRuns();
            if (ShowOutline && m_OutlineCount >= 3) AddOutlineCurves();
            if (ShowStartRing) AddRingCurves(1, segments);
            if (ShowEndRing) AddRingCurves(2, segments);
        }

        /// <summary>提交一节三次贝塞尔（4 个控制点）。控制点会被库拷进它自己的扁平缓存，所以复用 scratch 安全。</summary>
        private void AddCurveEntry(Color color)
        {
            for (int i = 0; i < m_BezierWidths.Length; i++) m_BezierWidths[i] = OutlineThickness;
            var entry = new CurveCacheEntry
            {
                startColor = color,
                endColor = color,
                tessellationSegments = Mathf.Clamp(SegmentCount / 6, 4, 32),
            };
            CurveRenderer.AddCurve(entry, m_Bezier, m_BezierWidths);
            m_CurveEntries++;
        }

        /// <summary>端面圆环＝把端面圆<b>补成整圈</b>：包边已经画了那一片的<b>外侧弧</b>，
    /// 环就补剩下的<b>内侧弧</b>（同两个端点、反方向走）——他 2026-10-23 的要求：
    /// "关闭 start/end ring 时消失的应该是内部圆环那部分"，而且"画整圈会和包边重复出两条线框"。
    /// 包边没在画（或这一片在轮廓上只占一个点）时，环自己补整圈。</summary>
        private void AddRingCurves(byte tag, int segments)
        {
            XChartCylinderDisc2D disc = tag == 1 ? m_DiscStart : m_DiscEnd;
            int index = FindRun(tag);
            if (!ShowOutline || index < 0)
            {
                // 包边没在画，或这一片整个躲在外轮廓里（凸包上连一段可见弧都没有）⇒ 只能画整圈
                m_RingFullCaps++;
                AddFullRing(disc, segments);
                return;
            }

            if (m_RunStarts[index] == m_RunEnds[index])
            {
                m_RingFullCaps++;
                AddFullRing(disc, segments);
                return;
            }

            // 补弧：起点接在可见弧的终点、扫过 2π-|可见|（显式走长路，不能用"反向短路"表达）
            if (!XChartCylinderGeometry.TryParameterOf(disc, m_Outline[m_RunStarts[index]], out float fromAngle))
                return;
            if (!XChartCylinderGeometry.TryParameterOf(disc, m_Outline[m_RunEnds[index]], out float toAngle))
                return;
            float visible = XChartCylinderGeometry.SweepToDirection(toAngle - fromAngle, DirectionOfRun(index));
            XChartCylinderGeometry.RingArc(fromAngle, visible, out float ringStart, out float ringSweep);
            AddArcPieces(disc, ringStart, ringSweep);
            m_RingArcs++;
        }

        /// <summary>整圈（每节 ≤90°；与采样段数解耦，所以放大也不会露出折点）</summary>
        private void AddFullRing(in XChartCylinderDisc2D disc, int segments)
        {
            int pieces = Mathf.Max(1, segments / 4);
            for (int p = 0; p < pieces; p++)
            {
                XChartCylinderGeometry.BuildArcPiece(disc, 0f, 6.2831855f, pieces, p, m_Bezier);
                AddCurveEntry(OutlineColor);
            }
        }

        /// <summary>
        /// 轮廓＝弧段 + 桥接段。凸包点先按"落在哪片端面的椭圆上"打标记，同标记的连续段是椭圆弧
        /// （走贝塞尔），跨标记那条就是"过圆心的长轴方向连外框"的桥接线（走直线）。
        /// </summary>
        private void AddOutlineCurves()
        {
            for (int r = 0; r < m_RunCount; r++)
            {
                int start = m_RunStarts[r];
                int end = m_RunEnds[r];
                if (m_RunTags[r] != 0 && start != end)
                {
                    AddArcBetween(DiscOfRun(r), m_Outline[start], m_Outline[end],
                        DirectionOfRun(r), m_Outline[start]);
                }

                int nextStart = m_RunStarts[(r + 1) % m_RunCount];
                AddStraightCurve(m_Outline[end], m_Outline[nextStart]);   // 段与段之间（含首尾相接那条桥接）
            }
        }

        /// <summary>
        /// 把凸包切成"同端面的连续段"。切分本身在纯几何里（`SplitOutlineRuns`），因为
        /// "哪半边在轮廓里"这种判定必须能离线验。
        /// </summary>
        private void ComputeRuns()
        {
            m_RunCount = 0;
            if (m_OutlineCount < 3) return;

            EnsureRunBuffers(m_OutlineCount);
            m_RunCount = XChartCylinderGeometry.SplitOutlineRuns(m_Outline, m_OutlineCount, m_DiscStart, m_DiscEnd,
                m_Tags, m_RunStarts, m_RunEnds, m_RunTags);
        }

        private void EnsureRunBuffers(int count)
        {
            if (m_Tags.Length < count)
            {
                m_Tags = new byte[count];
                m_RunStarts = new int[count];
                m_RunEnds = new int[count];
                m_RunTags = new byte[count];
            }
        }

        private int FindRun(byte tag)
        {
            for (int r = 0; r < m_RunCount; r++)
            {
                if (m_RunTags[r] == tag) return r;
            }
            return -1;
        }

        private XChartCylinderDisc2D DiscOfRun(int index) => m_RunTags[index] == 1 ? m_DiscStart : m_DiscEnd;

        /// <summary>这一段弧的转向：看段内第二个点（凸包是逆时针，弧必须顺着它走）</summary>
        private int DirectionOfRun(int index)
        {
            if (m_RunStarts[index] == m_RunEnds[index]) return 1;
            XChartCylinderDisc2D disc = DiscOfRun(index);
            int second = (m_RunStarts[index] + 1) % m_OutlineCount;
            if (!XChartCylinderGeometry.TryParameterOf(disc, m_Outline[m_RunStarts[index]], out float fromAngle))
                return 1;
            if (!XChartCylinderGeometry.TryParameterOf(disc, m_Outline[second], out float secondAngle))
                return 1;
            return XChartCylinderGeometry.ShortestDirection(secondAngle - fromAngle);
        }

        /// <summary>提交一段椭圆弧；<paramref name="direction"/> 非 0 时强制转向，为 0 时按 <paramref name="hintPoint"/> 定</summary>
        private void AddArcBetween(in XChartCylinderDisc2D disc, Vector2 fromPoint, Vector2 toPoint,
            int direction, Vector2 hintPoint)
        {
            if (!XChartCylinderGeometry.TryParameterOf(disc, fromPoint, out float fromAngle)) return;
            if (!XChartCylinderGeometry.TryParameterOf(disc, toPoint, out float toAngle)) return;

            if (direction == 0 && XChartCylinderGeometry.TryParameterOf(disc, hintPoint, out float hintAngle))
                direction = XChartCylinderGeometry.ShortestDirection(hintAngle - fromAngle);

            AddArcPieces(disc, fromAngle, XChartCylinderGeometry.SweepToDirection(toAngle - fromAngle, direction));
        }

        /// <summary>把"起点角 + 扫过角"切成 ≤90° 的贝塞尔节提交</summary>
        private void AddArcPieces(in XChartCylinderDisc2D disc, float fromAngle, float sweep)
        {
            int pieces = XChartCylinderGeometry.ArcPieceCount(sweep);
            for (int p = 0; p < pieces; p++)
            {
                XChartCylinderGeometry.BuildArcPiece(disc, fromAngle, sweep, pieces, p, m_Bezier);
                AddCurveEntry(OutlineColor);
            }
        }

        private void AddStraightCurve(Vector2 from, Vector2 to)
        {
            if ((from - to).sqrMagnitude < 1e-8f) return;
            XChartCylinderGeometry.BuildLineBezier(from, to, m_Bezier);
            AddCurveEntry(OutlineColor);
        }

        private void AppendStraightWires()
        {
            if (WireRenderer == null) return;

            if (ShowCapCross)
            {
                DrawCross(m_DiscStart);
                DrawCross(m_DiscEnd);
            }

            if (GeneratrixCount > 0 && m_OutlineCount > 0) DrawGeneratrices();
        }

        /// <summary>
        /// 一整条折线一次提交（<b>不要</b>一段一段 DrawLine）：库里网格生成器的"转角连接三角形"只在
        /// 同一根线内的相邻子段之间补，所以逐段画会在**每个转角留下断口**——他 2026-10-23 放大看到的
        /// "拉近了有断口"就是这么来的。闭合的多边形靠把首点补到末尾来实现。
        /// </summary>
        private void DrawPolyline(Vector2[] points, int count, bool close, Color color, float thickness)
        {
            int length = count + (close ? 1 : 0);
            if (length < 2) return;
            var buffer = new Vector2[length];
            for (int i = 0; i < count; i++) buffer[i] = points[i];
            if (close) buffer[count] = points[0];
            WireRenderer.DrawPolyline(buffer, color, thickness);
        }

        private void DrawCross(in XChartCylinderDisc2D disc)
        {
            Vector2 left, top, right, bottom;
            XChartCylinderGeometry.CrossPoints(disc, SegmentCount, out left, out top, out right, out bottom);
            // 过圆心的三段折线（左-心-右 / 上-心-下）：转角连接由同一条线内的接缝补齐
            m_Cross[0] = left;
            m_Cross[1] = disc.Center;
            m_Cross[2] = right;
            DrawPolyline(m_Cross, 3, false, CrossColor, CrossThickness);
            m_Cross[0] = top;
            m_Cross[1] = disc.Center;
            m_Cross[2] = bottom;
            DrawPolyline(m_Cross, 3, false, CrossColor, CrossThickness);
        }

        /// <summary>
        /// 内部母线：两端<b>对应</b>采样点连线（不是随便连）——对应关系由同一个采样角保证，
        /// 所以它和端面十字是同一套顶点，转起来不会互相错位。
        /// </summary>
        private void DrawGeneratrices()
        {
            int segments = Mathf.Clamp(SegmentCount, 8, 256);
            int stride = Mathf.Max(1, segments / Mathf.Max(1, GeneratrixCount));
            for (int i = 0; i < segments; i += stride)
            {
                Vector2 from = m_DiscStart.PointAtStep(i, segments);
                Vector2 to = m_DiscEnd.PointAtStep(i, segments);
                WireRenderer.DrawLine(from, to, GeneratrixColor, OutlineThickness);
            }
        }

        private void EnsureBuffers(int segments)
        {
            // scratch 装两片椭圆的采样点；包点缓冲按凸包容量口径要 2×采样数
            int samples = segments * 2;
            if (m_Scratch.Length < samples)
            {
                m_Scratch = new Vector2[samples];
                m_Outline = new Vector2[samples * 2];
            }
        }

        protected override void OnDispose()
        {
            FillRenderer = null;
            CurveRenderer = null;
            WireRenderer = null;
            m_Scratch = null;
            m_Outline = null;
            m_Tags = null;
        }

#if UNITY_EDITOR
        /// <summary>报表里点名轮廓点数与各开关：0 要能分清"没算"还是"算出来是空"（轮廓点数=0 时 m_Dirty 也停了）</summary>
        internal override void AppendDebugLine(System.Text.StringBuilder sb)
        {
            base.AppendDebugLine(sb);
            sb.Append(" outlinePoints=").Append(m_OutlineCount)
              .Append(" curveEntries=").Append(m_CurveEntries)
              .Append(" fill=").Append(ShowFill)
              .Append(" fillChunks=").Append(FillRenderer != null ? FillRenderer.ChunkCount : 0)
              .Append(" fillMaxVerts=").Append(FillRenderer != null ? FillRenderer.MaxChunkVertices : 0)
              .Append(" curveVerts=").Append(CurveRenderer != null ? CurveRenderer.LiveVertexCount : 0)
              .Append(" wireVerts=").Append(WireRenderer != null ? WireRenderer.LiveVertexCount : 0)
              .Append(" ringStart=").Append(ShowStartRing)
              .Append(" ringEnd=").Append(ShowEndRing)
              .Append(" ringArcs=").Append(m_RingArcs)
              .Append(" ringFullCaps=").Append(m_RingFullCaps)
              .Append(" cross=").Append(ShowCapCross)
              .Append(" generatrices=").Append(GeneratrixCount)
              .Append(" normals=").Append(StartNormal.sqrMagnitude > 1e-8f ? "explicit" : "alongAxis")
              .Append("/").Append(EndNormal.sqrMagnitude > 1e-8f ? "explicit" : "alongAxis")
              // 几何来源：数据模式下必须能看出"这一帧画了几根、取的是哪几组记录"，
              // 否则 skipped 与 literal、以及"截断了多少"都分不清
              .Append(" geometry=").Append(m_GeometryNote)
              .Append(" cylinders=").Append(m_CylinderCount);
        }
#endif
    }
}