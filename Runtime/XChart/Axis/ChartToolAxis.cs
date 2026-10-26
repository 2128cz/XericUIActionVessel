using System.Collections.Generic;
using TMPro;
using UnityEngine;
using XericLibrary.Runtime.UIGraph;
using XericUI.Core;
using XericUI.XChart.Mesh;
using XericUI.XChart.Space;

namespace XericUI.XChart.Axis
{
    /// <summary>轴在宿主面上沿哪个面参数方向走</summary>
    public enum XChartAxisDirection { AlongS = 0, AlongT = 1 }

    /// <summary>刻度标签的内容来源</summary>
    public enum XChartAxisLabelSource
    {
        /// <summary>数据直驱：每个主刻度写它的数值（格式串可覆盖，留空走自适应）</summary>
        TickValue = 0,
        /// <summary>维度标签：只写一次该维的显示名，挂在轴末端</summary>
        DimensionLabel = 1,
        /// <summary>自定义：用 <see cref="ChartToolAxis.CustomLabels"/> 按刻度顺序填</summary>
        Custom = 2,
    }

    /// <summary>
    /// 轴线元素：在立方体某个面的参数域里画一条轴 + 主/次刻度 + TMP 标签。
    ///
    /// <para><b>它完全住在面帧里</b>：宿主面（<see cref="Face"/>）+ 面内方向（<see cref="Direction"/>）
    /// + 该方向上的起止比例（<see cref="StartRatio"/>..
    /// <see cref="EndRatio"/>）+ 垂直方向上的位置（<see cref="CrossRatio"/>）。
    /// 这些参数经 <see cref="XChartFaceFrame"/> 变成像素，所以轴和背景/散点天然共形变——
    /// 转立方体、换视角都不用重新对坐标。</para>
    ///
    /// <para><b>空间类型</b>：<see cref="Scale"/> 决定"值 ↔ 轴上参数"的映射（线性 / 对数 / 指数），
    /// 刻度值由 <see cref="XChartAxisMath"/> 生成（对数按数量级铺 1/2/5，线性/指数按 nice-step 铺）。
    /// 次刻度在<b>参数</b>域里等分，所以在对数/指数轴上分出来的是几何级数。</para>
    ///
    /// <para><b>标签三源</b>：<see cref="XChartAxisLabelSource"/> —— 数值直驱（带格式串入口）、
    /// 维度显示名、自定义字符串数组。</para>
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.Chart + "/Axis/Axis Tool")]
    public sealed class ChartToolAxis : XChartToolBase
    {
        // ── 宿主与走向 ────────────────────────────────────────────

        /// <summary>
        /// 宿主面用**插槽 id** 指定（规范 §11.2）：默认取定义网格发布的 <c>mesh.face.&lt;面号&gt;</c>。
        /// 不再认"第几个面"——拿到的是点，所以异形网格只要发布同 id 的插槽就能用。
        /// </summary>
        public string SlotId { get; set; } = XChartCubeGeometry.FaceSlotId(XChartCubeGeometry.CubeFace.Bottom);

        /// <summary>
        /// 对齐格线（P9）：开启后每条主刻度的位置会<b>吸附到网格发布的那组线上</b>
        /// （轴沿 S 走读 <c>grid.T</c>、沿 T 走读 <c>grid.S</c>），偏移超过"相邻两条线间距的一半"
        /// 就不吸。网格是定义方，这里只是跟随。
        /// </summary>
        public bool AlignToGrid { get; set; }
        public XChartAxisDirection Direction { get; set; } = XChartAxisDirection.AlongS;
        public float StartRatio { get; set; }
        public float EndRatio { get; set; } = 1f;
        public float CrossRatio { get; set; }
        public XChartToolTransform Transform { get; set; } = XChartToolTransform.Identity;

        // ── 值域与刻度 ────────────────────────────────────────────

        /// <summary>
        /// 轴读哪一维——这是<b>绑定</b>（这条轴画的是哪一路数据），不是范围。
        /// 范围与"这一维现在有没有数据"归 chart 的数据板块（每个维度一行），
        /// 工具只呈现（规范 §1）。
        /// </summary>
        public int Dimension { get; set; }

        public XChartAxisScale Scale { get; set; } = XChartAxisScale.Linear;
        public double ExponentialStrength { get; set; } = 2.0;
        public bool AutoStep { get; set; } = true;
        public double ManualStep { get; set; } = 1.0;
        public int TargetMajorCount { get; set; } = 5;
        public int MinorTickCount { get; set; } = 4;

        // ── 外观 ──────────────────────────────────────────────────

        public Color LineColor { get; set; } = new Color(0.85f, 0.88f, 0.95f, 0.9f);
        public float LineThickness { get; set; } = 2f;
        public float MajorTickLength { get; set; } = 8f;
        public float MinorTickLength { get; set; } = 4f;
        public float TickThickness { get; set; } = 1.5f;
        /// <summary>刻度朝哪边：+1 朝面参数增大方向，-1 朝减小方向</summary>
        public float TickSide { get; set; } = -1f;

        // ── 标签 ──────────────────────────────────────────────────

        public XChartAxisLabelSource LabelSource { get; set; } = XChartAxisLabelSource.TickValue;
        /// <summary>数值格式化串（如 "0.00" 或 "0.#"）；留空走自适应</summary>
        public string LabelFormat { get; set; }
        public string[] CustomLabels { get; set; }
        public float LabelGap { get; set; } = 6f;
        public float LabelFontSize { get; set; } = 14f;
        public Color LabelColor { get; set; } = new Color(0.85f, 0.88f, 0.95f, 0.95f);
        public Vector2 LabelSize { get; set; } = new Vector2(90f, 24f);
        /// <summary>标签跟着轴的方向转（斜面上读起来才不拧着）</summary>
        public bool RotateLabelsWithAxis { get; set; } = true;

        // ── 内部 ──────────────────────────────────────────────────

        public UILineRendererV2 LineRenderer { get; private set; }
        private readonly List<double> m_Majors = new List<double>();
        private readonly List<double> m_Minors = new List<double>();
        private readonly XChartFaceFrame[] m_FaceFrames = new XChartFaceFrame[XChartCubeGeometry.FaceCount];

        private Matrix4x4 m_Composed = Matrix4x4.identity;
        private int m_LastSpaceVersion = -1;
        /// <summary>这一帧的面帧是从插槽拿的还是自己从立方几何推的（报表用）</summary>
        private string m_FrameSource = "cube";

        /// <summary>对齐格线的读数（报表用）：off / grid(n/m, snapMax=…) / no-slot / unknown-face</summary>
        private string m_AlignNote = "off";
        private int m_AlignSnapped;
        private int m_AlignTotal;
        private float m_AlignMaxSnap;
        private float[] m_GridRatios = new float[64];

        /// <summary>
        /// 把刻度<b>吸附到网格发布的线上</b>（P9 的"跟随"那一半）：网格是定义方，这里只读它的线。
        /// 轴沿 S 走 ⇒ 刻度落在<b>等 s 线</b>上，而等 s 线发布在 <c>grid.T</c>；沿 T 走读 <c>grid.S</c>。
        /// 取不到槽（没网格元素 / 网格没发布 / 手填的 id 不是 <c>mesh.face.&lt;n&gt;</c>）就不吸，
        /// 并把原因记进 <see cref="m_AlignNote"/>——"没吸上"和"没开对齐"在画面上一样，只有读数分得清。
        /// </summary>
        private bool TrySnapParameterToGrid(bool alongS, float parameter, out float snapped)
        {
            snapped = parameter;
            var board = Context?.Slots;
            if (board == null) { m_AlignNote = "no-board"; return false; }

            int faceIndex = FaceIndexFromSlotId(SlotId);
            if (faceIndex < 0 || faceIndex >= XChartCubeGeometry.FaceCount) { m_AlignNote = "unknown-face"; return false; }

            var face = (XChartCubeGeometry.CubeFace)faceIndex;
            XChartSlotView view;
            if (!board.TryRead(SlotId + (alongS ? ".grid.T" : ".grid.S"), out view)) { m_AlignNote = "no-slot"; return false; }

            int count = Mathf.Min(view.GroupCount, m_GridRatios.Length);
            int written = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 from, to;
                if (!view.TryGetLine(i, out from, out to)) continue;

                // 线是"等某个参数"的一条：沿 S 走的那组（grid.T）等的是 s，沿线两个端点的 s 相同；
                // grid.S 那组等的是 t。取第一个端点反解即可（面基是单位正交的）。
                XChartCubeGeometry.GetFaceParameter(face, from, out float lineS, out float lineT);
                m_GridRatios[written++] = alongS ? lineS : lineT;
            }

            if (written == 0) { m_AlignNote = "no-lines"; return false; }

            // 吸附上限 = 相邻两条线最小间距的一半：宁可不对齐，也不把刻度扭到老远的地方
            float smallest = float.MaxValue;
            for (int i = 1; i < written; i++)
            {
                float gap = Mathf.Abs(m_GridRatios[i] - m_GridRatios[i - 1]);
                if (gap > 1e-6f && gap < smallest) smallest = gap;
            }
            float maxDistance = smallest < float.MaxValue ? smallest * 0.5f : 1f;

            bool snappedOk = XChartGridLines.TrySnapToGrid(m_GridRatios, written, parameter, maxDistance, out snapped);
            m_AlignTotal++;
            if (snappedOk)
            {
                m_AlignSnapped++;
                m_AlignMaxSnap = Mathf.Max(m_AlignMaxSnap, Mathf.Abs(snapped - parameter));
            }
            m_AlignNote = "grid(" + m_AlignSnapped + "/" + m_AlignTotal + ", snapMax=" + m_AlignMaxSnap.ToString("0.####") + ")";
            return snappedOk;
        }
        private long m_LastDataVersion = -1;

        // 两段脏：刻度要等一次池调度才发落到位，所以"算需求"和"落笔"分开判脏
        private bool m_RangeDirty = true;
        private bool m_GeometryDirty = true;
        private bool m_HasRange;
        private double m_RangeMin = 0.0;
        private double m_RangeMax = 1.0;
        private double m_RangeStep = 1.0;

        public override void MarkDirty()
        {
            m_RangeDirty = true;
            m_GeometryDirty = true;
        }

        protected override void OnInitialize()
        {
            LineRenderer = CreateChildComponent<UILineRendererV2>("AxisLines");
            if (LineRenderer != null)
            {
                LineRenderer.defaultColor = LineColor;
                LineRenderer.defaultThickness = LineThickness;
                Stretch(LineRenderer.rectTransform);
            }

            MarkDirty();
            SetVisible(true);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 数据侧：算范围、铺刻度、<b>申请这一帧要的标签数</b>（申请只置脏，件在随后的池调度里发落）。
        /// 放在这里而不是 OnRender，是为了让"申请→发落→落笔"在同一片帧内闭合。
        /// </summary>
        public override void OnUpdate()
        {
            base.OnUpdate();
            if (Context == null) return;

            long dataVersion = Data != null ? Data.Version : -1;
            // 还欠着槽位时也重新走一遍：件是下一轮调度才补齐的，别把补齐的机会等成"数据变了才有"
            if (!m_RangeDirty && dataVersion == m_LastDataVersion && !TextSlotsPending) return;
            m_RangeDirty = false;
            m_LastDataVersion = dataVersion;
            m_GeometryDirty = true;

            m_Majors.Clear();
            double min, max, step;
            if (!ResolveRange(out min, out max, out step))
            {
                m_HasRange = false;
                RequestTextSlots(0);
                return;
            }

            m_HasRange = true;
            m_RangeMin = min;
            m_RangeMax = max;
            m_RangeStep = step;
            XChartAxisMath.BuildTicks(min, max, Scale, ManualStep, TargetMajorCount, AutoStep, m_Majors);
            RequestTextSlots(LabelSource == XChartAxisLabelSource.DimensionLabel ? 1 : m_Majors.Count);
        }

        /// <summary>
        /// 面帧从**定义网格发布的插槽**里取（规范 §11.2）：网格是定义者，其余工具跟随——
        /// 消费者拿到的是四个点，不是"第几个面"，所以换成异形网格也不用改这里。
        /// 取不到就回落到老路径（自己从立方几何推），并把**走的是哪条**印进报表（`frame=`）：
        /// 两条路画出来一样时更要印，否则"跟随网格"这句话没有生产者。
        /// </summary>
        private XChartFaceFrame ResolveFaceFrame(ChartToolSpace space)
        {
            m_FrameSource = "cube";
            IXChartSlotBoard board = Context?.Slots;
            XChartSlotView slot;
            if (board != null && board.TryRead(SlotId, out slot)
                && slot.Mode == XChartSlotMode.Quad3D)
            {
                Vector3 c00, c01, c11, c10;
                if (slot.TryGetQuad(0, out c00, out c01, out c11, out c10))
                {
                    XChartFaceFrame fromSlot = XChartFaceFrame.FromCorners(
                        space.Project(c00, m_Composed), space.Project(c01, m_Composed),
                        space.Project(c11, m_Composed), space.Project(c10, m_Composed));
                    if (fromSlot.IsValid)
                    {
                        m_FrameSource = "slot";
                        return fromSlot;
                    }
                }
            }

            // 回落：把同一个 id 按立方体自己的语义解释成面号（"mesh.face.3" -> 第 3 面）；
            // 解释不出来的 id 就没有回落可用，返回无效帧、由调用方清空并把 frame= 印出来。
            int faceIndex = FaceIndexFromSlotId(SlotId);
            space.BuildFaceFrames(m_FaceFrames, m_Composed);
            return faceIndex >= 0 && faceIndex < XChartCubeGeometry.FaceCount
                ? m_FaceFrames[faceIndex]
                : default(XChartFaceFrame);
        }

        /// <summary>回落用：从 <c>mesh.face.&lt;n&gt;</c> 里读面号；不是这个形状就返回 -1</summary>
        public static int FaceIndexFromSlotId(string slotId)
        {
            if (string.IsNullOrEmpty(slotId) || !slotId.StartsWith(XChartCubeGeometry.FaceSlotPrefix)) return -1;
            int index;
            return int.TryParse(slotId.Substring(XChartCubeGeometry.FaceSlotPrefix.Length), out index) ? index : -1;
        }

        /// <summary>几何侧：面帧、线、刻度、标签。空间版本变了必须重画（转立方体时标签要跟着走）</summary>
        public override void OnRender()
        {
            base.OnRender();
            if (LineRenderer == null) return;

            var space = Context?.Space;
            if (space == null) return;

            if (!m_GeometryDirty && space.ProjectionVersion == m_LastSpaceVersion) return;
            m_LastSpaceVersion = space.ProjectionVersion;

            if (!m_HasRange)
            {
                LineRenderer.ClearAll();
                m_GeometryDirty = false;
                return;
            }

            m_Composed = space.Compose(Transform);
            XChartFaceFrame frame = ResolveFaceFrame(space);

            if (!frame.IsValid || m_Majors.Count == 0)
            {
                LineRenderer.ClearAll();
                m_GeometryDirty = false;
                return;
            }

            DrawAxis(frame);
        }

        /// <summary>
        /// 轴的范围只问上下文要（手动优先、否则该维聚合，规则在 <see cref="XChartDomainMath"/>）。
        /// 对数轴不能跨 0：贴 0 取一个下界，其余照手动/聚合优先级。
        /// </summary>
        private bool ResolveRange(out double min, out double max, out double step)
        {
            min = 0.0; max = 1.0; step = 1.0;

            var context = Context;
            if (context == null || !context.TryResolveDomain(Dimension, out min, out max))
                return false;

            if (Scale == XChartAxisScale.Logarithmic && min <= 0.0)
                min = max > 0.0 ? max * 1e-3 : 1e-6;

            step = AutoStep ? XChartAxisMath.NiceStep(max - min, TargetMajorCount) : ManualStep;
            return max > min;
        }

        private void DrawAxis(XChartFaceFrame frame)
        {
            double min = m_RangeMin;
            double max = m_RangeMax;
            double step = m_RangeStep;

            // 对齐格线的读数每帧重算（"吸上几条/最大挪了多少"要跟着这一帧说话）
            m_AlignSnapped = 0;
            m_AlignTotal = 0;
            m_AlignMaxSnap = 0f;
            m_AlignNote = AlignToGrid ? "grid(none)" : "off";

            LineRenderer.ClearAll();

            bool alongS = Direction == XChartAxisDirection.AlongS;
            float span = Mathf.Clamp01(EndRatio) - Mathf.Clamp01(StartRatio);
            if (span <= 1e-4f) return;

            // 轴线上一点 + 面内垂直方向的"每单位像素"向量，刻度长度因此不受面大小影响
            Vector2 probe = alongS ? frame.Map(0.5f, CrossRatio) : frame.Map(CrossRatio, 0.5f);
            Vector2 probe2 = alongS ? frame.Map(0.5f, CrossRatio + 0.02f) : frame.Map(CrossRatio + 0.02f, 0.5f);
            Vector2 cross = probe2 - probe;
            float crossLength = cross.magnitude;
            if (crossLength <= 1e-4f) return;
            Vector2 crossUnit = cross / crossLength * TickSide;

            Vector2 axisFrom = PointAt(frame, alongS, Mathf.Clamp01(StartRatio));
            Vector2 axisTo = PointAt(frame, alongS, Mathf.Clamp01(StartRatio + span));

            // 轴在屏幕上的走向 = 标签该转的角度（绕自身 Z，UI 平面内旋转）
            float axisAngle = RotateLabelsWithAxis
                ? Mathf.Atan2(axisTo.y - axisFrom.y, axisTo.x - axisFrom.x) * Mathf.Rad2Deg
                : 0f;

            LineRenderer.DrawLine(axisFrom, axisTo, LineColor, LineThickness);

            // 标签一律来自池（基类申请、chart 发落）：这里只读，不创建、不扩表。
            // 件还没补齐时只少画标签，轴线和刻度照旧——不 NRE，也不留上一次的残字。
            int needed = LabelSource == XChartAxisLabelSource.DimensionLabel ? 1 : m_Majors.Count;
            var labels = Texts;

            for (int i = 0; i < m_Majors.Count; i++)
            {
                double value = m_Majors[i];
                float ratio = Mathf.Clamp01((float)XChartAxisMath.ToParameter(value, min, max, Scale, ExponentialStrength));
                float s = Mathf.Clamp01(StartRatio) + span * ratio;

                // 对齐格线：在**面参数**上吸附（格线发布的就是面参数位置），再折回轴上的比例，
                // 这样"刻度落在格线上"是几何意义，不是数值凑上去
                if (AlignToGrid && TrySnapParameterToGrid(alongS, s, out float snappedS))
                {
                    s = snappedS;
                    float begin = Mathf.Clamp01(StartRatio);
                    if (span > 1e-6f) ratio = Mathf.Clamp01((s - begin) / span);
                }

                Vector2 anchor = PointAt(frame, alongS, s);
                Vector2 tip = anchor + crossUnit * MajorTickLength;
                LineRenderer.DrawLine(anchor, tip, LineColor, TickThickness);

                if (MinorTickCount > 1 && i + 1 < m_Majors.Count)
                {
                    m_Minors.Clear();
                    XChartAxisMath.BuildMinorTicks(m_Majors[i], m_Majors[i + 1], MinorTickCount,
                        min, max, Scale, ExponentialStrength, m_Minors);
                    for (int m = 0; m < m_Minors.Count; m++)
                    {
                        float mr = Mathf.Clamp01((float)XChartAxisMath.ToParameter(m_Minors[m], min, max, Scale, ExponentialStrength));
                        Vector2 mAnchor = PointAt(frame, alongS, Mathf.Clamp01(StartRatio) + span * mr);
                        LineRenderer.DrawLine(mAnchor, mAnchor + crossUnit * MinorTickLength, LineColor, TickThickness);
                    }
                }

                if (i < labels.Count) WriteLabel(labels[i], anchor + crossUnit * (MajorTickLength + LabelGap),
                    LabelTextFor(i, value, step, min, max), axisAngle);
            }

            if (LabelSource == XChartAxisLabelSource.DimensionLabel && labels.Count > 0)
            {
                Vector2 end = PointAt(frame, alongS, Mathf.Clamp01(StartRatio + span));
                WriteLabel(labels[0], end + crossUnit * (MajorTickLength + LabelGap), DimensionLabel(), axisAngle);
            }

            // 没用上的槽位必须擦干净，否则上一次多出来的刻度标签会留在原地
            int used = Mathf.Min(needed, labels.Count);
            for (int i = used; i < labels.Count; i++)
                if (labels[i] != null) labels[i].text = string.Empty;

            // 件没补齐就继续脏着：下一次池调度补齐后重画一遍
            m_GeometryDirty = labels.Count < needed;
        }

        /// <summary>把"沿轴比例"换成像素点；垂直位置固定在 <see cref="CrossRatio"/></summary>
        private Vector2 PointAt(XChartFaceFrame frame, bool alongS, float s)
            => alongS ? frame.Map(s, CrossRatio) : frame.Map(CrossRatio, s);

        private void WriteLabel(TMP_Text label, Vector2 local, string text, float axisAngleDegrees)
        {
            if (label == null) return;
            label.text = text;
            label.fontSize = LabelFontSize;
            label.color = LabelColor;
            label.rectTransform.sizeDelta = LabelSize;
            label.rectTransform.anchoredPosition = local;
            label.rectTransform.localEulerAngles = new Vector3(0f, 0f, axisAngleDegrees);
        }

        private string LabelTextFor(int index, double value, double step, double min, double max)
        {
            switch (LabelSource)
            {
                case XChartAxisLabelSource.Custom:
                    return CustomLabels != null && index < CustomLabels.Length ? CustomLabels[index] : string.Empty;
                case XChartAxisLabelSource.DimensionLabel:
                    return DimensionLabel();
                default:
                    return XChartAxisMath.FormatValue(value, LabelFormat, step);
            }
        }

        private string DimensionLabel()
        {
            XChartDimensionDefinition definition;
            if (Data != null && Data.TryGetDimension(Dimension, out definition))
                return definition.DisplayName;
            return "dim " + Dimension;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 这一帧的范围是从哪来的：<c>override</c>＝chart 那一维的维度行填了覆盖、<c>aggregate</c>＝
        /// 用该维聚合、<c>none</c>＝两边都没有（于是整条轴不画）。报表靠它证明"范围归数据行"。
        /// 判据取<b>本帧的结果</b>（<see cref="m_HasRange"/>），不重新问一遍聚合——空模型下
        /// "聚合存在"是真的、值是 NaN，照那样问会印出 aggregate，那是在骗读报表的人。
        /// </summary>
        private string RangeSourceForDebug()
        {
            if (!m_HasRange) return "none";

            var data = Data;
            XChartDimensionDefinition definition;
            if (data != null && data.TryGetDimension(Dimension, out definition) && definition != null)
            {
                var over = definition.DomainOverride;
                if (over.HasMin || over.HasMax)
                    return "override[" + (over.HasMin ? over.Min.ToString("0.###") : "auto")
                        + ".." + (over.HasMax ? over.Max.ToString("0.###") : "auto") + "]";
            }
            return "aggregate";
        }

        internal override void AppendDebugLine(System.Text.StringBuilder sb)
        {
            base.AppendDebugLine(sb);
            sb.Append(" hasRange=").Append(m_HasRange)
              .Append(" range=[").Append(m_RangeMin.ToString("0.###")).Append(',').Append(m_RangeMax.ToString("0.###")).Append(']')
              .Append(" rangeSource=").Append(RangeSourceForDebug())
              // 工具**自己持有的**起止比例：面板写进配置的值有没有真的到工具身上，看这一对就分得清
              .Append(" span=").Append(StartRatio.ToString("0.###")).Append("..").Append(EndRatio.ToString("0.###"))
              .Append(" majors=").Append(m_Majors.Count)
              .Append(" slot=").Append(SlotId).Append(" dir=").Append(Direction).Append(" dim=").Append(Dimension)
              .Append(" want=").Append(RequestedTextCount)
              .Append(" have=").Append(Texts.Count)
              .Append(" pending=").Append(TextSlotsPending)
              .Append(" geoDirty=").Append(m_GeometryDirty)
              .Append(" lines=").Append(LineRenderer == null ? "NULL" : "ok")
              .Append(" wireVerts=").Append(LineRenderer != null ? LineRenderer.LiveVertexCount : 0)
              .Append(" frame=").Append(m_FrameSource)
              // 对齐格线的读数："没吸上"和"没开对齐"在画面上一样，只有这一行分得清
              .Append(" align=").Append(m_AlignNote);
        }
#endif

        protected override void OnDispose()
        {
            LineRenderer = null;
            m_Majors.Clear();
            m_Minors.Clear();
        }
    }
}
