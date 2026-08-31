using UnityEngine;
using XericLibrary.Runtime.UIGraph;

namespace XericUI.XChart.Radar
{
    /// <summary>
    /// 雷达图数据线框渲染工具。
    ///
    /// 使用 UILineRendererV2 绘制每个数据系列的多边形轮廓。
    /// 仅绘制线框，无填充效果。
    /// 每个系列一个闭合多边形，颜色和线宽独立配置。
    ///
    /// 子节点结构：
    ///   [ChartTool_RadarData]
    ///     └─ DataRenderer (UILineRendererV2) — 所有数据系列的线框
    /// </summary>
    [AddComponentMenu("Xeric/XChart/Radar/Radar Data Tool")]
    public class ChartToolRadarData : XChartToolBase
    {
        // ── 配置 ──────────────────────────────────────────────────

        /// <summary>雷达图半径（像素）</summary>
        public float Radius { get; set; } = 120f;

        /// <summary>中心位置</summary>
        public Vector2 Center { get; set; } = Vector2.zero;

        /// <summary>维度数量</summary>
        public int DimensionCount { get; set; } = 5;

        // ── 内部组件 ──────────────────────────────────────────────

        /// <summary>数据线框渲染器</summary>
        public UILineRendererV2 DataRenderer { get; private set; }

        // ── 数据缓存 ──────────────────────────────────────────────

        private XChartRadarDataModel.RadarSeries[] m_Series = new XChartRadarDataModel.RadarSeries[0];
        private float m_MaxValue = 1f;
        private bool m_DataDirty = true;

        // ── 初始化 ────────────────────────────────────────────────

        protected override void OnInitialize()
        {
            DataRenderer = CreateChildComponent<UILineRendererV2>("DataRenderer");
            if (DataRenderer != null)
            {
                var rt = DataRenderer.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

        }

        // ── 数据更新 ──────────────────────────────────────────────

        /// <summary>
        /// 设置数据系列。
        /// </summary>
        /// <param name="series">数据系列数组</param>
        /// <param name="maxValue">当前最大值（用于归一化）</param>
        public void SetSeries(XChartRadarDataModel.RadarSeries[] series, float maxValue)
        {
            m_Series = series ?? new XChartRadarDataModel.RadarSeries[0];
            m_MaxValue = maxValue > 0.001f ? maxValue : 1f;
            m_DataDirty = true;
        }

        /// <summary>标记数据为脏（下次渲染时刷新）</summary>
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

            if (DataRenderer == null) return;
            DataRenderer.ClearAll();

            if (DimensionCount < 3 || m_Series.Length == 0) return;

            // 预计算方向
            var dirs = XChartRadarDataModel.GetDimensionDirections(DimensionCount);

            // 绘制每个数据系列的多边形轮廓
            for (int si = 0; si < m_Series.Length; si++)
            {
                var series = m_Series[si];
                if (series.values == null || series.values.Length == 0) continue;

                DrawSeriesPolygon(series, dirs);
            }
        }

        private void DrawSeriesPolygon(XChartRadarDataModel.RadarSeries series, Vector2[] dirs)
        {
            int dims = DimensionCount;
            float invMax = 1f / m_MaxValue;

            // 计算多边形顶点
            var pts = new Vector2[dims + 1];
            for (int i = 0; i < dims; i++)
            {
                float val = (i < series.values.Length) ? series.values[i] : 0f;
                float r = Mathf.Clamp01(val * invMax) * Radius;
                pts[i] = Center + dirs[i] * r;
            }
            pts[dims] = pts[0]; // 闭合

            // 绘制闭合多边形线框
            for (int i = 0; i < dims; i++)
            {
                DataRenderer.DrawLine(pts[i], pts[i + 1],
                    series.color, series.lineWidth);
            }
        }

        // ── 清理 ──────────────────────────────────────────────────

        protected override void OnDispose()
        {
            DataRenderer = null;
            m_Series = null;
        }
    }
}
