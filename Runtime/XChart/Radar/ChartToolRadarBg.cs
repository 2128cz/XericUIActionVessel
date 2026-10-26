using UnityEngine;
using XericLibrary.Runtime.UIGraph;
using TMPro;
using XericUI.Core;

namespace XericUI.XChart.Radar
{
    /// <summary>
    /// 雷达图网格背景工具。
    ///
    /// 使用 UILineRendererV2 绘制：
    ///   1. 同心多边形网格（外圈 + 内圈百分比环）
    ///   2. 从中心到各维度的轴线
    ///   3. 每个维度端点的 TMP 标签
    ///
    /// 子节点结构：
    ///   [ChartTool_RadarBg]
    ///     ├─ GridRenderer (UILineRendererV2) — 网格线
    ///     ├─ AxisRenderer (UILineRendererV2) — 轴线
    ///     └─ [Labels] (transform)
    ///          ├─ Label_0 (TMP_Text)
    ///          ├─ Label_1 (TMP_Text)
    ///          └─ ...
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.Chart + "/Radar/Radar Background Tool")]
    public class ChartToolRadarBg : XChartToolBase
    {
        // ── 配置 ──────────────────────────────────────────────────

        private float m_Radius = 120f;
        /// <summary>雷达图半径（像素）</summary>
        public float Radius { get => m_Radius; set { m_Radius = value; MarkDirty(); } }

        private Vector2 m_Center = Vector2.zero;
        /// <summary>中心位置</summary>
        public Vector2 Center { get => m_Center; set { m_Center = value; MarkDirty(); } }

        private int m_DimensionCount = 5;
        /// <summary>维度数量</summary>
        public int DimensionCount { get => m_DimensionCount; set { m_DimensionCount = value; MarkDirty(); } }

        private string[] m_DimensionNames = new string[0];
        /// <summary>维度名称列表</summary>
        public string[] DimensionNames { get => m_DimensionNames; set { m_DimensionNames = value; MarkDirty(); } }

        private int m_RingCount = 4;
        /// <summary>网格环数量（含外圈）</summary>
        public int RingCount { get => m_RingCount; set { m_RingCount = value; MarkDirty(); } }

        private Color m_GridColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        /// <summary>网格颜色</summary>
        public Color GridColor { get => m_GridColor; set { m_GridColor = value; MarkDirty(); } }

        private Color m_AxisColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);
        /// <summary>轴线颜色</summary>
        public Color AxisColor { get => m_AxisColor; set { m_AxisColor = value; MarkDirty(); } }

        private float m_GridThickness = 1f;
        /// <summary>网格线宽</summary>
        public float GridThickness { get => m_GridThickness; set { m_GridThickness = value; MarkDirty(); } }

        private float m_AxisThickness = 1f;
        /// <summary>轴线宽</summary>
        public float AxisThickness { get => m_AxisThickness; set { m_AxisThickness = value; MarkDirty(); } }

        private bool m_ShowGrid = true;
        /// <summary>是否显示网格</summary>
        public bool ShowGrid { get => m_ShowGrid; set { m_ShowGrid = value; MarkDirty(); } }

        private bool m_ShowAxis = true;
        /// <summary>是否显示轴线</summary>
        public bool ShowAxis { get => m_ShowAxis; set { m_ShowAxis = value; MarkDirty(); } }

        private bool m_ShowLabels = true;
        /// <summary>是否显示标签</summary>
        public bool ShowLabels { get => m_ShowLabels; set { m_ShowLabels = value; MarkDirty(); } }

        private float m_LabelFontSize = 14f;
        /// <summary>标签字体大小</summary>
        public float LabelFontSize { get => m_LabelFontSize; set { m_LabelFontSize = value; MarkDirty(); } }

        private Color m_LabelColor = Color.white;
        /// <summary>标签颜色</summary>
        public Color LabelColor { get => m_LabelColor; set { m_LabelColor = value; MarkDirty(); } }

        private float m_LabelOffset = 10f;
        /// <summary>标签相对端点的偏移量（像素）</summary>
        public float LabelOffset { get => m_LabelOffset; set { m_LabelOffset = value; MarkDirty(); } }

        // ── 内部组件 ──────────────────────────────────────────────

        /// <summary>网格线渲染器</summary>
        public UILineRendererV2 GridRenderer { get; private set; }

        /// <summary>轴线渲染器</summary>
        public UILineRendererV2 AxisRenderer { get; private set; }

        /// <summary>
        /// 维度标签的数量口径：<b>要几个就申请几个</b>，件本身由 chart 的文本槽位池发落
        /// （规范 §5.4：隐藏靠少申请，不靠把发落到的槽位 <c>SetActive(false)</c>——那会被校验当无效丢弃）。
        /// </summary>
        private int DesiredLabelCount
            => ShowLabels && DimensionCount >= 3 ? DimensionCount : 0;

        // ── 内部状态 ──────────────────────────────────────────────

        private bool m_DataDirty = true;

        // ── 初始化 ────────────────────────────────────────────────

        protected override void OnInitialize()
        {
            // 创建网格线渲染器
            GridRenderer = CreateChildComponent<UILineRendererV2>("GridRenderer");
            if (GridRenderer != null)
            {
                var rt = GridRenderer.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            // 创建轴线渲染器
            AxisRenderer = CreateChildComponent<UILineRendererV2>("AxisRenderer");
            if (AxisRenderer != null)
            {
                var rt = AxisRenderer.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }

        /// <summary>申请维度标签（只登记需求，件在 chart 的池调度里发落）</summary>
        public override void OnUpdate()
        {
            base.OnUpdate();
            RequestTextSlots(DesiredLabelCount);
        }

        // ── 标记脏 ────────────────────────────────────────────────

        public override void MarkDirty()
        {
            m_DataDirty = true;
        }

        // ── 渲染 ──────────────────────────────────────────────────

        public override void OnRender()
        {
            base.OnRender();
            if (!m_DataDirty) return;
            m_DataDirty = false;

            DrawGrid();
            DrawAxis();
            UpdateLabels();
            // 槽位是下一轮调度才补齐的：还欠着件就保持脏，补齐后重画一次
            if (TextSlotsPending) m_DataDirty = true;
        }

        // ── 网格绘制 ──────────────────────────────────────────────

        private void DrawGrid()
        {
            if (GridRenderer == null) return;
            GridRenderer.ClearAll();

            if (!ShowGrid || DimensionCount < 3) return;

            // 预计算所有维度的方向
            int dims = DimensionCount;
            var dirs = XChartRadarDataModel.GetDimensionDirections(dims);

            // 绘制同心多边形环
            for (int ring = 1; ring <= RingCount; ring++)
            {
                float r = Radius * ring / RingCount;

                // 构建多边形顶点
                var pts = new Vector2[dims + 1];
                for (int i = 0; i < dims; i++)
                    pts[i] = Center + dirs[i] * r;
                pts[dims] = pts[0]; // 闭合

                // 绘制每个线段
                for (int i = 0; i < dims; i++)
                {
                    GridRenderer.DrawLine(pts[i], pts[i + 1],
                        GridColor, GridThickness);
                }
            }
        }

        // ── 轴线绘制 ──────────────────────────────────────────────

        private void DrawAxis()
        {
            if (AxisRenderer == null) return;
            AxisRenderer.ClearAll();

            if (!ShowAxis || DimensionCount < 3) return;

            int dims = DimensionCount;
            var dirs = XChartRadarDataModel.GetDimensionDirections(dims);

            for (int i = 0; i < dims; i++)
            {
                Vector2 end = Center + dirs[i] * Radius;
                AxisRenderer.DrawLine(Center, end, AxisColor, AxisThickness);
            }
        }

        // ── 标签管理 ──────────────────────────────────────────────

        private void UpdateLabels()
        {
            int dims = DimensionCount;
            var dirs = XChartRadarDataModel.GetDimensionDirections(dims);
            var labels = Texts;

            // 件要下一轮调度才补齐：这一帧先画已有的那些，欠着的由 OnRender 保持脏后再画一遍
            int usable = Mathf.Min(dims, labels.Count);

            for (int i = 0; i < usable; i++)
            {
                var label = labels[i];
                if (label == null) continue;

                // 设置文本
                string name = (i < DimensionNames.Length && DimensionNames[i] != null)
                    ? DimensionNames[i]
                    : $"Dim {i}";
                label.text = name;
                label.fontSize = LabelFontSize;
                label.color = LabelColor;

                // 计算标签位置：端点向外偏移
                Vector2 endPos = Center + dirs[i] * Radius;
                Vector2 labelPos = endPos + dirs[i] * LabelOffset;

                // 对齐方式：根据方向决定
                float angle = Mathf.Atan2(dirs[i].y, dirs[i].x) * Mathf.Rad2Deg;
                if (angle > -45f && angle <= 45f)
                    label.alignment = TextAlignmentOptions.Left;
                else if (angle > 45f && angle <= 135f)
                    label.alignment = TextAlignmentOptions.Bottom;
                else if (angle > 135f || angle <= -135f)
                    label.alignment = TextAlignmentOptions.Right;
                else
                    label.alignment = TextAlignmentOptions.Top;

                // 应用位置（尺寸也要在这里设：槽位来自池，创建时不知道要给哪个图表用）
                var rt = label.rectTransform;
                rt.anchoredPosition = labelPos;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(100f, 24f);
            }

            // 没点亮的槽位必须擦空：池件是跨维度数复用的，留着上一轮的字就是残影
            for (int i = usable; i < labels.Count; i++)
                if (labels[i] != null) labels[i].text = string.Empty;
        }

        // ── 清理 ──────────────────────────────────────────────────

        protected override void OnDispose()
        {
            GridRenderer = null;
            AxisRenderer = null;
        }
    }
}
