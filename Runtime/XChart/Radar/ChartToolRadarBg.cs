using UnityEngine;
using XericLibrary.Runtime.UIGraph;
using TMPro;

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
    [AddComponentMenu("Xeric/XChart/Radar/Radar Background Tool")]
    public class ChartToolRadarBg : XChartToolBase
    {
        // ── 配置 ──────────────────────────────────────────────────

        /// <summary>雷达图半径（像素）</summary>
        public float Radius { get; set; } = 120f;

        /// <summary>中心位置</summary>
        public Vector2 Center { get; set; } = Vector2.zero;

        /// <summary>维度数量</summary>
        public int DimensionCount { get; set; } = 5;

        /// <summary>维度名称列表</summary>
        public string[] DimensionNames { get; set; } = new string[0];

        /// <summary>网格环数量（含外圈）</summary>
        public int RingCount { get; set; } = 4;

        /// <summary>网格颜色</summary>
        public Color GridColor { get; set; } = new Color(0.5f, 0.5f, 0.5f, 0.3f);

        /// <summary>轴线颜色</summary>
        public Color AxisColor { get; set; } = new Color(0.5f, 0.5f, 0.5f, 0.2f);

        /// <summary>网格线宽</summary>
        public float GridThickness { get; set; } = 1f;

        /// <summary>轴线宽</summary>
        public float AxisThickness { get; set; } = 1f;

        /// <summary>是否显示网格</summary>
        public bool ShowGrid { get; set; } = true;

        /// <summary>是否显示轴线</summary>
        public bool ShowAxis { get; set; } = true;

        /// <summary>是否显示标签</summary>
        public bool ShowLabels { get; set; } = true;

        /// <summary>标签字体大小</summary>
        public float LabelFontSize { get; set; } = 14f;

        /// <summary>标签颜色</summary>
        public Color LabelColor { get; set; } = Color.white;

        /// <summary>标签相对端点的偏移量（像素）</summary>
        public float LabelOffset { get; set; } = 10f;

        // ── 内部组件 ──────────────────────────────────────────────

        /// <summary>网格线渲染器</summary>
        public UILineRendererV2 GridRenderer { get; private set; }

        /// <summary>轴线渲染器</summary>
        public UILineRendererV2 AxisRenderer { get; private set; }

        /// <summary>标签的父变换</summary>
        public Transform LabelsRoot { get; private set; }

        /// <summary>所有标签文本组件</summary>
        public TMP_Text[] LabelTexts { get; private set; }

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

            // 创建标签父节点
            var labelsObj = CreateChild("Labels");
            LabelsRoot = labelsObj;

            SetVisible(true);
        }

        // ── 标记脏 ────────────────────────────────────────────────

        public void MarkDirty()
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
            if (LabelsRoot == null) return;

            int dims = DimensionCount;
            var dirs = XChartRadarDataModel.GetDimensionDirections(dims);

            // 确保标签数量匹配
            EnsureLabelCount(dims);

            bool show = ShowLabels && dims >= 3;

            for (int i = 0; i < dims; i++)
            {
                var label = LabelTexts[i];
                if (label == null) continue;

                label.gameObject.SetActive(show);

                if (!show) continue;

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

                // 应用位置
                var rt = label.rectTransform;
                rt.anchoredPosition = labelPos;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
            }

            // 隐藏多余的标签
            for (int i = dims; i < LabelTexts.Length; i++)
            {
                if (LabelTexts[i] != null)
                    LabelTexts[i].gameObject.SetActive(false);
            }
        }

        /// <summary>确保标签数组长度匹配维度数量</summary>
        private void EnsureLabelCount(int count)
        {
            if (LabelTexts != null && LabelTexts.Length == count)
                return;

            // 清理旧的标签
            if (LabelTexts != null)
            {
                foreach (var label in LabelTexts)
                {
                    if (label != null && label.gameObject != null)
                    {
                        if (Application.isPlaying)
                            Object.Destroy(label.gameObject);
                        else
                            Object.DestroyImmediate(label.gameObject);
                    }
                }
            }

            LabelTexts = new TMP_Text[count];

            for (int i = 0; i < count; i++)
            {
                var labelObj = new GameObject($"Label_{i}");
                labelObj.transform.SetParent(LabelsRoot, false);
                labelObj.hideFlags = CurrentFlags;

                var tmp = labelObj.AddComponent<TextMeshProUGUI>();
                tmp.text = "";
                tmp.fontSize = LabelFontSize;
                tmp.color = LabelColor;
                tmp.alignment = TextAlignmentOptions.Center;

                var rt = tmp.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(100f, 24f);

                LabelTexts[i] = tmp;
            }
        }

        // ── 清理 ──────────────────────────────────────────────────

        protected override void OnDispose()
        {
            GridRenderer = null;
            AxisRenderer = null;
            LabelsRoot = null;
            LabelTexts = null;
        }
    }
}