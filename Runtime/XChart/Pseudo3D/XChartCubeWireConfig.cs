using System;
using UnityEngine;
using XericUI.XChart.Mesh;
using XericUI.XChart.Space;

namespace XericUI.XChart.Pseudo3D
{
    /// <summary>
    /// 立方体<b>线框</b>元素的配置（面板上叫 <c>BG Cube Wire</c>）。
    ///
    /// <para><b>它只管 12 条棱</b>：前向一套色、后向一套色，后向棱可整体关掉。
    /// 面内格线不在这里——那是 <see cref="XChartMeshBgConfig"/>（<c>BG Cube Grid</c>）的活。
    /// 上一版两边都能画格线、还都往 <c>mesh.face.*.grid.S/.T</c> 同一批插槽上发布，
    /// 于是"关掉一边另一边又亮回来"＋"改完别的再刷新一下这边的状态又反了"
    /// （他 2026-10-25 现场报的两条症状，同一个结构问题的两个面，规范 §22）。</para>
    ///
    /// <para>姿态/半边长/中心偏移仍归 <see cref="XChartSpaceConfig"/>：投影是
    /// <c>基变换 ∘ 工具变换</c>，这里只出自己的工具变换。</para>
    ///
    /// <para><b>为什么单独立一个元素配置</b>：这些旋钮原先是 <c>XPseudo3DScatterChart</c> 的
    /// 图级字段（面板「基础 → 立方体外观」）。渲染参数挂在图上，就有两个后果——
    /// 面板里它不在「工具」下（"既然是工具就要放在工具下面"），以及换一张图/加第二个背景时
    /// 那份旋钮并不能复用。搬进元素表后，参数随元素走、脚本注册与配置注册同一条路（规范 §22）。</para>
    /// </summary>
    [Serializable]
    [XChartElement("cube-wire", "BG Cube Wire", 9)]
    public sealed class XChartCubeWireConfig : XChartElementConfig
    {
        [Header("工具变换")]
        [SerializeField, Tooltip("线框自己的缩放/旋转/平移。默认恒等——线框就占满图的空间")]
        private XChartToolTransform m_Transform = XChartToolTransform.Identity;

        [Header("棱线")]
        [SerializeField, Tooltip("前向棱颜色（至少有一端面朝向观察者）")]
        private Color m_FrontEdgeColor = new Color(0.7f, 0.7f, 0.7f, 1f);

        [SerializeField, Tooltip("后向棱颜色（两面都背朝观察者的棱）；透明度低一点就是\"透过去看到的淡线\"")]
        private Color m_BackEdgeColor = new Color(0.4f, 0.4f, 0.4f, 0.4f);

        [SerializeField, Min(0.1f), Tooltip("棱线宽度（像素）")]
        private float m_EdgeThickness = 2f;

        [SerializeField, Tooltip("显示后向棱：关掉就只剩看得见的那一面外壳")]
        private bool m_ShowBackEdges = true;

        /// <summary>元素在 ToolsRoot 下的名字</summary>
        public override string ElementName => "BG Cube Wire";

        // 脚本入口：写的就是下面这些序列化字段（配置是权威，工具在 ApplyTo 里读它），
        // 所以不存在"脚本改了一份、面板显示另一份"
        /// <summary>前向棱颜色</summary>
        public Color FrontEdgeColor { get => m_FrontEdgeColor; set => m_FrontEdgeColor = value; }

        /// <summary>后向棱颜色</summary>
        public Color BackEdgeColor { get => m_BackEdgeColor; set => m_BackEdgeColor = value; }

        /// <summary>棱线宽度</summary>
        public float EdgeThickness { get => m_EdgeThickness; set => m_EdgeThickness = Mathf.Max(0.1f, value); }

        /// <summary>是否显示后向棱</summary>
        public bool ShowBackEdges { get => m_ShowBackEdges; set => m_ShowBackEdges = value; }

        public override XChartToolBase CreateTool() => new ChartToolCubeBg();

        public override void ApplyTo(XChartToolBase tool)
        {
            if (!(tool is ChartToolCubeBg wire)) return;

            wire.Transform = m_Transform;
            wire.FrontEdgeColor = m_FrontEdgeColor;
            wire.BackEdgeColor = m_BackEdgeColor;
            wire.EdgeThickness = m_EdgeThickness;
            wire.ShowBackEdges = m_ShowBackEdges;

            wire.MarkDirty();
        }

        public override void CaptureFrom(XChartToolBase tool)
        {
            if (!(tool is ChartToolCubeBg wire)) return;

            m_Transform = wire.Transform;
            m_FrontEdgeColor = wire.FrontEdgeColor;
            m_BackEdgeColor = wire.BackEdgeColor;
            m_EdgeThickness = wire.EdgeThickness;
            m_ShowBackEdges = wire.ShowBackEdges;
        }
    }
}
