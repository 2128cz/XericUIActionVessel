using System;
using UnityEngine;
using XericUI.XChart.Mesh;

namespace XericUI.XChart.Space
{
    /// <summary>
    /// 空间（基变换）元素的配置——<b>整张图的坐标系就是这一份</b>。
    ///
    /// <para>图表在初始化时会保证图里一定有一个 <see cref="ChartToolSpace"/>：两条注册路径
    /// 都没登记时自动补一个默认的。想调它就在层级面板里加这个元素（<b>加出来的是同一个名字
    /// "Space"，所以不会和默认的那个并存</b>），或者在脚本里直接改 <c>chart.Space</c>。</para>
    ///
    /// <para><b>渲染只看一个量：四元数 <see cref="Rotation"/></b>。没有相机、没有焦距、没有第二处
    /// 输入；尺寸也只有一个量（半边长）。理由与换算见 <see cref="XChartCubeView"/>。</para>
    ///
    /// <para><b>预设是按钮不是模式</b>：选一次预设 = 把那个四元数写进 <see cref="Rotation"/>，
    /// 选择器随即回到"手动"。所以不存在"选了预设，Rotation 字段就失效"这种事——
    /// 想在等距基础上再拧一点，直接改 Rotation 就行。</para>
    /// </summary>
    [Serializable]
    [XChartElement("space", "空间（基变换）", -1000)]
    public sealed class XChartSpaceConfig : XChartElementConfig
    {
        [Header("基变换")]
        [SerializeField, Tooltip("立方体半边长（UI 像素）：真实边长的一半，也等于面心到中心的距离")]
        private float m_HalfEdge = 120f;

        [SerializeField, Tooltip("视角预设：<b>一次性动作，不是模式</b>。选它在写入的那一刻把预设四元数灌进下面的 Rotation，" +
                                 "然后自己回到\"手动\"。渲染永远只读 Rotation。")]
        private XChartCubeViewPreset m_ViewPreset = XChartCubeViewPreset.Manual;

        [SerializeField, Tooltip("姿态四元数（x,y,z,w）——<b>唯一的旋转权威量</b>：没有相机、没有第二处输入")]
        private Quaternion m_Rotation = XChartCubeView.RotationOf(XChartCubeViewPreset.Isometric);

        [SerializeField, Tooltip("尺寸口径：半边长（尺度与旋转无关）/ 视口适配（铺满固定矩形，转起来会变小）")]
        private XChartCubeSizeMode m_SizeMode = XChartCubeSizeMode.HalfEdgeLength;

        [SerializeField, Tooltip("立方体中心在图表局部像素里的偏移")]
        private Vector2 m_CenterOffset = Vector2.zero;

        /// <summary>元素在 ToolsRoot 下的名字（和图表自动补的那个是同一个，所以不会并存）</summary>
        public override string ElementName => ChartToolSpace.DefaultToolName;

        /// <summary>当前姿态（渲染唯一权威）。写它＝放弃预设、手动定角度（与面板上直接改四元数同一条路）</summary>
        public Quaternion Rotation
        {
            get => m_Rotation;
            set { m_Rotation = value; m_ViewPreset = XChartCubeViewPreset.Manual; }
        }

        /// <summary>半边长（UI 像素）</summary>
        public float HalfEdge { get => m_HalfEdge; set => m_HalfEdge = Mathf.Max(0.01f, value); }

        /// <summary>尺寸口径（半边长 / 视口适配）</summary>
        public XChartCubeSizeMode SizeMode { get => m_SizeMode; set => m_SizeMode = value; }

        /// <summary>中心偏移（图表局部像素）</summary>
        public Vector2 CenterOffset { get => m_CenterOffset; set => m_CenterOffset = value; }

        /// <summary>一步跳到某个预设视角：等价于把那个四元数写进 <see cref="Rotation"/></summary>
        public void ApplyViewPreset(XChartCubeViewPreset preset)
        {
            if (preset == XChartCubeViewPreset.Manual) return;
            m_Rotation = XChartCubeView.RotationOf(preset);
            m_ViewPreset = XChartCubeViewPreset.Manual;
        }

        /// <summary>
        /// 把预设当按钮用：非"手动"时把预设四元数写进 <see cref="m_Rotation"/>，随即把选择器复位。
        /// 这样"改一下当前视角"和"从某个预设起步"用的是同一个字段，不存在"预设挡着 Rotation 让它失效"。
        /// </summary>
        private void ConsumeViewPreset()
        {
            if (m_ViewPreset == XChartCubeViewPreset.Manual) return;
            m_Rotation = XChartCubeView.RotationOf(m_ViewPreset);
            m_ViewPreset = XChartCubeViewPreset.Manual;
        }

        public override XChartToolBase CreateTool() => new ChartToolSpace();

        public override void ApplyTo(XChartToolBase tool)
        {
            ConsumeViewPreset();
            if (!(tool is ChartToolSpace space)) return;

            space.Rotation = m_Rotation;
            space.HalfEdge = m_HalfEdge;
            space.SizeMode = m_SizeMode;
            space.CenterOffset = m_CenterOffset;

            // 基变换变了要立刻让版本号走，下游元素下次渲染就会重投影
            space.MarkProjectionDirty();
        }

        public override void CaptureFrom(XChartToolBase tool)
        {
            if (!(tool is ChartToolSpace space)) return;

            m_HalfEdge = space.HalfEdge;
            m_SizeMode = space.SizeMode;
            m_CenterOffset = space.CenterOffset;
            m_Rotation = space.Rotation;
        }
    }
}
