using UnityEngine;
using XericLibrary.Runtime.UIGraph;
using PrimitiveType = XericLibrary.Runtime.UIGraph.PrimitiveType;
using XericUI.Core;
using XericUI.XChart.Mesh;
using XericUI.XChart.Space;

namespace XericUI.XChart.Pseudo3D
{
    /// <summary>
    /// 伪 3D 散点图主组件。
    ///
    /// <para><b>这张图自己不画任何东西</b>（规范 §22）：立方体线框、面内格线、散点都是「工具」，
    /// 参数在面板「工具」下的那一块块里。立方体三件（空间 / 线框 / 网格）在元素表空时由
    /// <see cref="DeclareDefaultElements"/> 补成默认元素——所以新挂一张图就是能用的样子，
    /// 而每一块都能被单独删掉、复制、改序。</para>
    ///
    /// 使用方式：
    ///   1. 在场景中创建空 GameObject，挂载本组件
    ///   2. 设置数据：chart.SetData(xs, ys, zs)
    ///   3. 外观参数在面板「工具」下改（旧版这里是「基础 → 立方体外观」，那份字段已删）
    ///
    /// 子节点结构（工具根节点下，名字＝元素名）：
    ///   [XChartTools]
    ///     ├─ Space                 ← 基变换（四元数 + 半边长）
    ///     ├─ BG Cube Wire          ← 12 条棱；发布 mesh.face.* 四角
    ///     ├─ BG Cube Grid          ← 面填充 + 面内格线；格线的唯一作者（也发布 .grid.S/.T）
    ///     └─ ChartTool_ScatterPoints
    ///          └─ PointRenderer (XScatterPointRenderer)
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.Chart + "/Pseudo3D/Pseudo3D Scatter Chart")]
    [ExecuteAlways]
    public class XPseudo3DScatterChart : XChartBase
    {
        // ── 串行化字段（Inspector 可编辑） ────────────────────────
        //
        // 「立方体外观」那组字段以前住在这里（半边长 / 四元数 / 尺寸口径 / 前后棱颜色 / 格线颜色 /
        // 分格数 S,T / 两个显示开关）。它们是**渲染参数**，挂在图上就有两个毛病：面板里它不在
        // 「工具」下、换一张图不能复用。现在它们各自是元素表里的一条配置
        // （<see cref="XChartSpaceConfig"/> / <see cref="XChartCubeWireConfig"/> /
        // <see cref="XChartMeshBgConfig"/>），默认值由 <see cref="DeclareDefaultElements"/> 给。

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

        private ChartToolScatterPoints m_ScatterPoints;

        // ── 数据模型 ──────────────────────────────────────────────

        private XChartDataModel3D m_DataModel = new XChartDataModel3D();

        /// <summary>3D 数据模型（只读）</summary>
        public XChartDataModel3D DataModel3D => m_DataModel;

        // ── 默认元素 ──────────────────────────────────────────────

        /// <summary>
        /// 立方体这三件（坐标系 / 线框 / 网格）默认就是图的一部分：元素表空着时补上，
        /// 补完之后它们就在面板「工具」下，可以单独删、单独调、可以复制一份变成两个背景。
        ///
        /// <para><b>网格那份的默认值＝旧版「立方体外观」那组字段的默认行为</b>：不填面（填充是网格元素
        /// 自己的可选项，旧图没有）、六面都画格线但只画朝向观察者的那几面（＝旧线框工具的画法）、
        /// 不描面轮廓（轮廓由线框那 12 条棱管，描两遍只会让边变粗）。
        /// 其余参数取各类配置自己的默认值，所以新挂一张图的样子和以前一致。</para>
        /// </summary>
        protected override void DeclareDefaultElements()
        {
            AddDefaultElement(new XChartSpaceConfig { OrderIndex = -1000 });
            AddDefaultElement(new XChartCubeWireConfig { OrderIndex = 0, DrawOrder = 0 });
            AddDefaultElement(new XChartMeshBgConfig
            {
                OrderIndex = 1,
                DrawOrder = 1,
                FillFaces = XChartCubeFaceMask.None,
                GridFaces = XChartCubeFaceMask.All,
                OnlyVisibleFaces = true,
                ShowFaceOutline = false,
            });
        }

        /// <summary>
        /// 元素表里某一类配置的第一个实例（脚本要改外观参数就走它，不再自带一份字段——
        /// 两处存同一件事就是"改了没反应"的来路）。没有就返回 null，调用方不当没发生也不崩。
        /// </summary>
        private T ElementOf<T>() where T : XChartElementConfig
        {
            return XChartElementTable.FirstOfKind(Elements, typeof(T)) as T;
        }

        // ── 初始化 ────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureToolsCreated();
            // 维度定义要在<b>没有数据的时候</b>就存在：面板的"数据"板块是按维度逐行给的，
            // 只在第一次喂点时定义维度，就会出现"图里还没数据 ⇒ 一行都没有、无处填范围"
            // （P4 验收现场："没有看到维度数据行"就是这一条）
            EnsureSharedDimensions();
        }

        private void Start()
        {
            EnsureToolsCreated();
            SyncAllParams();
            RefreshChart();
        }

        private void EnsureToolsCreated()
        {
            if (m_ScatterPoints != null) return;

            // 散点是"这张图必需的那个读数据的工具"，所以走脚本注册（OrderIndex 10，画在背景之上）。
            // 立方体的三件（空间/线框/网格）不在这里：它们是元素表里的配置，面板上看得见能改。
            m_ScatterPoints = AddTool<ChartToolScatterPoints>("ChartTool_ScatterPoints", 10);
        }

        // ── 参数同步 ──────────────────────────────────────────────

        private void SyncAllParams()
        {
            SyncPointParams();
        }

        private void SyncPointParams()
        {
            if (m_ScatterPoints == null) return;
            m_ScatterPoints.PointType = m_PointType;
            m_ScatterPoints.PointSizeMode = m_PointSizeMode;
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
            m_ScatterPoints.ColorMapDimension = m_ColorMapAxis;
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

        /// <summary>
        /// 刷新图表：让散点重读数据源、让空间重投影。
        /// <b>点现在是从图共享的序列化数据源里读的</b>（散点元素自己按维度下标 + 聚合范围归一化），
        /// 所以存进预制体的数据重开预制体后还在——以前读的是本组件那份非序列化的 3D 模型，一重载就空。
        /// </summary>
        public void RefreshChart()
        {
            EnsureToolsCreated();

            var space = Space;
            if (space != null) space.MarkProjectionDirty();

            if (m_ScatterPoints != null)
            {
                m_ScatterPoints.AutoFromData = true;
                m_ScatterPoints.MarkDataDirty();
            }
        }

        /// <summary>设置散点数据</summary>
        /// <param name="xs">X 坐标数组</param>
        /// <param name="ys">Y 坐标数组</param>
        /// <param name="zs">Z 坐标数组</param>
        public void SetData(double[] xs, double[] ys, double[] zs)
        {
            m_DataModel.SetPoints(xs, ys, zs);
            SetSharedData(xs, ys, zs);
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
            EnsureSharedDimensions();
            DataModel.TryAddRecord(m_DataModel.PointCount, new[] { (float)x, (float)y, (float)z });
            RefreshChart();
        }

        private void SetSharedData(double[] xs, double[] ys, double[] zs)
        {
            EnsureSharedDimensions();
            DataModel.Clear();
            int count = System.Math.Min(System.Math.Min(xs.Length, ys.Length), zs.Length);
            for (int i = 0; i < count; i++)
                DataModel.TryAddRecord(i, new[] { (float)xs[i], (float)ys[i], (float)zs[i] });
        }

        private void EnsureSharedDimensions()
        {
            if (DataModel.NumericDimensionCount == 3) return;
            DataModel.SetDimensions(new System.Collections.Generic.List<XChartDimensionDefinition>
            {
                new XChartDimensionDefinition("x", "X", XChartDimensionKind.Number),
                new XChartDimensionDefinition("y", "Y", XChartDimensionKind.Number),
                new XChartDimensionDefinition("z", "Z", XChartDimensionKind.Number),
            });
        }

        /// <summary>清空所有数据</summary>
        public void ClearData()
        {
            m_DataModel.ClearAll();
            DataModel.Clear();
            RefreshChart();
        }

        #endregion

        // ── 外观属性（转发到元素表） ───────────────────────────────

        #region 运行时属性

        /// <summary>
        /// 这一组属性以前<b>各配一个序列化字段</b>，再由 <c>SyncCubeParams</c> 灌进工具：
        /// 于是"面板上改元素"与"脚本改属性"写的是两处状态，谁后跑谁赢——他报的"勾选了反而消失"
        /// 就是这类结构的一个面（规范 §22）。现在读写的是<b>元素表里那份配置本身</b>：
        /// 脚本、面板、预制体看到的是同一个数，没有第二份需要同步。
        /// </summary>
        private void PushCubeLook()
        {
            ReapplyElements();
            ForceRefresh();
        }

        /// <summary>
        /// 坐标系元素（姿态 / 半边长 / 尺寸口径的权威）。基类保证图里有一个 <see cref="XChartBase.Space"/>
        /// 工具，但<b>配置</b>可以整条删掉——删了这里就是 null：getter 给中性值、setter 什么都不做，
        /// 既不抛也不背着他造一个元素回来。
        /// </summary>
        public XChartSpaceConfig SpaceElement => ElementOf<XChartSpaceConfig>();

        /// <summary>线框元素（前后棱颜色 / 棱宽 / 后向棱开关的权威）</summary>
        public XChartCubeWireConfig WireElement => ElementOf<XChartCubeWireConfig>();

        /// <summary>网格元素（格线颜色 / 分格数 / 画哪些面的权威）</summary>
        public XChartMeshBgConfig GridElement => ElementOf<XChartMeshBgConfig>();

        public float CubeHalfSize
        {
            get { var e = SpaceElement; return e != null ? e.HalfEdge : 0f; }
            set { var e = SpaceElement; if (e == null) return; e.HalfEdge = value; PushCubeLook(); }
        }

        /// <summary>姿态四元数——唯一的旋转权威量（没有相机位置可调）</summary>
        public Quaternion Rotation
        {
            get { var e = SpaceElement; return e != null ? e.Rotation : Quaternion.identity; }
            set { var e = SpaceElement; if (e == null) return; e.Rotation = value; PushCubeLook(); }
        }

        /// <summary>一步跳到某个预设视角，等价于把那个四元数写进 <see cref="Rotation"/></summary>
        public void ApplyViewPreset(XChartCubeViewPreset preset)
        {
            var e = SpaceElement;
            if (e == null) return;
            e.ApplyViewPreset(preset);
            PushCubeLook();
        }

        /// <summary>
        /// 立方体尺寸口径（默认半边长：立方体大小不随旋转变化）。
        /// 名字必须带 Cube 前缀：本类还有散点用的 <c>SizeMode</c>（UIGraph 那个类型），
        /// 同名会让散点那份字段的初始化器把类型名解成这个属性（CS0236）。
        /// </summary>
        public XChartCubeSizeMode CubeSizeMode
        {
            get { var e = SpaceElement; return e != null ? e.SizeMode : XChartCubeSizeMode.HalfEdgeLength; }
            set { var e = SpaceElement; if (e == null) return; e.SizeMode = value; PushCubeLook(); }
        }

        public Color FrontEdgeColor
        {
            get { var e = WireElement; return e != null ? e.FrontEdgeColor : Color.clear; }
            set { var e = WireElement; if (e == null) return; e.FrontEdgeColor = value; PushCubeLook(); }
        }

        public Color BackEdgeColor
        {
            get { var e = WireElement; return e != null ? e.BackEdgeColor : Color.clear; }
            set { var e = WireElement; if (e == null) return; e.BackEdgeColor = value; PushCubeLook(); }
        }

        public float EdgeThickness
        {
            get { var e = WireElement; return e != null ? e.EdgeThickness : 0f; }
            set { var e = WireElement; if (e == null) return; e.EdgeThickness = value; PushCubeLook(); }
        }

        public bool ShowBackEdges
        {
            get { var e = WireElement; return e != null && e.ShowBackEdges; }
            set { var e = WireElement; if (e == null) return; e.ShowBackEdges = value; PushCubeLook(); }
        }

        /// <summary>格线颜色（旧名 GridLineColor，现归网格元素）</summary>
        public Color GridLineColor
        {
            get { var e = GridElement; return e != null ? e.GridColor : Color.clear; }
            set { var e = GridElement; if (e == null) return; e.GridColor = value; PushCubeLook(); }
        }

        public int GridDivisions
        {
            get { var e = GridElement; return e != null ? e.GridDivisions : 0; }
            set { var e = GridElement; if (e == null) return; e.GridDivisions = value; PushCubeLook(); }
        }

        /// <summary>S 方向分格数（0＝跟随统一值）。按方向分是为了让两条轴各自都能和格线对齐</summary>
        public int GridDivisionsS
        {
            get { var e = GridElement; return e != null ? e.GridDivisionsS : 0; }
            set { var e = GridElement; if (e == null) return; e.GridDivisionsS = value; PushCubeLook(); }
        }

        public int GridDivisionsT
        {
            get { var e = GridElement; return e != null ? e.GridDivisionsT : 0; }
            set { var e = GridElement; if (e == null) return; e.GridDivisionsT = value; PushCubeLook(); }
        }

        /// <summary>
        /// 格线开关的旧写法。<b>它现在的真身是网格元素的 <see cref="XChartMeshBgConfig.GridFaces"/> 掩码</b>：
        /// 开＝六面都画，关＝一面都不画。以前图上这个 bool 和线框里的另一套开关并存，
        /// 两边都往同一批格线插槽上发布，才有"关掉一边另一边又亮回来"（规范 §22）。
        /// 只画后墙那类"部分面"请直接改掩码。
        /// </summary>
        public bool ShowGridLines
        {
            get { var e = GridElement; return e != null && e.GridFaces != XChartCubeFaceMask.None; }
            set
            {
                var e = GridElement;
                if (e == null) return;
                e.GridFaces = value ? XChartCubeFaceMask.All : XChartCubeFaceMask.None;
                PushCubeLook();
            }
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

        /// <summary>
        /// 必须 override 基类的 OnValidate 并调 base——以前这里写的是 <c>private void OnValidate()</c>，
        /// 它把基类那份遮掉了，导致基类的"重灌元素 + 强制刷新"对这个组件从未执行。
        /// 本图还没迁到 <c>OnChartRefresh</c>，所以自己那套参数同步仍在自己的 delayCall 里推。
        /// </summary>
        protected override void OnValidate()
        {
            if (m_PointSideCount < 3) m_PointSideCount = 3;
            if (m_PointSize < 0.5f) m_PointSize = 0.5f;
            if (m_ColorMapAxis < 0) m_ColorMapAxis = 0;
            if (m_ColorMapAxis > 2) m_ColorMapAxis = 2;

            base.OnValidate();

#if UNITY_EDITOR
            // 编辑器下延迟一拍再同步，确保 Awake/OnEnable 已执行
            // （立方体那几件不归这里管了：基类的 base.OnValidate 已经"重灌元素 + 强制刷新"过）
            if (isActiveAndEnabled)
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
#endif
        }
    }
}
