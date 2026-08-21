using UnityEngine;
using XericLibrary.Runtime.UIGraph;
using PrimitiveType = XericLibrary.Runtime.UIGraph.PrimitiveType;

namespace XericUI.XChart.Pseudo3D
{
    /// <summary>
    /// 伪 3D 散点图主组件。
    ///
    /// 在 UI 平面上绘制一个带有透视效果的 3D 散点图：
    ///   - 立方体线框代表三维空间边界
    ///   - 三个可见面上的标线网格帮助定位
    ///   - 数据点以图元（多边形/矩形）形式渲染在立方体内部
    ///
    /// 使用方式：
    ///   1. 在场景中创建空 GameObject，挂载本组件
    ///   2. 设置数据：chart.SetData(xs, ys, zs)
    ///   3. 调整外观参数
    ///
    /// 子节点结构：
    ///   [XPseudo3DScatterChart]
    ///     └─ [XChartTools]
    ///          ├─ [ChartTool_CubeBg] ← 立方体线框 + 网格
    ///          │    ├─ CubeEdgeRenderer (UILineRendererV2)
    ///          │    └─ CubeGridRenderer (UILineRendererV2)
    ///          └─ [ChartTool_ScatterPoints]
    ///               └─ PointRenderer (XScatterPointRenderer)
    /// </summary>
    [AddComponentMenu("Xeric/XChart/Pseudo3D/Pseudo3D Scatter Chart")]
    [ExecuteAlways]
    public class XPseudo3DScatterChart : XChartBase
    {
        // ── 串行化字段（Inspector 可编辑） ────────────────────────

        [Header("立方体外观")]
        [SerializeField] private float m_CubeHalfSize = 150f;
        [SerializeField] private float m_Pitch = 25f;
        [SerializeField] private float m_Yaw = -30f;
        [SerializeField] private Color m_FrontEdgeColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        [SerializeField] private Color m_BackEdgeColor = new Color(0.4f, 0.4f, 0.4f, 0.3f);
        [SerializeField] private float m_EdgeThickness = 2f;
        [SerializeField] private Color m_GridLineColor = new Color(0.5f, 0.5f, 0.5f, 0.25f);
        [SerializeField] private int m_GridDivisions = 5;
        [SerializeField] private bool m_ShowBackEdges = true;
        [SerializeField] private bool m_ShowGridLines = true;

        [Header("散点外观")]
        [SerializeField] private PrimitiveType m_PointType = PrimitiveType.Polygon;
        [SerializeField] private SizeMode m_PointSizeMode = SizeMode.InscribedCircle;
        [SerializeField] private float m_PointSize = 8f;
        [SerializeField] private Color m_PointColor = new Color(0.33f, 0.67f, 1f, 1f);
        [SerializeField] private Color m_PointCenterColor = Color.white;
        [SerializeField] private Color m_PointBorderColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        [SerializeField] private float m_PointBorderThickness = 1f;
        [SerializeField] private int m_PointSideCount = 8;
        [SerializeField] private float m_PointChamfer = 0f;
        [SerializeField] private float m_PointAngle = 0f;

        [Header("颜色映射")]
        [SerializeField] private bool m_EnableColorMapping = false;
        [SerializeField] private Color m_ColorMapMin = Color.blue;
        [SerializeField] private Color m_ColorMapMax = Color.red;
        [SerializeField] private int m_ColorMapAxis = 2;

        [Header("数据")]
        [SerializeField] private bool m_AutoUpdate = true;

        // ── 工具引用 ──────────────────────────────────────────────

        private ChartToolCubeBg m_CubeBg;
        private ChartToolScatterPoints m_ScatterPoints;

        // ── 数据模型 ──────────────────────────────────────────────

        private XChartDataModel3D m_DataModel = new XChartDataModel3D();

        /// <summary>数据模型（只读）</summary>
        public XChartDataModel3D DataModel => m_DataModel;

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
            if (m_CubeBg != null && m_ScatterPoints != null) return;

            // 创建立方体背景工具（OrderIndex 0，先渲染）
            if (m_CubeBg == null)
            {
                m_CubeBg = AddTool<ChartToolCubeBg>("ChartTool_CubeBg", 0);
            }

            // 创建散点工具（OrderIndex 10，后渲染，在上层）
            if (m_ScatterPoints == null)
            {
                m_ScatterPoints = AddTool<ChartToolScatterPoints>("ChartTool_ScatterPoints", 10);
                m_ScatterPoints.CubeBg = m_CubeBg;
            }
        }

        // ── 参数同步 ──────────────────────────────────────────────

        private void SyncAllParams()
        {
            SyncCubeParams();
            SyncPointParams();
        }

        private void SyncCubeParams()
        {
            if (m_CubeBg == null) return;
            m_CubeBg.CubeHalfSize = m_CubeHalfSize;
            m_CubeBg.Pitch = m_Pitch;
            m_CubeBg.Yaw = m_Yaw;
            m_CubeBg.FrontEdgeColor = m_FrontEdgeColor;
            m_CubeBg.BackEdgeColor = m_BackEdgeColor;
            m_CubeBg.EdgeThickness = m_EdgeThickness;
            m_CubeBg.GridLineColor = m_GridLineColor;
            m_CubeBg.GridDivisions = m_GridDivisions;
            m_CubeBg.ShowBackEdges = m_ShowBackEdges;
            m_CubeBg.ShowGridLines = m_ShowGridLines;
            m_CubeBg.MarkProjectionDirty();

            // 投影变化时，散点需要重新投影
            if (m_ScatterPoints != null)
                m_ScatterPoints.MarkDataDirty();
        }

        private void SyncPointParams()
        {
            if (m_ScatterPoints == null) return;
            m_ScatterPoints.PointType = m_PointType;
            m_ScatterPoints.SizeMode = m_PointSizeMode;
            m_ScatterPoints.PointSize = m_PointSize;
            m_ScatterPoints.PointColor = m_PointColor;
            m_ScatterPoints.CenterColor = m_PointCenterColor;
            m_ScatterPoints.BorderColor = m_PointBorderColor;
            m_ScatterPoints.BorderThickness = m_PointBorderThickness;
            m_ScatterPoints.SideCount = m_PointSideCount;
            m_ScatterPoints.ChamferSize = m_PointChamfer;
            m_ScatterPoints.PointAngle = m_PointAngle;
            m_ScatterPoints.EnableColorMapping = m_EnableColorMapping;
            m_ScatterPoints.ColorMapMin = m_ColorMapMin;
            m_ScatterPoints.ColorMapMax = m_ColorMapMax;
            m_ScatterPoints.ColorMapAxis = m_ColorMapAxis;
        }

        // ── 每帧更新 ──────────────────────────────────────────────

        protected override void Update()
        {
            base.Update();

            if (m_AutoUpdate && m_DataModel.IsDirty(XChartDataModel3D.DirtyFlags.DataChanged))
            {
                RefreshChart();
                m_DataModel.ClearDirty(XChartDataModel3D.DirtyFlags.DataChanged);
            }
        }

        // ── 公共 API ──────────────────────────────────────────────

        #region 公共 API

        /// <summary>刷新图表（重新投影所有数据点）</summary>
        public void RefreshChart()
        {
            EnsureToolsCreated();

            // 标记立方体投影需要重新计算
            if (m_CubeBg != null)
                m_CubeBg.MarkProjectionDirty();

            // 获取归一化数据点并传递给散点工具
            if (m_ScatterPoints != null && m_DataModel.HasData)
            {
                var normalized = m_DataModel.GetNormalizedPoints();
                m_ScatterPoints.SetNormalizedPoints(normalized);
                m_ScatterPoints.MarkDataDirty();
            }
            else if (m_ScatterPoints != null)
            {
                m_ScatterPoints.SetNormalizedPoints(null);
            }
        }

        /// <summary>设置散点数据</summary>
        /// <param name="xs">X 坐标数组</param>
        /// <param name="ys">Y 坐标数组</param>
        /// <param name="zs">Z 坐标数组</param>
        public void SetData(double[] xs, double[] ys, double[] zs)
        {
            m_DataModel.SetPoints(xs, ys, zs);
            RefreshChart();
        }

        /// <summary>设置散点数据（Vector3 数组）</summary>
        public void SetData(Vector3[] points)
        {
            var xs = new double[points.Length];
            var ys = new double[points.Length];
            var zs = new double[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                xs[i] = points[i].x;
                ys[i] = points[i].y;
                zs[i] = points[i].z;
            }
            SetData(xs, ys, zs);
        }

        /// <summary>添加一个数据点</summary>
        public void AddDataPoint(double x, double y, double z)
        {
            m_DataModel.AddPoint(x, y, z);
            RefreshChart();
        }

        /// <summary>清空所有数据</summary>
        public void ClearData()
        {
            m_DataModel.ClearAll();
            RefreshChart();
        }

        #endregion

        // ── 外观属性（运行时属性） ─────────────────────────────────

        #region 运行时属性

        public float CubeHalfSize
        {
            get => m_CubeHalfSize;
            set { m_CubeHalfSize = value; SyncCubeParams(); }
        }

        public float Pitch
        {
            get => m_Pitch;
            set { m_Pitch = value; SyncCubeParams(); }
        }

        public float Yaw
        {
            get => m_Yaw;
            set { m_Yaw = value; SyncCubeParams(); }
        }

        public Color FrontEdgeColor
        {
            get => m_FrontEdgeColor;
            set { m_FrontEdgeColor = value; SyncCubeParams(); }
        }

        public Color BackEdgeColor
        {
            get => m_BackEdgeColor;
            set { m_BackEdgeColor = value; SyncCubeParams(); }
        }

        public float EdgeThickness
        {
            get => m_EdgeThickness;
            set { m_EdgeThickness = value; SyncCubeParams(); }
        }

        public Color GridLineColor
        {
            get => m_GridLineColor;
            set { m_GridLineColor = value; SyncCubeParams(); }
        }

        public int GridDivisions
        {
            get => m_GridDivisions;
            set { m_GridDivisions = Mathf.Max(1, value); SyncCubeParams(); }
        }

        public bool ShowBackEdges
        {
            get => m_ShowBackEdges;
            set { m_ShowBackEdges = value; SyncCubeParams(); }
        }

        public bool ShowGridLines
        {
            get => m_ShowGridLines;
            set { m_ShowGridLines = value; SyncCubeParams(); }
        }

        public float PointSize
        {
            get => m_PointSize;
            set { m_PointSize = Mathf.Max(0.5f, value); SyncPointParams(); }
        }

        public PrimitiveType PointType
        {
            get => m_PointType;
            set { m_PointType = value; SyncPointParams(); }
        }

        public Color PointColor
        {
            get => m_PointColor;
            set { m_PointColor = value; SyncPointParams(); }
        }

        public int PointSideCount
        {
            get => m_PointSideCount;
            set { m_PointSideCount = Mathf.Max(3, value); SyncPointParams(); }
        }

        public float PointChamfer
        {
            get => m_PointChamfer;
            set { m_PointChamfer = Mathf.Clamp01(value); SyncPointParams(); }
        }

        public bool EnableColorMapping
        {
            get => m_EnableColorMapping;
            set { m_EnableColorMapping = value; SyncPointParams(); }
        }

        public Color ColorMapMin
        {
            get => m_ColorMapMin;
            set { m_ColorMapMin = value; SyncPointParams(); }
        }

        public Color ColorMapMax
        {
            get => m_ColorMapMax;
            set { m_ColorMapMax = value; SyncPointParams(); }
        }

        public int ColorMapAxis
        {
            get => m_ColorMapAxis;
            set { m_ColorMapAxis = Mathf.Clamp(value, 0, 2); SyncPointParams(); }
        }

        #endregion

        // ── 编辑器 ────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_GridDivisions < 1) m_GridDivisions = 1;
            if (m_PointSideCount < 3) m_PointSideCount = 3;
            if (m_PointSize < 0.5f) m_PointSize = 0.5f;
            if (m_CubeHalfSize < 10f) m_CubeHalfSize = 10f;
            if (m_ColorMapAxis < 0) m_ColorMapAxis = 0;
            if (m_ColorMapAxis > 2) m_ColorMapAxis = 2;

            // 编辑器下延迟同步，确保 Awake/Start 已执行
            if (isActiveAndEnabled && m_CubeBg != null)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null && isActiveAndEnabled)
                    {
                        SyncAllParams();
                        RefreshChart();
                    }
                };
            }
        }
#endif
    }
}