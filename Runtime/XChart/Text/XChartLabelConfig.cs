using System;
using UnityEngine;
using XericUI.XChart.Space;

namespace XericUI.XChart.Text
{
    /// <summary>
    /// 数据标签元素的配置。<b>面板这一侧只存"选哪一档/哪一列"</b>：
    /// 锚点与文字对齐的对应关系住在 <see cref="XChartLabelAnchorMath"/>，
    /// 列号到标签槽的换算住在 <see cref="XChartColumnMath"/>——配置里再写一遍就会各说一套。
    ///
    /// <para><c>LabelSlot</c> 是<b>标签列的序号</b>（不是数据表里的行号）：窗口里排在最前面那些列就是标签列，
    /// 面板上看到的列号要换算成槽号才读得对，所以这一格存的是槽号而由同一处算出来。</para>
    /// </summary>
    [Serializable]
    [XChartElement("label", "标签", 60)]
    public sealed class XChartLabelConfig : XChartElementConfig
    {
        [Header("数据来源")]
        [SerializeField, Min(0), Tooltip("读第几个标签列（标签列在数据表里排在最前面；这一格是槽号，不是表里的行号）")]
        private int m_LabelSlot;

        [SerializeField, Tooltip("标签挂在哪儿：三个数值维下标（显示范围在 chart 的数据板块逐维给）")]
        private int m_DimensionX;
        [SerializeField] private int m_DimensionY = 1;
        [SerializeField] private int m_DimensionZ = 2;

        [SerializeField, Tooltip("标签自己的变换（叠在图的空间之上）")]
        private XChartToolTransform m_Transform = XChartToolTransform.Identity;

        [SerializeField, Tooltip("数据落在显示范围之外时怎么办：原样画（默认）/ 夹到范围端点 / 整条隐藏")]
        private XChartValueOutOfRange m_OutOfRange = XChartValueOutOfRange.RenderAsIs;

        [Header("呈现")]
        [SerializeField, Tooltip("标签矩形相对它那一点的位置（9 档）。文字对齐由同一档算出来，不另给一个字段")]
        private XChartLabelAnchor m_Anchor = XChartLabelAnchor.UpperCenter;

        [SerializeField, Tooltip("从那个点再挪多少像素（非有限数按零算）")]
        private Vector2 m_Offset = new Vector2(0f, 6f);

        [SerializeField, Min(1), Tooltip("字号")]
        private int m_FontSize = 14;

        [SerializeField, Tooltip("字色")]
        private Color m_LabelColor = Color.white;

        [SerializeField, Min(0f), Tooltip("描边宽度")]
        private float m_OutlineWidth;

        [SerializeField, Tooltip("标签矩形大小（像素）。字超出的部分按下面的换行档处理")]
        private Vector2 m_RectSize = new Vector2(160f, 30f);

        [SerializeField, Tooltip("字是否在这个矩形内换行")]
        private bool m_Wrap;

        public override string ElementName => "Label";

        public override XChartToolBase CreateTool() => new ChartToolLabels();

        public override void ApplyTo(XChartToolBase tool)
        {
            if (!(tool is ChartToolLabels labels)) return;

            labels.LabelSlot = m_LabelSlot;
            labels.DimensionX = m_DimensionX;
            labels.DimensionY = m_DimensionY;
            labels.DimensionZ = m_DimensionZ;
            labels.Transform = m_Transform;
            labels.OutOfRange = m_OutOfRange;

            labels.Anchor = m_Anchor;
            labels.Offset = m_Offset;
            labels.FontSize = m_FontSize;
            labels.LabelColor = m_LabelColor;
            labels.OutlineWidth = m_OutlineWidth;
            labels.RectSize = m_RectSize;
            labels.Wrap = m_Wrap;

            labels.MarkDirty();
        }

        public override void CaptureFrom(XChartToolBase tool)
        {
            if (!(tool is ChartToolLabels labels)) return;

            m_LabelSlot = labels.LabelSlot;
            m_Anchor = labels.Anchor;
            m_Offset = labels.Offset;
            m_FontSize = labels.FontSize;
            m_LabelColor = labels.LabelColor;
            m_RectSize = labels.RectSize;
            m_Wrap = labels.Wrap;
        }

        public override int RequiredNumericDimensions => 3;

        public override int RequiredRecordCount => 1;

        public override int RequiredLabelColumns => 1;
    }
}
