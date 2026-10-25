using System;
using UnityEngine;
using XericUI.XChart.Space;

namespace XericUI.XChart.Mesh
{
    /// <summary>
    /// 立方体<b>网格背景</b>元素（面板上叫 <c>BG Cube Grid</c>）的配置——<b>Inspector 上看到的就是这一份</b>。
    ///
    /// <para>它是<b>格线的唯一作者</b>：画面内格线、也按同一套条数发布
    /// <c>mesh.face.&lt;面号&gt;.grid.S/.T</c> 插槽给轴线跟随（规范 §22）。线框元素
    /// （<see cref="XChartCubeWireConfig"/>）只画棱、只发四角，两边不再抢同一批 id。</para>
    ///
    /// <para>它通过 <see cref="XChartElementConfig"/> 协议接进图表：图表用 [SerializeReference]
    /// 持有一张配置表，Unity 会按实际类型展开这里的字段，所以新增元素不需要写自定义 Editor。</para>
    ///
    /// <para>两份面掩码各自独立：<see cref="FillFaces"/> 管填充，<see cref="GridFaces"/> 管面内网格。
    /// 掩码不看可见性——背景网格本来就常画在背朝观察者的那几面（后墙 / 底面）。
    /// 字母契约：<b>a=前 b=后 c=上 d=下 e=左 f=右</b>。</para>
    ///
    /// <para><b>它不再持有坐标系</b>：姿态（四元数）/半边长/尺寸口径/中心偏移都归
    /// <see cref="XChartSpaceConfig"/>（空间元素）管，本配置只报出自己的<b>工具变换</b>。
    /// 投影是 <c>基变换 ∘ 工具变换</c>，所以背景、线框、散点、轴线必然落在同一个空间里。</para>
    ///
    /// <para><b>名字为什么不叫 MeshBg 了</b>：后面还要有平面坐标系网格、雷达图网格、神经图谱网格，
    /// "Mesh/背景"分不开它们；名字按<b>画的是什么几何</b>取（<c>BG Cube Grid</c>），
    /// 与 <c>BG Cube Wire</c>（线框）、将来的 <c>BG Plane Grid</c> 一类并列（他 2026-10-25 点名要求）。</para>
    /// </summary>
    [Serializable]
    [XChartElement("cube-grid", "BG Cube Grid", 10)]
    public sealed class XChartMeshBgConfig : XChartElementConfig
    {
        [Header("工具变换")]
        [SerializeField, Tooltip("背景自己的缩放/旋转/平移。默认恒等——背景就占满图的空间")]
        private XChartToolTransform m_Transform = XChartToolTransform.Identity;

        [Header("填充面")]
        [SerializeField, Tooltip("要填充哪些面；None = 不填充")]
        private XChartCubeFaceMask m_FillFaces = XChartCubeFaceMask.BackWalls;

        [SerializeField, Tooltip("填充色（面边缘）")]
        private Color m_FillColor = new Color(0.16f, 0.19f, 0.24f, 0.85f);

        [SerializeField, Tooltip("填充中心色；与边缘色不同则走径向渐变")]
        private Color m_FillCenterColor = new Color(0.16f, 0.19f, 0.24f, 0.85f);

        [Header("网格面")]
        [SerializeField, Tooltip("要在哪些面上画网格；None = 不画")]
        private XChartCubeFaceMask m_GridFaces = XChartCubeFaceMask.BackWalls;

        [SerializeField, Tooltip("网格线颜色")]
        private Color m_GridColor = new Color(0.55f, 0.6f, 0.7f, 0.35f);

        [SerializeField, Min(0.1f), Tooltip("网格线宽")]
        private float m_GridThickness = 1f;

        [SerializeField, Range(1, 50), Tooltip("每方向的网格分格数（统一值；下面 S/T 给了正数就按各自的来）。" +
                                              "绑了维度时它当\"目标刻度条数\"")]
        private int m_GridDivisions = 5;

        [SerializeField, Min(0), Tooltip("S 方向分格数（0＝跟随上面的统一值）。等分时\"沿 S 的线由 T 的分格数决定\"" +
                                 "（两条边都算，所以 N 段出 N+1 条）；绑了维度时这个数是那一族线的目标刻度条数")]
        private int m_GridDivisionsS;

        [SerializeField, Min(0), Tooltip("T 方向分格数（0＝跟随统一值）")]
        private int m_GridDivisionsT;

        [SerializeField, Tooltip("沿 S 的格线绑哪一维（-1＝不绑、等分）：绑了就用该维数据范围算线位置，轴线可按发布的线跟随")]
        private int m_GridDimensionS = -1;

        [SerializeField, Tooltip("沿 T 的格线绑哪一维（-1＝不绑、等分）")]
        private int m_GridDimensionT = -1;

        [SerializeField, Tooltip("是否同时描出面轮廓")]
        private bool m_ShowFaceOutline = true;

        [SerializeField, Tooltip("只画朝向观察者的面（关掉才会画后墙/底面这类背朝观察者的面）")]
        private bool m_OnlyVisibleFaces;

        /// <summary>元素在 ToolsRoot 下的名字</summary>
        public override string ElementName => "BG Cube Grid";

        // ── 给脚本用的入口（默认长什么样由预设说，不在图上再存一份字段）──────────
        // 这些就是上面序列化字段的读写口，不是第二份状态：配置本身是权威，工具在 ApplyTo 时读它。

        /// <summary>填哪些面（None＝不填）</summary>
        public XChartCubeFaceMask FillFaces { get => m_FillFaces; set => m_FillFaces = value; }

        /// <summary>在哪些面上画格线（None＝不画）</summary>
        public XChartCubeFaceMask GridFaces { get => m_GridFaces; set => m_GridFaces = value; }

        /// <summary>只画朝向观察者的面</summary>
        public bool OnlyVisibleFaces { get => m_OnlyVisibleFaces; set => m_OnlyVisibleFaces = value; }

        /// <summary>描面轮廓：图里已经有 <c>BG Cube Wire</c> 时应当关掉，否则同一条边被描两遍</summary>
        public bool ShowFaceOutline { get => m_ShowFaceOutline; set => m_ShowFaceOutline = value; }

        /// <summary>每方向分格数（统一值）</summary>
        public int GridDivisions { get => m_GridDivisions; set => m_GridDivisions = Mathf.Max(1, value); }

        /// <summary>S 方向分格数（0＝跟随统一值）</summary>
        public int GridDivisionsS { get => m_GridDivisionsS; set => m_GridDivisionsS = Mathf.Max(0, value); }

        /// <summary>T 方向分格数（0＝跟随统一值）</summary>
        public int GridDivisionsT { get => m_GridDivisionsT; set => m_GridDivisionsT = Mathf.Max(0, value); }

        /// <summary>格线颜色</summary>
        public Color GridColor { get => m_GridColor; set => m_GridColor = value; }

        /// <summary>格线线宽</summary>
        public float GridThickness { get => m_GridThickness; set => m_GridThickness = Mathf.Max(0.1f, value); }

        /// <summary>沿 S 的格线绑哪一维（&lt;0＝不绑、等分）</summary>
        public int GridDimensionS { get => m_GridDimensionS; set => m_GridDimensionS = value; }

        /// <summary>沿 T 的格线绑哪一维（&lt;0＝不绑、等分）</summary>
        public int GridDimensionT { get => m_GridDimensionT; set => m_GridDimensionT = value; }

        public override XChartToolBase CreateTool() => new ChartToolMeshBg();

        public override void ApplyTo(XChartToolBase tool)
        {
            if (!(tool is ChartToolMeshBg meshBg)) return;

            // 投影不在这里：背景只报出自己的工具变换，基变换在空间元素那份配置里
            meshBg.Transform = m_Transform;

            meshBg.FillFaces = m_FillFaces;
            meshBg.FillColor = m_FillColor;
            meshBg.FillCenterColor = m_FillCenterColor;

            meshBg.GridFaces = m_GridFaces;
            meshBg.GridColor = m_GridColor;
            meshBg.GridThickness = m_GridThickness;
            meshBg.GridDivisions = m_GridDivisions;
            meshBg.GridDivisionsS = m_GridDivisionsS;
            meshBg.GridDivisionsT = m_GridDivisionsT;
            meshBg.GridDimensionS = m_GridDimensionS;
            meshBg.GridDimensionT = m_GridDimensionT;
            meshBg.ShowFaceOutline = m_ShowFaceOutline;
            meshBg.OnlyVisibleFaces = m_OnlyVisibleFaces;

            meshBg.MarkDirty();
        }

        public override void CaptureFrom(XChartToolBase tool)
        {
            if (!(tool is ChartToolMeshBg meshBg)) return;

            m_Transform = meshBg.Transform;
        }
    }
}