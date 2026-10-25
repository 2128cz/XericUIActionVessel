using UnityEngine;
using XericUI.Core;
using XericUI.XChart.Axis;
using XericUI.XChart.Mesh;
using XericUI.XChart.Pseudo3D;

namespace XericUI.XChart.Products
{
    /// <summary>
    /// 网格渲染预览图：<b>用来验证立方体网格背景（填充 / 面内网格 / 面控制 / 3D 旋转）
    /// 以及后续同类网格渲染器（圆柱等）的观感</b>。
    ///
    /// <para><b>它示范的是"脚本注册"这条路</b>：三个工具全部在 <see cref="RegisterTools"/> 里登记，
    /// 没有用 Inspector 的元素表——固定的调试组合用这种写法最省事。生产用的成品图应当走
    /// 元素配置（<see cref="XChartElementConfig"/>），那样才可在 Inspector 里增删改。</para>
    ///
    /// <para><b>验证要点</b></para>
    /// <list type="bullet">
    /// <item>面掩码：<c>a=前 b=后 c=上 d=下 e=左 f=右</c>，填充与网格各一份（互不影响）。</item>
    /// <item>默认画的是<b>背朝观察者</b>的后墙/底面（背景墙观感）；勾 <see cref="m_OnlyVisibleFaces"/>
    /// 切成"实体盒子"观感，可来回对照。</item>
    /// <item>半透明填充按面心深度从远到近提交，改 alpha 能看出叠色顺序对不对。</item>
    /// <item>散点走的是线框工具的投影，两者的四元数姿态/半边长/尺寸口径由本组件统一推下去，
    /// 所以点会落在盒子里。默认视角是 <see cref="XChartCubeViewPreset.Isometric"/>
    /// （45° 正交，整个立方体的轮廓是一个<b>正六边形</b>）。</item>
    /// </list>
    ///
    /// <para><b>编辑器里就要能看到效果</b>：基类是 [ExecuteAlways]，改本组件的旋钮会延迟一拍
    /// 直接生效。注意编辑器静止时 Unity 不重绘 SceneView，LateUpdate 不一定跑——动一下鼠标/平移视图
    /// 就会刷新，这不是坏了。</para>
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.Chart + "/Products/网格渲染预览图")]
    [ExecuteAlways]
    public class XMeshPreviewChart : XChartBase
    {
        // ── 空间（3D 旋转 + 投影） ────────────────────────────────

        [Header("空间")]
        [SerializeField, Min(1f), Tooltip("立方体半边长（UI 像素）：真实边长的一半。" +
            "外接球半径（顶点到中心）= √3 倍，等距轮廓正六边形的外接圆 = √(8/3) 倍")]
        private float m_HalfSize = 100f;

        [SerializeField, Tooltip("视角预设：<b>一次性动作</b>。选它就把预设四元数写进 Rotation，然后回到\"手动\"；渲染只读 Rotation")]
        private XChartCubeViewPreset m_ViewPreset = XChartCubeViewPreset.Manual;

        [SerializeField, Tooltip("姿态四元数（x,y,z,w）——唯一的旋转权威量，没有相机位置可调")]
        private Quaternion m_Rotation = XChartCubeView.RotationOf(XChartCubeViewPreset.Isometric);

        [SerializeField, Tooltip("尺寸口径：半边长（尺度与旋转无关，默认）/ 视口适配（铺满固定矩形，转起来会变小）")]
        private XChartCubeSizeMode m_SizeMode = XChartCubeSizeMode.HalfEdgeLength;

        // ── 填充 ──────────────────────────────────────────────────

        [Header("填充面")]
        [SerializeField, Tooltip("要填充哪些面；None = 不填充")]
        private XChartCubeFaceMask m_FillFaces = XChartCubeFaceMask.BackWalls;

        [SerializeField, Tooltip("填充色（面边缘）")]
        private Color m_FillColor = new Color(0.16f, 0.19f, 0.24f, 0.85f);

        [SerializeField, Tooltip("填充中心色；与边缘色不同则走径向渐变")]
        private Color m_FillCenterColor = new Color(0.28f, 0.34f, 0.42f, 0.85f);

        [SerializeField, Tooltip("只画朝向观察者的面（关掉才会画后墙/底面这类背朝观察者的面）")]
        private bool m_OnlyVisibleFaces;

        // ── 面内网格 ──────────────────────────────────────────────

        [Header("网格面")]
        [SerializeField, Tooltip("要在哪些面上画网格；None = 不画")]
        private XChartCubeFaceMask m_GridFaces = XChartCubeFaceMask.BackWalls;

        [SerializeField, Tooltip("网格线颜色")]
        private Color m_GridColor = new Color(0.55f, 0.6f, 0.7f, 0.4f);

        [SerializeField, Min(0.1f), Tooltip("网格线宽")]
        private float m_GridThickness = 1f;

        [SerializeField, Range(1, 50), Tooltip("每个方向的网格分割数")]
        private int m_GridDivisions = 5;

        [SerializeField, Tooltip("是否同时描出面轮廓")]
        private bool m_ShowFaceOutline = true;

        // ── 线框 ──────────────────────────────────────────────────

        [Header("线框")]
        [SerializeField, Tooltip("是否显示立方体 12 棱线框")]
        private bool m_ShowWireframe = true;

        [SerializeField, Tooltip("前向棱颜色")]
        private Color m_FrontEdgeColor = new Color(0.75f, 0.8f, 0.9f, 0.9f);

        [SerializeField, Tooltip("后向棱颜色")]
        private Color m_BackEdgeColor = new Color(0.5f, 0.55f, 0.65f, 0.35f);

        [SerializeField, Min(0.1f), Tooltip("棱线宽")]
        private float m_EdgeThickness = 2f;

        // ── 示例散点 ──────────────────────────────────────────────

        [Header("示例散点")]
        [SerializeField, Tooltip("是否显示示例散点")]
        private bool m_ShowSamplePoints = true;

        [SerializeField, Range(0, 2000), Tooltip("示例点数量")]
        private int m_SamplePointCount = 240;

        [SerializeField, Min(0.5f), Tooltip("点大小（像素）")]
        private float m_PointSize = 6f;

        [SerializeField, Tooltip("点颜色")]
        private Color m_PointColor = new Color(0.34f, 0.72f, 1f, 1f);

        [SerializeField, Tooltip("点边框颜色")]
        private Color m_PointBorderColor = new Color(0.08f, 0.12f, 0.2f, 1f);

        // ── 轴线 / 体积元素（同一空间的其它消费者） ───────────────

        [Header("轴线")]
        [SerializeField, Tooltip("显示 X / Y / Z 三条轴线元素")]
        private bool m_ShowAxes = true;

        [SerializeField, Tooltip("刻度标签用数值直驱时的格式化串；留空＝自适应")]
        private string m_AxisLabelFormat;

        [Header("圆柱 / 箭头")]
        [SerializeField, Tooltip("显示一根斜圆柱（两端不重合，验斜圆柱也走同一套环几何）")]
        private bool m_ShowCylinder;

        [SerializeField, Range(3, 128), Tooltip("圆柱分段数")]
        private int m_CylinderSegments = 24;

        [SerializeField, Min(0.01f), Tooltip("圆柱半径（立方体单位）")]
        private float m_CylinderRadius = 0.22f;

        [SerializeField, Tooltip("显示向量箭头（数据里每条记录的后三维当方向）")]
        private bool m_ShowArrows;

        // ── 工具引用 ──────────────────────────────────────────────

        private ChartToolMeshBg m_MeshBg;
        private ChartToolCubeBg m_Wireframe;
        private ChartToolScatterPoints m_Points;
        private ChartToolAxis m_AxisX;
        private ChartToolAxis m_AxisY;
        private ChartToolAxis m_AxisZ;
        private ChartToolCylinder m_Cylinder;
        private ChartToolVectorArrows m_Arrows;

        private Vector3[] m_SamplePoints;

        /// <summary>
        /// 当前姿态（渲染唯一权威）。预设选择器是一次性的：非"手动"时把预设四元数写进
        /// <see cref="m_Rotation"/> 并复位选择器，之后 Rotation 想怎么拧就怎么拧。
        /// </summary>
        public Quaternion Rotation
        {
            get => m_Rotation;
            set { m_Rotation = value; m_ViewPreset = XChartCubeViewPreset.Manual; ApplySpace(); }
        }

        private Quaternion ConsumeViewPreset()
        {
            if (m_ViewPreset == XChartCubeViewPreset.Manual) return m_Rotation;
            m_Rotation = XChartCubeView.RotationOf(m_ViewPreset);
            m_ViewPreset = XChartCubeViewPreset.Manual;
            return m_Rotation;
        }

        /// <summary>★ 脚本注册：本图要用哪些工具都在这里登记（唯一的集中注册点）</summary>
        protected override void RegisterTools()
        {
            // 空间工具（投影权威）由基类保证存在，这里不重复登记
            // 背景最先画（负序号），线框、轴线、体积元素、散点依次压上去
            // 工具名＝元素显示名（<c>BG Cube Grid</c> / <c>BG Cube Wire</c>）：面板的「脚本注册的工具」
            // 就是照这个名字点名的，两边一个名字才认得出是同一个东西
            m_MeshBg = RegisterTool<ChartToolMeshBg>("BG Cube Grid", -10);
            m_Wireframe = RegisterTool<ChartToolCubeBg>("BG Cube Wire", 0);

            m_AxisX = RegisterTool<ChartToolAxis>("AxisX", 5);
            m_AxisY = RegisterTool<ChartToolAxis>("AxisY", 6);
            m_AxisZ = RegisterTool<ChartToolAxis>("AxisZ", 7);
            ConfigureAxes();

            m_Cylinder = RegisterTool<ChartToolCylinder>("Cylinder", 8);
            m_Points = RegisterTool<ChartToolScatterPoints>("SamplePoints", 10);
            m_Arrows = RegisterTool<ChartToolVectorArrows>("Arrows", 11);
        }

        /// <summary>三条轴各挑自己的面 / 方向 / 维度；它们共用同一个空间，所以不必彼此协调</summary>
        private void ConfigureAxes()
        {
            if (m_AxisX != null)
            {
                m_AxisX.SlotId = XChartCubeGeometry.FaceSlotId(XChartCubeGeometry.CubeFace.Bottom);
                m_AxisX.Direction = XChartAxisDirection.AlongS;
                m_AxisX.Dimension = 0;
            }
            if (m_AxisY != null)
            {
                m_AxisY.SlotId = XChartCubeGeometry.FaceSlotId(XChartCubeGeometry.CubeFace.Bottom);
                m_AxisY.Direction = XChartAxisDirection.AlongT;
                m_AxisY.Dimension = 1;
            }
            if (m_AxisZ != null)
            {
                m_AxisZ.SlotId = XChartCubeGeometry.FaceSlotId(XChartCubeGeometry.CubeFace.Left);
                m_AxisZ.Direction = XChartAxisDirection.AlongT;
                m_AxisZ.Dimension = 2;
            }
        }

        protected override void OnChartInitialized() => ApplySettings();

        /// <summary>刷新时把本组件的旋钮推给工具（正常刷新与强制刷新共用这一条）</summary>
        protected override void OnChartRefresh(bool force) => ApplySettings();

        protected override void OnResetRender()
        {
            // 渲染重置：让示例点重新生成一遍（验证重排）
            m_SamplePoints = null;
        }

        protected override void OnChartValidated() => ApplySettings();

        private void ApplySettings()
        {
            ApplySpace();
            ApplyMeshBg();
            ApplyWireframe();
            ApplyAxes();
            ApplyVolume();
            ApplyPoints();
        }

        private void ApplyAxes()
        {
            ApplyOneAxis(m_AxisX, m_AxisLabelFormat);
            ApplyOneAxis(m_AxisY, m_AxisLabelFormat);
            ApplyOneAxis(m_AxisZ, m_AxisLabelFormat);
        }

        private void ApplyOneAxis(ChartToolAxis axis, string format)
        {
            if (axis == null) return;
            axis.SetVisible(m_ShowAxes);
            axis.LabelFormat = format;
            axis.MarkDirty();
        }

        private void ApplyVolume()
        {
            if (m_Cylinder != null)
            {
                m_Cylinder.SetVisible(m_ShowCylinder);
                m_Cylinder.SegmentCount = m_CylinderSegments;
                m_Cylinder.Radius = m_CylinderRadius;
                m_Cylinder.MarkDirty();
            }
            if (m_Arrows != null)
            {
                m_Arrows.SetVisible(m_ShowArrows);
                m_Arrows.MarkDirty();
            }
        }

        private void ApplySpace()
        {
            var space = Space;
            if (space != null)
            {
                space.Rotation = ConsumeViewPreset();
                space.HalfEdge = m_HalfSize;
                space.SizeMode = m_SizeMode;
                space.MarkProjectionDirty();
            }

            // 背景和线框默认恒等变换，和空间严丝合缝；散点自己也没额外偏移
            if (m_MeshBg != null) m_MeshBg.MarkDirty();
            if (m_Wireframe != null) m_Wireframe.MarkDirty();
            if (m_Points != null) m_Points.MarkDataDirty();
        }

        private void ApplyMeshBg()
        {
            if (m_MeshBg == null) return;

            m_MeshBg.FillFaces = m_FillFaces;
            m_MeshBg.FillColor = m_FillColor;
            m_MeshBg.FillCenterColor = m_FillCenterColor;
            m_MeshBg.OnlyVisibleFaces = m_OnlyVisibleFaces;

            m_MeshBg.GridFaces = m_GridFaces;
            m_MeshBg.GridColor = m_GridColor;
            m_MeshBg.GridThickness = m_GridThickness;
            m_MeshBg.GridDivisions = m_GridDivisions;
            m_MeshBg.ShowFaceOutline = m_ShowFaceOutline;
        }

        private void ApplyWireframe()
        {
            if (m_Wireframe == null) return;

            m_Wireframe.SetVisible(m_ShowWireframe);
            m_Wireframe.FrontEdgeColor = m_FrontEdgeColor;
            m_Wireframe.BackEdgeColor = m_BackEdgeColor;
            m_Wireframe.EdgeThickness = m_EdgeThickness;
            // 这里以前还写一句 m_Wireframe.ShowGridLines = false："面内网格归 MeshBg"这件事
            // 现在由结构保证（线框工具根本没有格线这条代码路径），不需要每个调用方再声明一遍
        }

        private void ApplyPoints()
        {
            if (m_Points == null) return;

            m_Points.SetVisible(m_ShowSamplePoints);
            m_Points.PointSize = m_PointSize;
            m_Points.PointColor = m_PointColor;
            m_Points.BorderColor = m_PointBorderColor;
            m_Points.PointType = XericLibrary.Runtime.UIGraph.PrimitiveType.Polygon;
            m_Points.PointSizeMode = XericLibrary.Runtime.UIGraph.SizeMode.InscribedCircle;
            m_Points.SideCount = 8;

            if (!m_ShowSamplePoints) return;

            if (m_SamplePoints == null || m_SamplePoints.Length != m_SamplePointCount)
            {
                m_SamplePoints = BuildSamplePoints(m_SamplePointCount);
                PublishSamples(m_SamplePoints);
            }

            // 走数据源那条路：点和轴线读的是同一份记录，不再各拿一份数组
            m_Points.AutoFromData = true;
            m_Points.MarkDataDirty();
        }

        /// <summary>
        /// 把示例数据登记到图共享的<b>序列化</b>数据模型里：每条记录 6 个数值维
        /// <c>[x, y, z, dx, dy, dz]</c>——前三维给散点和轴线，后三维给箭头。
        /// 存进数据模型（而不是只留在内存）才能让"重开预制体数据还在"这件事成立。
        /// </summary>
        private void PublishSamples(Vector3[] positions)
        {
            EnsureDataDimensions();
            DataModel.Clear();

            var values = new float[6];
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 p = positions[i];
                values[0] = p.x;
                values[1] = p.y;
                values[2] = p.z;
                // 方向取"绕 Y 轴切向 + 一点上抬"，看起来就是一个旋涡场
                values[3] = -p.y;
                values[4] = p.x;
                values[5] = 0.25f;
                DataModel.TryAddRecord(i, values);
            }
        }

        private void EnsureDataDimensions()
        {
            if (DataModel.NumericDimensionCount == 6) return;
            DataModel.SetDimensions(new System.Collections.Generic.List<XChartDimensionDefinition>
            {
                new XChartDimensionDefinition("x", "X", XChartDimensionKind.Number),
                new XChartDimensionDefinition("y", "Y", XChartDimensionKind.Number),
                new XChartDimensionDefinition("z", "Z", XChartDimensionKind.Number),
                new XChartDimensionDefinition("dx", "dX", XChartDimensionKind.Number),
                new XChartDimensionDefinition("dy", "dY", XChartDimensionKind.Number),
                new XChartDimensionDefinition("dz", "dZ", XChartDimensionKind.Number),
            });
        }

        /// <summary>
        /// 生成 [-1,1]³ 里的一批示例点。用固定种子的 LCG，不用 UnityEngine.Random——
        /// 编辑期每次重算必须得到同一批点，否则一改旋钮点就乱跳，没法验证。
        /// </summary>
        private static Vector3[] BuildSamplePoints(int count)
        {
            var points = new Vector3[Mathf.Max(0, count)];
            uint state = 20261021u;
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new Vector3(NextUnit(ref state), NextUnit(ref state), NextUnit(ref state));
            }
            return points;
        }

        private static float NextUnit(ref uint state)
        {
            state = state * 1664525u + 1013904223u;
            return (state >> 8) / (float)(1 << 24) * 2f - 1f;
        }
    }
}