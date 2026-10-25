using UnityEngine;
using XericUI.Core;

namespace XericUI.XChart.Radar
{
    /// <summary>
    /// 雷达图主组件。
    ///
    /// 在 UI 平面上绘制一个或多个雷达图数据系列。
    /// 每个系列对应一个闭合多边形线框，共享同一组维度轴。
    /// 每个维度端点显示 TMP 标签。
    ///
    /// 使用方式：
    ///   1. 在场景中创建空 GameObject，挂载本组件
    ///   2. 设置维度：chart.SetDimensions("速度", "力量", "防御", "敏捷", "魔力")
    ///   3. 添加数据：chart.AddSeries("战士", new float[] { 0.8f, 0.9f, 0.7f, 0.4f, 0.2f })
    ///
    /// 子节点结构：
    ///   [XRadarChart]
    ///     └─ [XChartTools]
    ///          ├─ [ChartTool_RadarBg] (OrderIndex=0) ← 网格 + 轴线 + 标签
    ///          │    ├─ GridRenderer (UILineRendererV2)
    ///          │    ├─ AxisRenderer (UILineRendererV2)
    ///          │    └─ [Labels]
    ///          │         ├─ Label_0 (TMP_Text)
    ///          │         ├─ Label_1 (TMP_Text)
    ///          │         └─ ...
    ///          └─ [ChartTool_RadarData] (OrderIndex=10) ← 数据线框
    ///               └─ DataRenderer (UILineRendererV2)
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.Chart + "/Radar/Radar Chart")]
    [ExecuteAlways]
    public class XRadarChart : XChartBase
    {
        // ── 串行化字段 ────────────────────────────────────────────

        [Header("雷达图外观")]
        [SerializeField] private float m_Radius = 120f;
        [SerializeField] private Vector2 m_CenterOffset = Vector2.zero;

        [Header("网格")]
        [SerializeField] private bool m_ShowGrid = true;
        [SerializeField] private int m_RingCount = 4;
        [SerializeField] private Color m_GridColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        [SerializeField] private float m_GridThickness = 1f;

        [Header("轴线")]
        [SerializeField] private bool m_ShowAxis = true;
        [SerializeField] private Color m_AxisColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);
        [SerializeField] private float m_AxisThickness = 1f;

        [Header("标签")]
        [SerializeField] private bool m_ShowLabels = true;
        [SerializeField] private float m_LabelFontSize = 14f;
        [SerializeField] private Color m_LabelColor = Color.white;
        [SerializeField] private float m_LabelOffset = 10f;

        [Header("默认系列外观")]
        [SerializeField] private Color m_DefaultSeriesColor = new Color(0.33f, 0.67f, 1f, 1f);
        [SerializeField] private float m_DefaultLineWidth = 2f;

        [Header("自动更新")]
        [SerializeField] private bool m_AutoUpdate = true;

        // ── 工具引用 ──────────────────────────────────────────────

        private ChartToolRadarBg m_RadarBg;
        private ChartToolRadarData m_RadarData;

        // ── 数据模型 ──────────────────────────────────────────────

        private XChartRadarDataModel m_DataModel = new XChartRadarDataModel();

        /// <summary>雷达数据模型（只读）</summary>
        public XChartRadarDataModel RadarDataModel => m_DataModel;

        // ── 预置颜色轮（用于自动分配颜色） ────────────────────────

        private static readonly Color[] s_ColorWheel = new Color[]
        {
            new Color(0.33f, 0.67f, 1f, 1f),  // 蓝
            new Color(1f, 0.33f, 0.33f, 1f),  // 红
            new Color(0.33f, 1f, 0.33f, 1f),  // 绿
            new Color(1f, 0.67f, 0f, 1f),     // 橙
            new Color(0.67f, 0.33f, 1f, 1f),  // 紫
            new Color(1f, 0.33f, 0.67f, 1f),  // 粉
            new Color(0f, 0.67f, 1f, 1f),     // 天蓝
            new Color(0.5f, 1f, 0.5f, 1f),    // 亮绿
        };

        // ── 初始化 ────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureToolsCreated();
        }

        private void Start()
        {
            EnsureToolsCreated();
            SyncAllParams();
            RefreshChart();
        }

        private void EnsureToolsCreated()
        {
            if (m_RadarBg != null && m_RadarData != null) return;

            if (m_RadarBg == null)
                m_RadarBg = AddTool<ChartToolRadarBg>("ChartTool_RadarBg", 0);

            if (m_RadarData == null)
                m_RadarData = AddTool<ChartToolRadarData>("ChartTool_RadarData", 10);
        }

        // ── 参数同步 ──────────────────────────────────────────────

        private void SyncAllParams()
        {
            SyncBgParams();
            SyncDataParams();
        }

        private void SyncBgParams()
        {
            if (m_RadarBg == null) return;
            m_RadarBg.Radius = m_Radius;
            m_RadarBg.Center = m_CenterOffset;
            m_RadarBg.DimensionCount = m_DataModel.DimensionCount;
            m_RadarBg.DimensionNames = m_DataModel.Dimensions;
            m_RadarBg.RingCount = m_RingCount;
            m_RadarBg.GridColor = m_GridColor;
            m_RadarBg.GridThickness = m_GridThickness;
            m_RadarBg.AxisColor = m_AxisColor;
            m_RadarBg.AxisThickness = m_AxisThickness;
            m_RadarBg.ShowGrid = m_ShowGrid;
            m_RadarBg.ShowAxis = m_ShowAxis;
            m_RadarBg.ShowLabels = m_ShowLabels;
            m_RadarBg.LabelFontSize = m_LabelFontSize;
            m_RadarBg.LabelColor = m_LabelColor;
            m_RadarBg.LabelOffset = m_LabelOffset;
            m_RadarBg.MarkDirty();
        }

        private void SyncDataParams()
        {
            if (m_RadarData == null) return;
            m_RadarData.Radius = m_Radius;
            m_RadarData.Center = m_CenterOffset;
            m_RadarData.DimensionCount = m_DataModel.DimensionCount;
            m_RadarData.MarkDirty();
        }

        // ── 每帧更新 ──────────────────────────────────────────────

        protected override void Update()
        {
            base.Update();

            if (m_AutoUpdate)
            {
                if (m_DataModel.IsDirty(XChartRadarDataModel.DirtyFlags.DimensionsChanged))
                {
                    // 维度变化：需要同步背景和数据
                    SyncBgParams();
                    SyncDataParams();
                    PushDataToTool();
                    m_DataModel.ClearDirty(XChartRadarDataModel.DirtyFlags.DimensionsChanged);
                }
                else if (m_DataModel.IsDirty(XChartRadarDataModel.DirtyFlags.DataChanged))
                {
                    // 数据变化：只需要更新数据
                    SyncDataParams();
                    PushDataToTool();
                    m_DataModel.ClearDirty(XChartRadarDataModel.DirtyFlags.DataChanged);
                }
            }
        }

        // ── 数据推送 ──────────────────────────────────────────────

        private void PushDataToTool()
        {
            if (m_RadarData == null) return;

            // 将数据模型的系列列表转为数组
            int count = m_DataModel.SeriesCount;
            var seriesArray = new XChartRadarDataModel.RadarSeries[count];
            for (int i = 0; i < count; i++)
                seriesArray[i] = m_DataModel.GetSeries(i);

            m_RadarData.SetSeries(seriesArray, m_DataModel.MaxValue);
        }

        // ── 公共 API ──────────────────────────────────────────────

        #region 公共 API

        /// <summary>刷新图表</summary>
        public void RefreshChart()
        {
            EnsureToolsCreated();
            SyncAllParams();
            PushDataToTool();
        }

        /// <summary>设置维度名称</summary>
        public void SetDimensions(params string[] dimensionNames)
        {
            m_DataModel.SetDimensions(dimensionNames);
            var dimensions = new System.Collections.Generic.List<XChartDimensionDefinition>(dimensionNames.Length);
            for (int i = 0; i < dimensionNames.Length; i++)
                dimensions.Add(new XChartDimensionDefinition($"radar_{i}", dimensionNames[i], XChartDimensionKind.Number));
            DataModel.SetDimensions(dimensions);
            // 由 Update 自动同步
        }

        /// <summary>设置维度名称（从列表）</summary>
        public void SetDimensions(System.Collections.Generic.IList<string> dimensionNames)
        {
            m_DataModel.SetDimensions(dimensionNames);
        }

        /// <summary>添加一个数据系列（自动分配颜色）</summary>
        public void AddSeries(string name, float[] values)
        {
            Color color = s_ColorWheel[m_DataModel.SeriesCount % s_ColorWheel.Length];
            m_DataModel.AddSeries(name, values, color, m_DefaultLineWidth);
            DataModel.TryAddRecord(m_DataModel.SeriesCount, values, new[] { name });
        }

        /// <summary>添加一个数据系列（指定颜色）</summary>
        public void AddSeries(string name, float[] values, Color color, float lineWidth = 2f)
        {
            m_DataModel.AddSeries(name, values, color, lineWidth);
            DataModel.TryAddRecord(m_DataModel.SeriesCount, values, new[] { name });
        }

        /// <summary>批量设置数据系列</summary>
        public void SetSeries(System.Collections.Generic.IList<XChartRadarDataModel.RadarSeries> series)
        {
            m_DataModel.SetSeries(series);
        }

        /// <summary>移除指定系列</summary>
        public void RemoveSeries(int index)
        {
            m_DataModel.RemoveSeries(index);
        }

        /// <summary>清空所有数据</summary>
        public void ClearData()
        {
            m_DataModel.ClearAll();
            RefreshChart();
        }

        /// <summary>是否自动缩放</summary>
        public bool AutoScale
        {
            get => m_DataModel.AutoScale;
            set { m_DataModel.AutoScale = value; RefreshChart(); }
        }

        /// <summary>自定义最大值（AutoScale=false 时使用）</summary>
        public float CustomMaxValue
        {
            get => m_DataModel.CustomMaxValue;
            set { m_DataModel.CustomMaxValue = Mathf.Max(0.01f, value); RefreshChart(); }
        }

        #endregion

        // ── 运行时属性 ────────────────────────────────────────────

        #region 运行时属性

        public float Radius
        {
            get => m_Radius;
            set { m_Radius = Mathf.Max(10f, value); SyncBgParams(); SyncDataParams(); }
        }

        public Vector2 CenterOffset
        {
            get => m_CenterOffset;
            set { m_CenterOffset = value; SyncBgParams(); SyncDataParams(); }
        }

        public bool ShowGrid
        {
            get => m_ShowGrid;
            set { m_ShowGrid = value; SyncBgParams(); }
        }

        public int RingCount
        {
            get => m_RingCount;
            set { m_RingCount = Mathf.Max(1, value); SyncBgParams(); }
        }

        public Color GridColor
        {
            get => m_GridColor;
            set { m_GridColor = value; SyncBgParams(); }
        }

        public bool ShowAxis
        {
            get => m_ShowAxis;
            set { m_ShowAxis = value; SyncBgParams(); }
        }

        public Color AxisColor
        {
            get => m_AxisColor;
            set { m_AxisColor = value; SyncBgParams(); }
        }

        public bool ShowLabels
        {
            get => m_ShowLabels;
            set { m_ShowLabels = value; SyncBgParams(); }
        }

        public float LabelFontSize
        {
            get => m_LabelFontSize;
            set { m_LabelFontSize = Mathf.Max(4f, value); SyncBgParams(); }
        }

        public Color LabelColor
        {
            get => m_LabelColor;
            set { m_LabelColor = value; SyncBgParams(); }
        }

        public float LabelOffset
        {
            get => m_LabelOffset;
            set { m_LabelOffset = value; SyncBgParams(); }
        }

        #endregion

        // ── 编辑器 ────────────────────────────────────────────────

        /// <summary>
        /// override 基类并调 base：写成 <c>private void OnValidate()</c> 会遮掉基类那份，
        /// "重灌元素 + 强制刷新"就再也不会为这个组件执行（和散点图同一个坑）。
        /// </summary>
        protected override void OnValidate()
        {
            if (m_Radius < 10f) m_Radius = 10f;
            if (m_RingCount < 1) m_RingCount = 1;
            if (m_LabelFontSize < 4f) m_LabelFontSize = 4f;

            base.OnValidate();

#if UNITY_EDITOR
            if (isActiveAndEnabled && m_RadarBg != null)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null && isActiveAndEnabled)
                    {
                        SyncAllParams();
                        PushDataToTool();
                    }
                };
            }
#endif
        }
    }
}
