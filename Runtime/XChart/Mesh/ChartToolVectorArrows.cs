using System.Collections.Generic;
using UnityEngine;
using XericLibrary.Runtime.UIGraph;
using XericUI.Core;
using XericUI.XChart.Space;

namespace XericUI.XChart.Mesh
{
    /// <summary>
    /// 向量场 / 流形箭头元素。<b>数据口径（规范 §25.2，2026-10-25 他澄清后定稿）</b>：
    /// <b>前三维＝坐标 x/y/z</b>（与圆柱、散点同一套归一化，折法见
    /// <see cref="XChartCubeFoldMath.TryFoldCube"/> 与 §28/§32），<b>一支箭头＝相邻两条记录</b>
    /// （第 i 条是起点，第 i+1 条按 <see cref="DirectionSource"/> 解释成"第二个点"或"方向矢量"），
    /// <b>第 4~7 维是颜色</b>（色相 0..256 色环 / 饱和度 / 明度 / 不透明度，可选）。
    ///
    /// <para><b>两种读法</b>：绝对 ⇒ 第二条记录的坐标就是方向（起点 + 方向×倍率），螺旋点画出来是一片"刺猬"；
    /// 相对 ⇒ 它是第二个点（起点 + 差向量×倍率），一串首尾相接的点读起来就是"连着头转"。</para>
    ///
    /// <para><b>画法＝线 + 库自带的箭头标记</b>（他 2026-10-26 的纠正，规范 §29）：杆是一条
    /// <b>共线</b>的三次贝塞尔条目（细分出来就是直的，宽度＝<see cref="ShaftWidth"/>，
    /// 起止色就是沿杆渐变），头是曲线渲染器原生的 <see cref="ArrowHeadData"/> 标记，
    /// 它在曲线上的位置由 <see cref="HeadPosition"/> 给（就是标记的 <c>progress</c>）。
    /// 上一版把杆和头都拼成填充四边形，等于绕开了库已经提供的能力，还多养了一条坏掉的路径。</para>
    ///
    /// <para><b>连续绘制</b>：把每条记录的坐标按顺序连成 Catmull-Rom 贝塞尔路径（线<b>真的经过每个数据点</b>），
    /// 标记挂在那一节上；这时"相对/绝对"不适用 ⇒ 配置里那一项隐藏（互斥显示）。</para>
    ///
    /// <para><b>数据只经过中转接口</b>：按维度下标 <see cref="IXChartDataSource.GetValueAt"/> 逐条取，
    /// 不碰记录数组；支数由 <see cref="MaxArrowCount"/> 兜住，免得十万条记录一口气投影。</para>
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.Chart + "/Mesh/Vector Arrow Tool")]
    public sealed class ChartToolVectorArrows : XChartToolBase
    {
        private int m_CoordinateDimension;
        /// <summary>坐标的维度下标（x/y/z 三维）</summary>
        public int CoordinateDimension { get => m_CoordinateDimension; set { m_CoordinateDimension = value; MarkDirty(); } }
        private XChartArrowDirectionSource m_DirectionSource = XChartArrowDirectionSource.NextRecordAsVector;
        /// <summary>方向从哪来（四档，箭数与维数需求都跟着它变；算式在 XChartVectorFieldMath 里）</summary>
        public XChartArrowDirectionSource DirectionSource
        {
            get => m_DirectionSource;
            set { m_DirectionSource = value; MarkDirty(); }
        }
        private bool m_Continuous;
        /// <summary>把每条记录的坐标按顺序连成路径（此时相对/绝对不适用）</summary>
        public bool Continuous { get => m_Continuous; set { m_Continuous = value; MarkDirty(); } }
        private float m_DirectionScale = 0.25f;
        /// <summary>方向向量的长度倍率（立方体单位）；相对模式下作用在差向量上</summary>
        public float DirectionScale { get => m_DirectionScale; set { m_DirectionScale = value; MarkDirty(); } }
        private int m_MaxArrowCount = 512;
        /// <summary>最多画多少支（一支＝相邻两条记录）</summary>
        public int MaxArrowCount { get => m_MaxArrowCount; set { m_MaxArrowCount = value; MarkDirty(); } }

        private XChartToolTransform m_Transform = XChartToolTransform.Identity;
        /// <summary>箭头自己的变换（叠在图的空间之上）</summary>
        public XChartToolTransform Transform { get => m_Transform; set { m_Transform = value; MarkDirty(); } }

        private XChartValueOutOfRange m_OutOfRange = XChartValueOutOfRange.RenderAsIs;
        /// <summary>
        /// 数据落在显示范围之外时怎么办（默认<b>原样画</b>——显示范围的本分是定标尺，不是决定数据画不画，§32）。
        /// <see cref="XChartValueOutOfRange.HideOutOfRange"/> 是整条记录不画（不是只藏越轴的那一半）。
        /// </summary>
        public XChartValueOutOfRange OutOfRange
        {
            get => m_OutOfRange;
            set { m_OutOfRange = value; MarkDirty(); }
        }

        // ── 颜色：可选的三到四个数值维 ────────────────────────────

        private bool m_UseColorDimensions;
        /// <summary>是否用额外的数值维给每支箭头上色</summary>
        public bool UseColorDimensions { get => m_UseColorDimensions; set { m_UseColorDimensions = value; MarkDirty(); } }
        private int m_ColorDimension = 3;
        /// <summary>颜色维的起点（色相、饱和度、明度 [+不透明度]）</summary>
        public int ColorDimension { get => m_ColorDimension; set { m_ColorDimension = value; MarkDirty(); } }
        private bool m_HasAlphaDimension;
        /// <summary>是否多读一维不透明度</summary>
        public bool HasAlphaDimension { get => m_HasAlphaDimension; set { m_HasAlphaDimension = value; MarkDirty(); } }

        private Color m_ArrowColor = new Color(0.95f, 0.6f, 0.25f, 0.9f);
        public Color ArrowColor { get => m_ArrowColor; set { m_ArrowColor = value; MarkDirty(); } }
        private float m_ShaftWidth = 2f;
        /// <summary>杆宽（像素）＝曲线条目的宽度</summary>
        public float ShaftWidth { get => m_ShaftWidth; set { m_ShaftWidth = value; MarkDirty(); } }
        private float m_HeadLength = 10f;
        /// <summary>头长（像素，<b>沿切线</b>）＝标记的 <c>width</c></summary>
        public float HeadLength { get => m_HeadLength; set { m_HeadLength = value; MarkDirty(); } }
        private float m_HeadWidth = 7f;
        /// <summary>头宽（像素，<b>垂直切线</b>）＝标记的 <c>height</c></summary>
        public float HeadWidth { get => m_HeadWidth; set { m_HeadWidth = value; MarkDirty(); } }
        private bool m_ShowZeroVectorsAsDots;
        /// <summary>零向量画成一个<b>圆点标记</b>；关掉就什么都不画（那种记录在画面上等于不存在）</summary>
        public bool ShowZeroVectorsAsDots { get => m_ShowZeroVectorsAsDots; set { m_ShowZeroVectorsAsDots = value; MarkDirty(); } }

        /// <summary>
        /// 唯一的绘制出口：库里的贝塞尔细分渲染器（圆柱弧段同一件）。
        /// <b>杆与头都从它走</b>——直线是"共线控制点"的特例（§10.4：折线逐段画会在转角留断口，
        /// 而这里连直线都不需要它），头是它自带的标记。
        /// 它不在位时这个元素什么都画不出来，报表 <c>path=none</c> 当场说清，不退回任何"备用画法"。
        /// </summary>
        public UICurveRenderer CurveRenderer { get; private set; }

        private float m_HeadPosition = 1f;
        /// <summary>
        /// 连续绘制时<b>箭头在路径上的哪个位置</b>（0＝起点、1＝末端，默认 1）：
        /// 直接交给标记的 <c>progress</c>，路径不剪断——线从标记底下穿过去是矢量场画法的常态，
        /// "剪开"是上一版我自己加的额外机制，库的标记本来就不需要它（§29）。
        /// </summary>
        public float HeadPosition { get => m_HeadPosition; set { m_HeadPosition = value; MarkDirty(); } }

        private float m_MarkerAnchor = 1f;
        /// <summary>
        /// 头贴在矢量端点的哪一处：0＝尾部落在点上（向前伸）、0.5＝中心对点、1＝<b>头尖落在点上</b>（默认）。
        /// 它就是库那个标记的 <c>depthCompensation</c>（映射＝anchor×2−1，在纯算术里）。
        /// </summary>
        public float MarkerAnchor { get => m_MarkerAnchor; set { m_MarkerAnchor = value; MarkDirty(); } }

        private int m_PathSegments = 12;
        /// <summary>连续绘制时每段路径细分成几节（曲线渲染器的 tessellation）</summary>
        public int PathSegments { get => m_PathSegments; set { m_PathSegments = value; MarkDirty(); } }

        private readonly List<Vector2> m_Pixels = new List<Vector2>(64);
        // 路径还要留着<b>立方体单位</b>的那一份：头的投影比例要用"这一节在 3D 里多长"作分母
        private readonly List<Vector3> m_Cubes = new List<Vector3>(64);
        private readonly Vector2[] m_Bezier = new Vector2[XChartArrowPathMath.ControlPointsPerSegment];
        private readonly float[] m_Widths = new float[XChartArrowPathMath.ControlPointsPerSegment];
        // 一支箭头一个标记，所以一条曲线最多挂一个：数组长度 1 是刻意的（AddCurve 吃的是数组）
        private readonly ArrowHeadData[] m_Heads = new ArrowHeadData[1];
        private string m_PathRenderer = "-";
        private int m_CurveEntries;
        private int m_Markers;
        private float m_HeadScale = 1f;

        private Matrix4x4 m_Composed = Matrix4x4.identity;
        private int m_LastSpaceVersion = -1;
        private long m_LastDataVersion = -1;
        private bool m_Dirty = true;

        // 本帧的自述读数：算了几支 / 表里几个数值维 / 需要几个 / 几条记录 / 跳过几条
        private int m_ArrowsBuilt;
        private int m_DimensionsHave;
        private int m_DimensionsNeed;
        private int m_RecordsHave;
        private int m_PointsSkipped;
        // 坏数据（非有限）与"按策略隐藏"是两件事：前者是数据不可用，后者是我们主动不画（§32）
        private int m_PointsHidden;

        // 三条坐标轴各自的显示范围（每帧现问上下文要：覆盖优先、否则该维聚合）。
        // 数据值必须经它们折成立方体单位才投影——直接当立方体单位用会把整场箭头投到盒子外面（§28）
        private double m_MinX, m_MaxX, m_MinY, m_MaxY, m_MinZ, m_MaxZ;
        private bool m_HasDomains;

        public override void MarkDirty() => m_Dirty = true;

        /// <summary>
        /// 这个元素要几个数值维：坐标那一半<b>随方向来源变</b>（本条自带方向那档要 6：0~2 点、3~5 方向），
        /// 再加（可选）颜色 3。<b>不透明度那一维不算硬需求</b>：多给就多读、没给退回单色 alpha——
        /// 把它算进门槛的话，勾一下"有不透明度"会让整个元素突然一条都不画，那是把开关变成了总闸。
        /// </summary>
        public int RequiredDimensionCount
            => XChartVectorFieldMath.RequiredCoordinateDimensions(DirectionSource)
               + (UseColorDimensions ? XChartArrowConfig.ColorDimensionCount : 0);

        /// <summary>几条记录凑一支：本条自带方向那档一条就够，其余要两条</summary>
        public int RequiredRecordCount
            => DirectionSource == XChartArrowDirectionSource.ThisRecordCarriesVector ? 1 : 2;

        /// <summary>这一支箭头在屏幕上的"像素／立方体单位"比例（头的尺寸吃它，§30）</summary>
        public float LastHeadScale => m_HeadScale;

        protected override void OnInitialize()
        {
            CurveRenderer = CreateChildComponent<UICurveRenderer>("ArrowCurve");
            if (CurveRenderer != null)
            {
                // 控制点、宽度、标记每帧由本工具喂：别让它自己重建默认那条
                CurveRenderer.autoRebuild = false;
                var rect = CurveRenderer.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            SetVisible(true);
        }

        public override void OnRender()
        {
            base.OnRender();

            var space = Context?.Space;
            if (space == null) return;

            long dataVersion = Data != null ? Data.Version : -1;
            if (!m_Dirty && space.ProjectionVersion == m_LastSpaceVersion && dataVersion == m_LastDataVersion)
                return;

            // 渲染器不在位先把"这一帧确实重新算过、但画不出来"记下来：不然读数会停在上一帧
            if (CurveRenderer == null)
            {
                m_Dirty = false;
                m_LastSpaceVersion = space.ProjectionVersion;
                m_LastDataVersion = dataVersion;
                m_PathRenderer = "none";
                m_ArrowsBuilt = 0;
                m_CurveEntries = 0;
                m_Markers = 0;
                return;
            }

            m_Dirty = false;
            m_LastSpaceVersion = space.ProjectionVersion;
            m_LastDataVersion = dataVersion;
            m_Composed = space.Compose(Transform);

            // ClearAll 连标记缓存一起清（库的 CurveCacheRendererBase.ClearAll 里就有 m_Arrows.Clear()），
            // 所以每帧重喂不会越攒越多
            CurveRenderer.ClearAll();
            m_PathRenderer = "curve";
            BuildArrows(space);
            CurveRenderer.RebuildAll();
        }

        private void BuildArrows(ChartToolSpace space)
        {
            m_ArrowsBuilt = 0;
            m_CurveEntries = 0;
            m_Markers = 0;
            m_DimensionsHave = 0;
            m_RecordsHave = 0;
            m_PointsSkipped = 0;
            m_PointsHidden = 0;
            m_DimensionsNeed = RequiredDimensionCount;
            m_HasDomains = false;

            var source = Data;
            if (source == null || !source.HasData) return;

            m_DimensionsHave = source.NumericDimensionCount;
            m_RecordsHave = source.RecordCount;
            // 形状不够就一支都不画（现象是"加了工具但什么都没显示"）——需求与实际几个数都留在报表里，
            // 面板也印同一对（规范 §25.1）
            if (m_DimensionsHave < CoordinateDimension + RequiredDimensionCount) return;
            if (m_RecordsHave < RequiredRecordCount) return;

            // 坐标得按各维的显示范围折进盒子（±1＝盒子边界）。折不出来就一支都不画，
            // 而不是拿原始值硬投影——后者是"计数全对、画面空无一物"的那条路（§28）
            m_HasDomains = TryResolveDomains();
            if (!m_HasDomains) return;

            // 一支箭头＝相邻两条记录 ⇒ 支数由纯算术算（离线钉着：记录不够 vs 维数不够要分得清）
            int limit = XChartVectorFieldMath.ArrowCount(DirectionSource, m_RecordsHave, MaxArrowCount);

            if (Continuous)
            {
                BuildContinuous(source, space, limit);
                return;
            }

            for (int i = 0; i < limit; i++)
            {
                Vector3 origin, second;
                if (!TryReadArrow(source, i, out origin, out second))
                {
                    m_PointsSkipped++;
                    continue;
                }
                // 读法在纯算术里：相邻两条·下一条是第二个点 ⇒ 作差；其余三档 ⇒ 第二量本身就是方向
                Vector3 tip = XChartVectorFieldMath.Tip(DirectionSource, origin, second, DirectionScale);
                AddShaft(space, origin, tip, ColorAt(source, FirstRecordOf(i)), ColorAt(source, SecondRecordOf(i)));
                m_ArrowsBuilt++;
            }
        }

        /// <summary>这支箭头的<b>第一条</b>记录号（起点、起点色都读它）</summary>
        private int FirstRecordOf(int arrow)
            => DirectionSource == XChartArrowDirectionSource.AlternatingRecords ? arrow * 2 : arrow;

        /// <summary>
        /// 这支箭头的<b>第二条</b>记录号（终点色读它）。"本条自带方向"那档没有第二条，就是它自己——
        /// 那一档两端同色是数据的说法，不是画错了。
        /// </summary>
        private int SecondRecordOf(int arrow)
        {
            switch (DirectionSource)
            {
                case XChartArrowDirectionSource.AlternatingRecords: return arrow * 2 + 1;
                case XChartArrowDirectionSource.ThisRecordCarriesVector: return arrow;
                default: return arrow + 1;
            }
        }

        /// <summary>
        /// 按"方向从哪来"读出一支箭头需要的两个量。相邻两条那两档读两条记录的坐标（都折进盒子）；
        /// 自带方向那档读同一条的 3~5 维、分段那档读奇数号记录的 0~2 维——
        /// <b>这两档的方向按跨度缩、不位移</b>（<c>delta / span</c>）：拿位置那套"减 min 再除"去折一个朝向，
        /// 就把"朝哪"变成了"在哪"；而完全不折又是另一个错——他 2026-10-26 看到的"一根比终点还大的矢量"
        /// 就是原始值(−93.5,10.1,49.0) 直接当立方体单位用的结果（约 23 个盒子单位 ≈ 2700 像素）。
        /// </summary>
        private bool TryReadArrow(IXChartDataSource source, int arrow, out Vector3 origin, out Vector3 second)
        {
            origin = Vector3.zero;
            second = Vector3.zero;
            int firstRecord = FirstRecordOf(arrow);
            int secondRecord = SecondRecordOf(arrow);
            if (firstRecord < 0 || secondRecord >= m_RecordsHave) return false;
            if (!TryReadFolded(source, firstRecord, 0, out origin)) return false;

            switch (DirectionSource)
            {
                case XChartArrowDirectionSource.ThisRecordCarriesVector:
                    return TryReadDelta(source, firstRecord, XChartArrowConfig.CoordinateDimensionCount, out second);
                case XChartArrowDirectionSource.AlternatingRecords:
                    return TryReadDelta(source, secondRecord, 0, out second);
                default:
                    return TryReadFolded(source, secondRecord, 0, out second);
            }
        }

        /// <summary>
        /// 连续绘制：途经点 → Catmull-Rom 贝塞尔链（曲线<b>真的经过每个数据点</b>——矢量场的点是数据本身，
        /// 用不经过控制点的自由贝塞尔等于把数据挪了位置），逐节提交成一条路径；
        /// 头挂在 <see cref="HeadPosition"/> 所在的那一节上（"位置 → 第几节 + 节内参数"由纯算术给）。
        /// 段与段之间是同一串坐标，所以"相对/绝对"在这里没有意义（配置里那一项也隐藏了）。
        /// </summary>
        private void BuildContinuous(IXChartDataSource source, ChartToolSpace space, int limit)
        {
            m_Pixels.Clear();
            m_Cubes.Clear();
            for (int i = 0; i <= limit; i++)
            {
                Vector3 cube;
                // 跳过的点要报出来：路径按"活下来的点"连，颜色仍按记录号取——数据里有非有限值时
                // 这两者会错开，不印出来的话就只能靠猜（skipped= 那一读数的全部意义）
                if (!TryReadFolded(source, i, 0, out cube)) { m_PointsSkipped++; continue; }
                m_Cubes.Add(cube);
                m_Pixels.Add(space.Project(cube, m_Composed));
            }

            int segments = m_Pixels.Count - 1;
            if (segments < 1) return;

            XChartArrowPathMath.ResolvePosition(segments, HeadPosition, out int headSegment, out float headT);

            for (int s = 0; s < segments; s++)
            {
                BuildSegmentBezier(s);
                bool withHead = s == headSegment;
                if (withHead)
                {
                    // 连续绘制没有"一支矢量"那个长度可折，就用<b>这一节自己</b>的投影比例
                    // （画了多少像素 ÷ 它在立方体单位里该有多少像素）：弯到背面去的那一节，头跟着一起缩
                    m_HeadScale = XChartVectorFieldMath.ProjectedScale(
                        Vector2.Distance(m_Pixels[s], m_Pixels[s + 1]),
                        Vector3.Distance(m_Cubes[s], m_Cubes[s + 1]), space.CubeToPixel);
                    m_Heads[0] = ToHeadData(XChartVectorFieldMath.Head(
                        HeadLength * m_HeadScale, HeadWidth * m_HeadScale, headT, MarkerAnchor,
                        ColorAt(source, Mathf.Min(s + 1, m_RecordsHave - 1))));
                    m_Markers++;
                    m_ArrowsBuilt = 1;      // 连续绘制是"一条路径 + 一支箭头"
                }
                AddEntry(m_Bezier, ColorAt(source, s), ColorAt(source, s + 1), withHead);
            }
        }

        /// <summary>段 s（点 s → 点 s+1）的贝塞尔控制点。端点之外重复端点：端点处切线指向段内。</summary>
        private void BuildSegmentBezier(int s)
        {
            int last = m_Pixels.Count - 1;
            XChartArrowPathMath.CatmullRomToBezier(
                m_Pixels[s > 0 ? s - 1 : 0], m_Pixels[s], m_Pixels[s + 1],
                m_Pixels[s + 2 <= last ? s + 2 : last], m_Bezier);
        }

        /// <summary>
        /// 一支箭头：一条<b>共线</b>贝塞尔（细分出来就是直线）＋末端一个头标记。
        /// 零向量不投一条零长曲线（库在零长输入上的细分没有承诺），改画一个圆点标记；
        /// 开关关掉就整条不画——<c>arrows=/skipped=</c> 说得出为什么少了一支。
        /// </summary>
        private void AddShaft(ChartToolSpace space, Vector3 originCube, Vector3 tipCube,
            Color startColor, Color endColor)
        {
            Vector2 from = space.Project(originCube, m_Composed);
            Vector2 to = space.Project(tipCube, m_Composed);

            // 头的尺寸吃"这条矢量自己的投影比例"：库的标记是<b>固定像素</b>的（库里那行
            // pos += tangent * arrow.width * 0.5f * depthCompensation），不乘它就等于
            // "转个身所有箭头还是同样长"（他 2026-10-26 的判决：圆周方向投到 2D 不可能都等长）
            m_HeadScale = XChartVectorFieldMath.ProjectedScale(Vector2.Distance(from, to),
                Vector3.Distance(originCube, tipCube), space.CubeToPixel);

            if ((to - from).sqrMagnitude < 1e-4f)
            {
                if (!ShowZeroVectorsAsDots) return;
                // 一像素的极短直线只为让条目存在，真正画出来的东西是这个圆点
                XChartArrowPathMath.Straight(from, from + new Vector2(1f, 0f), m_Bezier);
                m_Heads[0] = ToHeadData(XChartVectorFieldMath.Dot(
                    Mathf.Max(ShaftWidth, 1f), Mathf.Max(HeadWidth * m_HeadScale, 1f), endColor));
                AddEntry(m_Bezier, endColor, endColor, true);
                m_Markers++;
                return;
            }

            XChartArrowPathMath.Straight(from, to, m_Bezier);
            m_Heads[0] = ToHeadData(XChartVectorFieldMath.Head(
                HeadLength * m_HeadScale, HeadWidth * m_HeadScale, 1f, MarkerAnchor, endColor));
            AddEntry(m_Bezier, startColor, endColor, true);
            m_Markers++;
        }

        private void AddEntry(Vector2[] controlPoints, Color startColor, Color endColor, bool withHead)
        {
            for (int i = 0; i < m_Widths.Length; i++) m_Widths[i] = Mathf.Max(0.1f, ShaftWidth);
            var entry = new CurveCacheEntry
            {
                startColor = startColor,
                endColor = endColor,
                // 共线的贝塞尔吃多少节都是直的，所以杆只给最小的 4；路径那边走 PathSegments
                tessellationSegments = Continuous ? Mathf.Clamp(PathSegments, 2, 64) : 4,
            };
            CurveRenderer.AddCurve(entry, controlPoints, m_Widths, withHead ? m_Heads : null);
            m_CurveEntries++;
        }

        /// <summary>
        /// 纯算术的标记描述 → 库的 <see cref="ArrowHeadData"/>。<b>这一层只做对号入座</b>：
        /// 库的 <c>width</c> 是"沿切线"、<c>height</c> 是"垂直切线"，而我们那两个名字叫"头长/头宽"；
        /// <c>depthCompensation = 1</c> 让<b>头尖对准那个数据点</b>（矢量场的箭头指的就是那一点），
        /// 圆点用 0（中心对点）。形状、颜色、位置的算法与夹取都在纯算术里（离线钉着），这里不判据。
        /// </summary>
        private static ArrowHeadData ToHeadData(XChartArrowMarker marker)
        {
            return new ArrowHeadData
            {
                shape = marker.Shape == XChartMarkerShape.Circle ? ArrowShape.Circle : ArrowShape.Triangle,
                reversed = false,
                progress = marker.Progress,
                width = marker.AlongTangent,
                height = marker.AcrossTangent,
                depthCompensation = marker.TipAtPoint,
                color = marker.Color,
            };
        }

        /// <summary>
        /// 三条坐标轴各自的显示范围——<b>每帧现问上下文要</b>（覆盖优先，否则该维聚合；规则在
        /// <see cref="XChartDomainMath"/>）。范围不归元素存：元素只说要哪几维，自己再存一份就是
        /// "点和轴对不上"的来路（与散点同一条规矩，§16）。
        /// </summary>
        private bool TryResolveDomains()
        {
            m_MinX = m_MaxX = m_MinY = m_MaxY = m_MinZ = m_MaxZ = 0d;
            var context = Context;
            if (context == null) return false;
            return context.TryResolveDomain(CoordinateDimension, out m_MinX, out m_MaxX)
                && context.TryResolveDomain(CoordinateDimension + 1, out m_MinY, out m_MaxY)
                && context.TryResolveDomain(CoordinateDimension + 2, out m_MinZ, out m_MaxZ);
        }

        /// <summary>
        /// 一条记录的第 <c>offset~offset+2</c> 维（相对坐标起点）→ <b>立方体单位</b>的点：原始值先按各维
        /// 范围折进 [-1,1] 再投影（折法与"超范围怎么办"在纯算术里，离线钉着：<see cref="XChartCubeFoldMath"/>）。
        /// 折不出来（值非有限、或那一维跨度为 0）就返回 false，调用方计入 <c>skipped=</c>——
        /// <b>不是"静默少一支"，是"少几支当场说"</b>。
        /// </summary>
        private bool TryReadFolded(IXChartDataSource source, int record, int offset, out Vector3 cube)
        {
            cube = Vector3.zero;
            if (record < 0 || record >= m_RecordsHave) return false;
            int dim = CoordinateDimension + offset;
            double x = source.GetValueAt(record, dim);
            double y = source.GetValueAt(record, dim + 1);
            double z = source.GetValueAt(record, dim + 2);
            if (XChartCubeFoldMath.TryFoldCube(x, y, z,
                    m_MinX, m_MaxX, m_MinY, m_MaxY, m_MinZ, m_MaxZ, OutOfRange, out cube)) return true;
            // 分得清"数据不可用"与"按策略主动不画"：两者在画面上同形（都少一支），只有这一对数分得开
            if (IsBad(x) || IsBad(y) || IsBad(z)) m_PointsSkipped++; else m_PointsHidden++;
            cube = Vector3.zero;
            return false;
        }

        private static bool IsBad(double value)
            => double.IsNaN(value) || double.IsInfinity(value);

        /// <summary>
        /// 一条记录的三个<b>原始</b>值（方向用）→ <b>立方体单位的差</b>：只除以跨度、<b>不位移</b>
        /// （减 min 会把"朝哪"变成"在哪"），也不吃 <see cref="OutOfRange"/>——钳制与隐藏是坐标的事。
        /// 尺度在这里统一：一个数据跨度＝2 个立方体单位，最后由 <see cref="DirectionScale"/> 调长短。
        /// </summary>
        private bool TryReadDelta(IXChartDataSource source, int record, int offset, out Vector3 cubeDelta)
        {
            cubeDelta = Vector3.zero;
            if (record < 0 || record >= m_RecordsHave) return false;
            int dim = CoordinateDimension + offset;
            double x = source.GetValueAt(record, dim);
            double y = source.GetValueAt(record, dim + 1);
            double z = source.GetValueAt(record, dim + 2);
            if (XChartCubeFoldMath.TryFoldDelta(x, y, z,
                    m_MinX, m_MaxX, m_MinY, m_MaxY, m_MinZ, m_MaxZ, out cubeDelta)) return true;
            cubeDelta = Vector3.zero;
            // 三个数都好却折不出来＝这一档没有可用尺度（整列同一个数）；坏数是数据不可用——两件事分开数
            if (IsBad(x) || IsBad(y) || IsBad(z)) m_PointsSkipped++; else m_PointsHidden++;
            return false;
        }

        /// <summary>
        /// 这一条记录的颜色：没开颜色维就是单色；开了就按
        /// <c>[色相(0..256 色环), 饱和度(0..1), 明度(0..1), (不透明度 0..1)]</c> 读。
        /// <b>读不成（维不够或值不是有限数）就退回单色</b>——不是画成黑：黑在深色背景上等于没画，
        /// 而"退回了单色"这件事在报表的 <c>colors=</c> 读数里看得见。
        /// </summary>
        private Color ColorAt(IXChartDataSource source, int record)
        {
            if (!UseColorDimensions) return ArrowColor;
            if (source.NumericDimensionCount < ColorDimension + XChartArrowConfig.ColorDimensionCount) return ArrowColor;
            // 颜色维压到了坐标／方向那几列上（自带方向那档要 6 列，颜色起点默认还是 3）：
            // 那不是"一种颜色"，那是把方向当色相读——退回单色，并让 colors=overlap 说出来
            if (ColorDimension < XChartVectorFieldMath.RequiredCoordinateDimensions(DirectionSource))
                return ArrowColor;

            float hue = (float)source.GetValueAt(record, ColorDimension);
            float saturation = (float)source.GetValueAt(record, ColorDimension + 1);
            float value = (float)source.GetValueAt(record, ColorDimension + 2);
            float alpha = HasAlphaDimension
                ? (float)source.GetValueAt(record, ColorDimension + 3)
                : ArrowColor.a;

            Color color;
            return XChartVectorFieldMath.TryColor(hue, saturation, value, alpha, out color) ? color : ArrowColor;
        }

        protected override void OnDispose()
        {
            CurveRenderer = null;
            m_Pixels.Clear();
            m_Cubes.Clear();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 报表里点名这一帧：<b>维数（现有/需要）、记录数、折进盒子用的范围</b>＋算了几支、几个标记、
        /// 提交了几条曲线、曲线顶点数。"加了工具什么都没显示"这件事只有这一行分得清是
        /// "维数不够"、"记录不够"、"折不进盒子（dom=-）"还是"曲线渲染器不在位（path=none）"
        /// （他 2026-10-25 现场；规范 §25.1、§28、§29）。
        /// </summary>
        internal override void AppendDebugLine(System.Text.StringBuilder sb)
        {
            base.AppendDebugLine(sb);
            sb.Append(" dims=").Append(m_DimensionsHave).Append('/').Append(m_DimensionsNeed)
              .Append(" records=").Append(m_RecordsHave)
              // dom=- 就是"折不出来 ⇒ 一支都不画"；它和 arrows>0 不可能同时出现（那道门在算支数之前）
              .Append(" dom=").Append(m_HasDomains
                  ? "x[" + m_MinX.ToString("0.###") + ".." + m_MaxX.ToString("0.###") + "] y["
                    + m_MinY.ToString("0.###") + ".." + m_MaxY.ToString("0.###") + "] z["
                    + m_MinZ.ToString("0.###") + ".." + m_MaxZ.ToString("0.###") + "]"
                  : "-")
              .Append(" arrows=").Append(m_ArrowsBuilt)
              .Append(" skipped=").Append(m_PointsSkipped)
              .Append(" hidden=").Append(m_PointsHidden)
              .Append(" out=").Append(OutOfRange.ToString().ToLower())
              .Append(" src=").Append(Continuous ? "continuous" : DirectionSource.ToString().ToLower())
              .Append(" anchor=").Append(MarkerAnchor.ToString("0.###"))
              .Append(" headScale=").Append(m_HeadScale.ToString("0.###"))
              // colors=overlap：颜色维的起点压到了坐标／方向那几列上 ⇒ 实际画的是单色（不是"颜色没生效"，是配置撞了）
              .Append(" colors=").Append(!UseColorDimensions ? "single"
                  : (ColorDimension < XChartVectorFieldMath.RequiredCoordinateDimensions(DirectionSource) ? "overlap"
                     : (HasAlphaDimension ? "hsv+a" : "hsv")))
              .Append(" path=").Append(m_PathRenderer)
              .Append(" at=").Append(HeadPosition.ToString("0.###"))
              .Append(" curves=").Append(m_CurveEntries)
              .Append(" markers=").Append(m_Markers)
              .Append(" curveVerts=").Append(CurveRenderer != null ? CurveRenderer.LiveVertexCount : 0);
        }
#endif
    }
}
