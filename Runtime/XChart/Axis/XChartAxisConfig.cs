using System;
using UnityEngine;
using XericUI.XChart.Mesh;
using XericUI.XChart.Space;

namespace XericUI.XChart.Axis
{
    /// <summary>
    /// 轴线元素的配置。一条轴 = <b>宿主面 + 面内方向 + 子区间 + 空间类型 + 刻度 + 标签来源</b>，
    /// 全部走同一套面帧协议，所以它和背景/散点必然共形变（口径见 <see cref="XChartFaceFrame"/>）。
    ///
    /// <para>要三条轴就在面板里加三个这个元素，各自选自己的面和维度——
    /// 图表不必为"X 轴 / Y 轴 / Z 轴"写任何专用字段。</para>
    /// </summary>
    [Serializable]
    [XChartElement("axis", "轴线", 20)]
    public sealed class XChartAxisConfig : XChartElementConfig
    {
        [Header("宿主")]
        [SerializeField, Tooltip("轴画在哪个面上：填插槽 id（默认 mesh.face.<面号>，由定义网格的元素发布；面号 = 0前 1后 2右 3左 4上 5下，语义同 XChartCubeFaceMask）")]
        private string m_SlotId = "mesh.face.5";

        [SerializeField, Tooltip("对齐格线：开启后每条主刻度会吸附到网格发布的那组线上（网格是定义方，轴只跟随）；偏移超过相邻两线间距的一半就不吸")]
        private bool m_AlignToGrid;

        [SerializeField, Tooltip("沿面参数 s 还是 t 走")]
        private XChartAxisDirection m_Direction = XChartAxisDirection.AlongS;

        [SerializeField, Range(0f, 1f), Tooltip("轴在该方向上的起点比例。默认 0＝从面的边开始；要和网格格线对齐就得铺满整面")]
        private float m_StartRatio;

        [SerializeField, Range(0f, 1f), Tooltip("轴在该方向上的终点比例。默认 1＝到对面边；内缩成 0.95 这种值，两头的刻度就再也对不上格线")]
        private float m_EndRatio = 1f;

        [SerializeField, Range(0f, 1f), Tooltip("轴在垂直方向上的位置（0=贴这条边的起点侧）")]
        private float m_CrossRatio;

        [SerializeField, Tooltip("轴自己的变换（叠在图的空间之上）")]
        private XChartToolTransform m_Transform = XChartToolTransform.Identity;

        [Header("值域与刻度")]
        [SerializeField, Tooltip("轴读哪一维（绑定）。显示范围在 chart 的数据板块逐维给，这里不再存范围")]
        private int m_Dimension;

        [SerializeField, Tooltip("空间类型：线性 / 对数 / 指数")]
        private XChartAxisScale m_Scale = XChartAxisScale.Linear;

        [SerializeField, Tooltip("指数空间的强度（值按 exp(k·参数) 增长）")]
        [Min(0.001f)] private float m_ExponentialStrength = 2f;

        [SerializeField, Tooltip("真=按目标条数自动取好看步长（1/2/5×10^n）；假=用下面的步长")]
        private bool m_AutoStep = true;

        [SerializeField, Tooltip("手动主刻度步长")]
        private double m_ManualStep = 1.0;

        [SerializeField, Range(1, 30), Tooltip("自动步长想要的目标主刻度条数")]
        private int m_TargetMajorCount = 5;

        [SerializeField, Range(0, 20), Tooltip("两个主刻度之间的次刻度份数（0=不画）")]
        private int m_MinorTickCount = 4;

        [Header("外观")]
        [SerializeField] private Color m_LineColor = new Color(0.85f, 0.88f, 0.95f, 0.9f);
        [SerializeField, Min(0.1f)] private float m_LineThickness = 2f;
        [SerializeField, Min(0f)] private float m_MajorTickLength = 8f;
        [SerializeField, Min(0f)] private float m_MinorTickLength = 4f;
        [SerializeField, Min(0.1f)] private float m_TickThickness = 1.5f;
        [SerializeField, Tooltip("刻度朝哪边：-1 朝面参数减小方向（朝外），+1 朝增大方向")]
        private float m_TickSide = -1f;

        [Header("标签")]
        [SerializeField, Tooltip("内容来源：刻度数值 / 维度显示名 / 自定义字符串")]
        private XChartAxisLabelSource m_LabelSource = XChartAxisLabelSource.TickValue;

        [SerializeField, Tooltip("数值格式化串（如 0.00 或 0.#）；留空＝按量级自适应")]
        private string m_LabelFormat;

        [SerializeField, Tooltip("标签来源选\"自定义\"时，按主刻度顺序填这些文本")]
        private string[] m_CustomLabels = new string[0];

        [SerializeField, Min(0f), Tooltip("标签离刻度线的间距（像素）")]
        private float m_LabelGap = 6f;

        [SerializeField, Min(1f), Tooltip("字号")]
        private float m_LabelFontSize = 14f;

        [SerializeField] private Color m_LabelColor = new Color(0.85f, 0.88f, 0.95f, 0.95f);
        [SerializeField] private Vector2 m_LabelSize = new Vector2(90f, 24f);

        [SerializeField, Tooltip("标签跟着轴的方向转（斜面上读起来才不拧着）")]
        private bool m_RotateLabelsWithAxis = true;

        public override string ElementName => "Axis";

        public override XChartToolBase CreateTool() => new ChartToolAxis();

        public override void ApplyTo(XChartToolBase tool)
        {
            if (!(tool is ChartToolAxis axis)) return;

            axis.SlotId = m_SlotId;
            axis.AlignToGrid = m_AlignToGrid;
            axis.Direction = m_Direction;
            axis.StartRatio = m_StartRatio;
            axis.EndRatio = m_EndRatio;
            axis.CrossRatio = m_CrossRatio;
            axis.Transform = m_Transform;

            axis.Dimension = m_Dimension;
            axis.Scale = m_Scale;
            axis.ExponentialStrength = m_ExponentialStrength;
            axis.AutoStep = m_AutoStep;
            axis.ManualStep = m_ManualStep;
            axis.TargetMajorCount = m_TargetMajorCount;
            axis.MinorTickCount = m_MinorTickCount;

            axis.LineColor = m_LineColor;
            axis.LineThickness = m_LineThickness;
            axis.MajorTickLength = m_MajorTickLength;
            axis.MinorTickLength = m_MinorTickLength;
            axis.TickThickness = m_TickThickness;
            axis.TickSide = m_TickSide;

            axis.LabelSource = m_LabelSource;
            axis.LabelFormat = m_LabelFormat;
            axis.CustomLabels = m_CustomLabels;
            axis.LabelGap = m_LabelGap;
            axis.LabelFontSize = m_LabelFontSize;
            axis.LabelColor = m_LabelColor;
            axis.LabelSize = m_LabelSize;
            axis.RotateLabelsWithAxis = m_RotateLabelsWithAxis;

            axis.MarkDirty();
        }
    }
}
