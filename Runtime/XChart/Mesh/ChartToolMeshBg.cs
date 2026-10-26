using System;
using System.Collections.Generic;
using UnityEngine;
using XericLibrary.Runtime.UIGraph;
using XericUI.Core;
using XericUI.XChart.Axis;
using XericUI.XChart.Space;

namespace XericUI.XChart.Mesh
{
    /// <summary>
    /// 伪立方体背景元素：<b>填充面 + 面内网格</b>（线框棱由 <c>ChartToolCubeBg</c> 负责，两者可同用）。
    ///
    /// <para><b>画哪些面由掩码决定，不看可见性</b>：图表里的背景网格常画在背朝观察者的那几面
    /// （后墙 / 底面），所以 <see cref="FillFaces"/> 与 <see cref="GridFaces"/> 是两份独立的
    /// <see cref="XChartCubeFaceMask"/>，字母契约见该类型（a=前 b=后 c=上 d=下 e=左 f=右）。</para>
    ///
    /// <para><b>深度顺序</b>：填充按面心深度从远到近提交（画家算法），半透明叠色才正确；
    /// 网格线跟在填充之后（子节点顺序 = 绘制顺序）。</para>
    ///
    /// <para><b>空间来源</b>：本元素<b>不再自己算投影</b>，而是问图里的
    /// <see cref="ChartToolSpace"/> 要矩阵和面帧（<c>投影 = 基变换 ∘ 工具变换</c>）。
    /// 散点/轴线问的是同一个空间，所以点必然落在背景里——这是结构保证，不是两边凑数值。</para>
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.Chart + "/Mesh/Cube Mesh Background Tool")]
    public class ChartToolMeshBg : XChartToolBase
    {
        // ── 工具变换（在图的空间之上再叠一层） ────────────────────

        /// <summary>
        /// 背景自己的变换（立方体局部单位）。默认恒等——背景通常就占满图的空间。
        /// 想画一个"小一号/偏一角"的背景格子时才用它。
        /// </summary>
        public XChartToolTransform Transform { get; set; } = XChartToolTransform.Identity;

        // ── 填充 ──────────────────────────────────────────────────

        /// <summary>要填充哪些面</summary>
        public XChartCubeFaceMask FillFaces { get; set; } = XChartCubeFaceMask.None;

        /// <summary>填充色（面边缘）</summary>
        public Color FillColor { get; set; } = new Color(0.16f, 0.19f, 0.24f, 0.85f);

        /// <summary>填充面中心色；与 <see cref="FillColor"/> 不同时走径向渐变</summary>
        public Color FillCenterColor { get; set; } = new Color(0.16f, 0.19f, 0.24f, 0.85f);

        // ── 面内网格 ──────────────────────────────────────────────

        /// <summary>要画网格的面</summary>
        public XChartCubeFaceMask GridFaces { get; set; } = XChartCubeFaceMask.None;

        /// <summary>网格线颜色</summary>
        public Color GridColor { get; set; } = new Color(0.55f, 0.6f, 0.7f, 0.35f);

        /// <summary>网格线宽</summary>
        public float GridThickness { get; set; } = 1f;

        /// <summary>每个方向的网格分割数（统一值；S/T 各自给了正数时按各自的来）</summary>
        public int GridDivisions { get; set; } = 5;

        /// <summary>S 方向分格数。0＝跟随 <see cref="GridDivisions"/>。
        /// 等分时"沿 S 的线由 T 的分格数决定"（算式在 <see cref="XChartGridDivisions"/>，探针钉着）；
        /// 绑了维度时这个数是<b>那一族线的目标刻度条数</b>（不是分格数，规范 §22）。</summary>
        public int GridDivisionsS { get; set; }

        /// <summary>T 方向分格数。0＝跟随 <see cref="GridDivisions"/></summary>
        public int GridDivisionsT { get; set; }

        /// <summary>是否同时描出面轮廓（没有线框元素时也能看清面的边界）</summary>
        public bool ShowFaceOutline { get; set; } = true;

        /// <summary>
        /// 只画朝向观察者的面。默认 false——背景网格本就常画在背朝观察者的那几面（后墙/底面）；
        /// 想要"实体盒子"的观感时打开它。
        /// </summary>
        public bool OnlyVisibleFaces { get; set; }

        /// <summary>网格与轮廓的颜色与线宽（轮廓更实一点）</summary>
        public Color OutlineColor { get; set; } = new Color(0.7f, 0.75f, 0.85f, 0.6f);

        // ── 内部组件 ──────────────────────────────────────────────

        /// <summary>面填充渲染器</summary>
        public XChartMeshFillRenderer FillRenderer { get; private set; }

        /// <summary>面内网格渲染器</summary>
        public UILineRendererV2 GridRenderer { get; private set; }

        // ── 投影状态（权威在 ChartToolSpace，这里只缓存复合结果） ──

        private readonly XChartFaceFrame[] m_FaceFrames = new XChartFaceFrame[XChartCubeGeometry.FaceCount];
        private readonly List<FaceSortEntry> m_SortedFaces = new List<FaceSortEntry>(XChartCubeGeometry.FaceCount);

        private struct FaceSortEntry
        {
            public int Face;
            public float Depth;
        }

        private static readonly Comparison<FaceSortEntry> s_FarToNear =
            (left, right) => left.Depth.CompareTo(right.Depth);

        private ChartToolSpace m_Space;
        private Matrix4x4 m_Composed = Matrix4x4.identity;
        private int m_LastSpaceVersion = -1;
        /// <summary>上一次发布格线时数据模型的版本号：绑了维度的格线要跟着数据走，
        /// 而数据变化不会动空间的投影版本，所以这个号要单独判（否则"数据变了格线还停在老位置"）</summary>
        private long m_LastDataVersion = -1;
        private bool m_Dirty = true;
        /// <summary>发布插槽用的复用缓冲（一个面的四角 = 12 个值）</summary>
        private readonly float[] m_SlotValues = new float[12];

        // 本帧的自述读数：画了几条线 / 发了几条线 / 提交了几块几面 / 面帧有效几个
        private int m_LinesS;
        private int m_LinesT;
        private int m_GridLinesPublished;
        private int m_GridFacesPublished;
        private int m_GridSlotsRetracted;
        private int m_GridLinesDrawn;
        private int m_GridFacesDrawn;
        private int m_FacesOutlined;
        private int m_FacesFilled;

        /// <summary>格线插槽的值暂存：<b>两个方向各一个</b>，长度按各自的条数精确相等（板子按数组长度算组数）</summary>
        private float[] m_GridValuesS;
        private float[] m_GridValuesT;

        /// <summary>这一方向各条线的面参数比例（等分或数据刻度），复用不分配</summary>
        private float[] m_RatioScratchS;
        private float[] m_RatioScratchT;
        private readonly List<double> m_TickScratch = new List<double>();

        /// <summary>沿 S 的线绑哪一维（&lt;0 ＝ 不绑，等分）</summary>
        public int GridDimensionS { get; set; } = -1;

        /// <summary>沿 T 的线绑哪一维（&lt;0 ＝ 不绑，等分）</summary>
        public int GridDimensionT { get; set; } = -1;

        /// <summary>本元素的变换变了（或想强制重取空间）时调用</summary>
        public override void MarkDirty() => m_Dirty = true;

        /// <summary>本工具定义网格（发布面/格线插槽、算范围）⇒ 归定义组、最先跑（规范 §11.4）；
        /// 它"画在底下"这件事由绘制序（默认最小）表达，与分组无关</summary>
        public override XChartToolCategory Category => XChartToolCategory.Definition;

        /// <summary>取某个面的面帧（图表局部像素）</summary>
        public XChartFaceFrame GetFaceFrame(XChartCubeGeometry.CubeFace face)
        {
            UpdateProjection();
            return m_FaceFrames[(int)face];
        }

        /// <summary>
        /// 空间版本没变、自己的变换也没变，就一次矩阵乘法都不做。
        /// 空间是渲染时现找的（不是初始化时塞的），所以注册先后顺序不影响正确性。
        /// </summary>
        private void UpdateProjection()
        {
            m_Space = Context?.Space;
            if (m_Space == null) return;

            // 数据版本也要判：格线绑了维度时"线在哪"是数据的函数，而写数据不会动空间的投影版本
            var data = Context != null ? Context.Data : null;
            long dataVersion = data != null ? data.Version : -1;
            if (!m_Dirty && m_Space.ProjectionVersion == m_LastSpaceVersion && dataVersion == m_LastDataVersion) return;
            m_Dirty = false;
            m_LastSpaceVersion = m_Space.ProjectionVersion;
            m_LastDataVersion = dataVersion;

            m_Composed = m_Space.Compose(Transform);
            m_Space.BuildFaceFrames(m_FaceFrames, m_Composed);
            SortFacesByDepth();
            PublishFaceSlots();
        }

        /// <summary>
        /// 把六个面发布成 `Quad3D` 插槽（规范 §11.2）：<b>本工具是定义网格的那一个，其余工具按 id 取用</b>。
        /// 发布的是**单位立方体的四角**（不是像素）：消费者用自己的矩阵投影，于是
        /// "谁投影"仍只有一条路，而"哪个面在哪儿"由定义者说了算——换成异形网格只需改这里。
        /// </summary>
        private void PublishFaceSlots()
        {
            IXChartSlotBoard board = Context?.Slots;
            if (board == null) return;

            for (int i = 0; i < XChartCubeGeometry.FaceCount; i++)
            {
                var face = (XChartCubeGeometry.CubeFace)i;
                Vector3 c00, c01, c11, c10;
                XChartCubeGeometry.GetFaceCorners(face, out c00, out c01, out c11, out c10);

                m_SlotValues[0] = c00.x; m_SlotValues[1] = c00.y; m_SlotValues[2] = c00.z;
                m_SlotValues[3] = c01.x; m_SlotValues[4] = c01.y; m_SlotValues[5] = c01.z;
                m_SlotValues[6] = c11.x; m_SlotValues[7] = c11.y; m_SlotValues[8] = c11.z;
                m_SlotValues[9] = c10.x; m_SlotValues[10] = c10.y; m_SlotValues[11] = c10.z;

                board.Publish(XChartCubeGeometry.FaceSlotId(face), XChartSlotMode.Quad3D,
                    XChartSlotAccess.ReadOnly, m_SlotValues, out string _);
            }

            PublishGridSlots(board);
        }

        /// <summary>
        /// 把每个面的**格线**发布成 <c>Line3D</c> 插槽（规范 §11.2 / P9）：沿 S 的一组与
        /// 沿 T 的一组。发布的是<b>单位立方体端点</b>，与 <c>mesh.face.&lt;n&gt;</c> 的四角同一口径
        /// ⇒ 谁投影仍只有一条路。
        ///
        /// <para><b>线位置由谁定</b>：绑了维度（<see cref="GridDimensionS"/>/<see cref="GridDimensionT"/> ≥ 0）
        /// 就用<b>该维的数据范围</b>算刻度位置（<see cref="XChartGridLines"/>，与轴线刻度同一套 nice step）；
        /// 没绑就等分（比例 <c>i/(count-1)</c>，与旧行为逐位相同）——"网格线的定义权在网格、
        /// 轴线按发布的线跟随"这句话就落在这里。</para>
        ///
        /// <para>缓冲必须<b>长度精确</b>：插槽板按数组长度算组数，开大了会把残留当成额外的线发出去。</para>
        /// </summary>
        private void PublishGridSlots(IXChartSlotBoard board)
        {
            // 条数只算一次（两个方向各一份），缓冲按两者最大值开；比例数组算完就留着给绘制侧用，
            // 于是"画上哪几条线"与"发布给轴线哪几条线"不可能各说各话（规范 §22）
            m_LinesS = XChartGridDivisions.LinesAlongS(GridDivisionsS, GridDivisionsT, GridDivisions);
            m_LinesT = XChartGridDivisions.LinesAlongT(GridDivisionsS, GridDivisionsT, GridDivisions);
            EnsureRatioBuffers(Mathf.Max(m_LinesS, m_LinesT));
            m_LinesS = BuildGridRatios(true, m_RatioScratchS);
            m_LinesT = BuildGridRatios(false, m_RatioScratchT);

            m_GridLinesPublished = 0;
            m_GridFacesPublished = 0;
            m_GridSlotsRetracted = 0;
            for (int i = 0; i < XChartCubeGeometry.FaceCount; i++)
            {
                var face = (XChartCubeGeometry.CubeFace)i;
                if (DrawsGridOnFace(face))
                {
                    m_GridFacesPublished++;
                    m_GridLinesPublished += PublishGridSlot(board, face, true, m_RatioScratchS, m_LinesS);
                    m_GridLinesPublished += PublishGridSlot(board, face, false, m_RatioScratchT, m_LinesT);
                    continue;
                }

                // 不画的那个面得把上一轮发出去的那批线**撤下来**：插槽说的是"这里有线可吸"，
                // 只发不收就等于留一批画面上并不存在的线给轴线去吸（规范 §22）
                m_GridSlotsRetracted += RetractGridSlot(board, face, true);
                m_GridSlotsRetracted += RetractGridSlot(board, face, false);
            }
        }

        private static int RetractGridSlot(IXChartSlotBoard board, XChartCubeGeometry.CubeFace face, bool alongS)
            => board.Unpublish(XChartCubeGeometry.GridSlotId(face, alongS)) ? 1 : 0;

        /// <summary>
        /// 这个面的格线<b>这一帧到底会不会被画出来</b>——发布侧与绘制侧共用这同一个判据。
        ///
        /// <para>插槽的语义是"这里有格线，可以吸附"。把掩码筛掉的面、背朝观察者的面（开了"只画可见面"时）、
        /// 面帧退化的面也发出去，轴线就会去吸一条画面上一条都不存在的线——那正是本轮要消灭的
        /// "关掉一边、另一边还跟着"的形状，只是换到了插槽这一层（规范 §22）。
        /// 两侧各写一遍过滤迟早会飘，所以只写一次。</para>
        /// </summary>
        private bool DrawsGridOnFace(XChartCubeGeometry.CubeFace face)
        {
            if (!XChartCubeFaceMaskUtility.HasFace(GridFaces, face)) return false;
            if (!m_FaceFrames[(int)face].IsValid) return false;
            if (OnlyVisibleFaces && m_Space != null && !m_Space.IsFaceVisible(face, m_Composed)) return false;
            return true;
        }

        private int PublishGridSlot(IXChartSlotBoard board, XChartCubeGeometry.CubeFace face,
            bool alongS, float[] ratios, int lines)
        {
            int needed = XChartCubeGeometry.GridSlotValueCount(lines);
            if (needed <= 0) return 0;

            float[] buffer = alongS ? m_GridValuesS : m_GridValuesT;
            if (buffer == null || buffer.Length != needed)
            {
                buffer = new float[needed];
                if (alongS) m_GridValuesS = buffer;
                else m_GridValuesT = buffer;
            }

            if (XChartCubeGeometry.FillGridLineValues(face, alongS, ratios, lines, buffer) <= 0) return 0;
            board.Publish(XChartCubeGeometry.GridSlotId(face, alongS), XChartSlotMode.Line3D,
                XChartSlotAccess.ReadOnly, buffer, out string _);
            return lines;
        }

        /// <summary>
        /// 这一方向的线位置：绑了维度就用数据范围算刻度（与轴线同源），否则等分。
        /// <b>那一维没有数据时退回等分而不是不画</b>——网格整片消失比"线回到等分"难查得多。
        /// </summary>
        private int BuildGridRatios(bool alongS, float[] ratios)
        {
            // 等分那一档的条数：算式只在 XChartGridDivisions 里（"沿 S 的线由 T 的分格数决定"，探针钉着）
            int lines = alongS
                ? XChartGridDivisions.LinesAlongS(GridDivisionsS, GridDivisionsT, GridDivisions)
                : XChartGridDivisions.LinesAlongT(GridDivisionsS, GridDivisionsT, GridDivisions);
            int dimension = alongS ? GridDimensionS : GridDimensionT;
            var context = Context;

            if (dimension < 0 || context == null)
                return XChartGridLines.BuildUniformRatios(lines, ratios);

            double min, max;
            if (!context.TryResolveDomain(dimension, out min, out max))
                return XChartGridLines.BuildUniformRatios(lines, ratios);

            // 绑了维度：条数目标＝该方向自己那个分格数（0＝统一值），刻度由数据范围定
            int target = Mathf.Max(2, alongS
                ? XChartGridDivisions.Resolve(GridDivisionsS, GridDivisions)
                : XChartGridDivisions.Resolve(GridDivisionsT, GridDivisions));
            return XChartGridLines.BuildRatios(min, max, XChartAxisScale.Linear, 0.0,
                target, 0.0, true, ratios, m_TickScratch);
        }

        private void EnsureRatioBuffers(int capacity)
        {
            if (m_RatioScratchS == null || m_RatioScratchS.Length < capacity) m_RatioScratchS = new float[capacity];
            if (m_RatioScratchT == null || m_RatioScratchT.Length < capacity) m_RatioScratchT = new float[capacity];
        }

        // ── 初始化 ────────────────────────────────────────────────

        protected override void OnInitialize()
        {
            // 先建填充、后建网格：兄弟顺序就是绘制顺序，网格要在填充之上
            FillRenderer = CreateChildComponent<XChartMeshFillRenderer>("CubeFaceFill");
            if (FillRenderer != null) Stretch(FillRenderer.rectTransform);

            GridRenderer = CreateChildComponent<UILineRendererV2>("CubeFaceGrid");
            if (GridRenderer != null)
            {
                GridRenderer.defaultColor = GridColor;
                GridRenderer.defaultThickness = GridThickness;
                Stretch(GridRenderer.rectTransform);
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

        // ── 画家序 ────────────────────────────────────────────────

        /// <summary>按面心深度从远到近排序（画家算法：远的先提交）</summary>
        private void SortFacesByDepth()
        {
            m_SortedFaces.Clear();
            for (int i = 0; i < XChartCubeGeometry.FaceCount; i++)
            {
                m_SortedFaces.Add(new FaceSortEntry
                {
                    Face = i,
                    Depth = XChartCubeGeometry.GetFaceDepth(m_Composed, (XChartCubeGeometry.CubeFace)i),
                });
            }
            m_SortedFaces.Sort(s_FarToNear);
        }

        // ── 渲染 ──────────────────────────────────────────────────

        public override void OnRender()
        {
            base.OnRender();

            UpdateProjection();

            DrawFill();
            DrawGrid();
        }

        private void DrawFill()
        {
            m_FacesFilled = 0;
            if (FillRenderer == null) return;

            FillRenderer.ClearFaces();
            if (FillFaces != XChartCubeFaceMask.None)
            {
                for (int i = 0; i < m_SortedFaces.Count; i++)
                {
                    var face = (XChartCubeGeometry.CubeFace)m_SortedFaces[i].Face;
                    if (!XChartCubeFaceMaskUtility.HasFace(FillFaces, face)) continue;
                    if (OnlyVisibleFaces && !m_Space.IsFaceVisible(face, m_Composed)) continue;

                    XChartFaceFrame frame = m_FaceFrames[(int)face];
                    if (!frame.IsValid) continue;

                    FillRenderer.AddFace(frame.CornerST00, frame.CornerST01, frame.CornerST11, frame.CornerST10,
                        FillColor, FillCenterColor);
                    m_FacesFilled++;
                }
            }
            FillRenderer.Apply();
        }

        private void DrawGrid()
        {
            m_GridLinesDrawn = 0;
            m_GridFacesDrawn = 0;
            m_FacesOutlined = 0;
            if (GridRenderer == null) return;

            GridRenderer.ClearAll();
            if (GridFaces == XChartCubeFaceMask.None) return;

            // 格线的位置**不在这里再算一遍**：用发布那一侧留下的同一份比例数组（PublishGridSlots 里填的），
            // 于是"画上的线"与"轴线能吸附到的线"永远是同一批。旧版这里自己按 divisions 等分，
            // 发布侧按刻度算，两条路各画一套（规范 §22）
            for (int i = 0; i < m_SortedFaces.Count; i++)
            {
                var face = (XChartCubeGeometry.CubeFace)m_SortedFaces[i].Face;
                // 与发布侧同一个判据：哪些面有格线，只许有一处说了算（DrawsGridOnFace）
                if (!DrawsGridOnFace(face)) continue;

                XChartFaceFrame frame = m_FaceFrames[(int)face];
                m_GridFacesDrawn++;

                if (ShowFaceOutline)
                {
                    frame.GetLineAlongS(0f, out Vector2 bottomFrom, out Vector2 bottomTo);
                    frame.GetLineAlongS(1f, out Vector2 topFrom, out Vector2 topTo);
                    frame.GetLineAlongT(0f, out Vector2 leftFrom, out Vector2 leftTo);
                    frame.GetLineAlongT(1f, out Vector2 rightFrom, out Vector2 rightTo);

                    GridRenderer.DrawLine(bottomFrom, bottomTo, OutlineColor, GridThickness);
                    GridRenderer.DrawLine(topFrom, topTo, OutlineColor, GridThickness);
                    GridRenderer.DrawLine(leftFrom, leftTo, OutlineColor, GridThickness);
                    GridRenderer.DrawLine(rightFrom, rightTo, OutlineColor, GridThickness);
                    m_GridLinesDrawn += 4;
                    m_FacesOutlined++;
                }

                for (int d = 0; d < m_LinesS; d++)
                {
                    frame.GetLineAlongS(m_RatioScratchS[d], out Vector2 from, out Vector2 to);
                    GridRenderer.DrawLine(from, to, GridColor, GridThickness);
                    m_GridLinesDrawn++;
                }

                for (int d = 0; d < m_LinesT; d++)
                {
                    frame.GetLineAlongT(m_RatioScratchT[d], out Vector2 from, out Vector2 to);
                    GridRenderer.DrawLine(from, to, GridColor, GridThickness);
                    m_GridLinesDrawn++;
                }
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 这一帧的自述：<c>fillFaces/gridFaces</c> 是掩码字母（面板上改的数有没有真落到工具上），
        /// <c>filled=</c> 提交了几面填充，<c>faces=画上/发出</c> 与 <c>linesCheck=</c> 是本元素的**自证**：
        /// "画上哪几条线"和"发布给轴线哪几条线"必须同一批（差值只许来自描轮廓的 4 条／面）。
        /// <b>判决由被测进程自己印</b>——"应当相等"写在文档里没人查就等于没写（规范 §22）。
        /// 他报的"填充面改了没反应"这类症状，也只有这几个数能分清是"没提交"还是"提交了看不见"。
        /// </summary>
        internal override void AppendDebugLine(System.Text.StringBuilder sb)
        {
            base.AppendDebugLine(sb);
            int valid = 0;
            for (int i = 0; i < m_FaceFrames.Length; i++)
                if (m_FaceFrames[i].IsValid) valid++;
            // 轮廓那 4 条只画不发（插槽里发的是格线，不含边框），所以期望值要把它加回去
            int want = m_GridLinesPublished + m_FacesOutlined * 4;
            sb.Append(" fillFaces=").Append(XChartCubeFaceMaskUtility.Describe(FillFaces))
              .Append(" gridFaces=").Append(XChartCubeFaceMaskUtility.Describe(GridFaces))
              .Append(" filled=").Append(m_FacesFilled)
              .Append(" gridDrawn=").Append(m_GridLinesDrawn)
              .Append(" gridPublished=").Append(m_GridLinesPublished)
              .Append(" faces=").Append(m_GridFacesDrawn).Append('/').Append(m_GridFacesPublished)
              .Append(" retracted=").Append(m_GridSlotsRetracted)
              .Append(" linesCheck=").Append(m_GridLinesDrawn == want ? "ok"
                  : "DIFF drawn=" + m_GridLinesDrawn + " want=" + want)
              .Append(" lines=").Append(m_LinesS).Append('/').Append(m_LinesT)
              .Append(" frames=").Append(valid).Append('/').Append(m_FaceFrames.Length)
              .Append(" chunks=").Append(FillRenderer != null ? FillRenderer.ChunkCount : -1)
              .Append(" renderers=").Append(FillRenderer == null ? "fill-NULL" : "ok")
              .Append('/').Append(GridRenderer == null ? "grid-NULL" : "ok");
        }
#endif

        // ── 清理 ──────────────────────────────────────────────────

        protected override void OnDispose()
        {
            // 元素是格线的唯一作者 ⇒ 元素没了，它发过的格线插槽必须跟着没（否则轴线还在吸一批
            // 已经没有任何东西在画的线，规范 §22）。这里**只撤自己发的**：`mesh.face.<n>` 那批四角
            // 是立方体本身的几何，线框元素也在发，动它就是拆别人定义的东西。
            var board = Context?.Slots;
            if (board != null)
            {
                for (int i = 0; i < XChartCubeGeometry.FaceCount; i++)
                {
                    var face = (XChartCubeGeometry.CubeFace)i;
                    board.Unpublish(XChartCubeGeometry.GridSlotId(face, true));
                    board.Unpublish(XChartCubeGeometry.GridSlotId(face, false));
                }
            }

            FillRenderer = null;
            GridRenderer = null;
            m_SortedFaces.Clear();
        }
    }
}