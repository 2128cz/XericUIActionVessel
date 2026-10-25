using System;
using UnityEngine;
using XericUI.XChart.Space;

namespace XericUI.XChart.Mesh
{
    /// <summary>
    /// 一条记录里的两个点怎么变成一支箭头。
    ///
    /// <para><b>绝对</b>：后三点是方向矢量本身（起点 + 方向×倍率＝末端）。螺旋点这样画出来是"刺猬"。
    /// <b>相对</b>：后三点是第二个点，箭头＝第一点指向第二点（差向量×倍率）。
    /// 连续绘制模式下两者都不适用（连起来的是一条空间路径）。</para>
    /// </summary>
    public enum XChartArrowVectorMode
    {
        /// <summary>后三维＝方向矢量</summary>
        Absolute = 0,
        /// <summary>后三维＝第二个点（箭头由第一点指向它）</summary>
        Relative = 1,
    }

    /// <summary>圆柱元素的配置（几何单位都是立方体局部：±1 就是盒子边界）。</summary>
    [Serializable]
    [XChartElement("cylinder", "圆柱", 40)]
    public sealed class XChartCylinderConfig : XChartElementConfig
    {
        [Header("几何")]
        [SerializeField, Tooltip("起点圆心（立方体单位）")]
        private Vector3 m_Start = new Vector3(-0.6f, -0.6f, -0.6f);
        [SerializeField, Tooltip("终点圆心（立方体单位）；两端不重合就是斜圆柱")]
        private Vector3 m_End = new Vector3(0.6f, 0.6f, 0.6f);

        [SerializeField, Tooltip("几何来源：关＝用上面的常量坐标；开＝从数据取（每组的首尾两条记录＝这条圆柱的两端）")]
        private bool m_DriveByData;
        [SerializeField, Tooltip("数据模式下 X 取哪一维")]
        private int m_DimensionX;
        [SerializeField, Tooltip("数据模式下 Y 取哪一维")]
        private int m_DimensionY = 1;
        [SerializeField, Tooltip("数据模式下 Z 取哪一维")]
        private int m_DimensionZ = 2;
        [SerializeField, Min(2), Tooltip("几条记录凑一根圆柱（2＝两两一组：记录 0-1、2-3…，不重叠也不并入上一组）")]
        private int m_GroupSize = 2;
        [SerializeField, Min(1), Tooltip("最多画几根（记录多时按组数上限截断，报表会点名截了多少）")]
        private int m_MaxCylinders = 64;

        [SerializeField, Min(0.001f), Tooltip("半径（立方体单位）")]
        private float m_Radius = 0.22f;
        [SerializeField, Range(8, 256), Tooltip("一圈分多少段（轮廓与十字的采样精度）")]
        private int m_SegmentCount = 48;

        [SerializeField, Tooltip("起点端面的法线。零＝沿两端连线（与旧行为一致）")]
        private Vector3 m_StartNormal = Vector3.zero;
        [SerializeField, Tooltip("终点端面的法线。零＝沿两端连线。两端各给各的，就能做出端面不平行的那种")]
        private Vector3 m_EndNormal = Vector3.zero;

        [SerializeField, Tooltip("圆柱自己的变换（叠在图的空间之上）")]
        private XChartToolTransform m_Transform = XChartToolTransform.Identity;

        [Header("呈现（各有开关）")]
        [SerializeField, Tooltip("外轮廓包边（视角下的轮廓，不是把所有母线都连起来）")]
        private bool m_ShowOutline = true;
        [SerializeField] private Color m_OutlineColor = new Color(0.75f, 0.85f, 1f, 0.9f);
        [SerializeField, Min(0.1f)] private float m_OutlineThickness = 2f;

        [SerializeField, Tooltip("轮廓填充（与包边同一个凸包多边形）")]
        private bool m_ShowFill = true;
        [SerializeField] private Color m_FillColor = new Color(0.34f, 0.55f, 0.85f, 0.35f);

        [SerializeField, Tooltip("端面十字：用端面自己的采样顶点连成，所以永远与实际端面对得上，能看出滚转")]
        private bool m_ShowCapCross;
        [SerializeField] private Color m_CrossColor = new Color(0.9f, 0.95f, 1f, 0.85f);
        [SerializeField, Min(0.1f)] private float m_CrossThickness = 1.5f;

        [SerializeField, Tooltip("起点端面的闭合圆环（端面自己的边界，整圈闭合）")]
        private bool m_ShowStartRing;
        [SerializeField, Tooltip("终点端面的闭合圆环")]
        private bool m_ShowEndRing;

        [SerializeField, Range(0, 64), Tooltip("内部母线数量（两端对应点相连；0＝不画）")]
        private int m_GeneratrixCount;
        [SerializeField] private Color m_GeneratrixColor = new Color(0.75f, 0.85f, 1f, 0.35f);

        public override string ElementName => "Cylinder";

        public override XChartToolBase CreateTool() => new ChartToolCylinder();

        public override void ApplyTo(XChartToolBase tool)
        {
            if (!(tool is ChartToolCylinder cylinder)) return;

            cylinder.Start = m_Start;
            cylinder.End = m_End;
            cylinder.DriveByData = m_DriveByData;
            cylinder.DimensionX = m_DimensionX;
            cylinder.DimensionY = m_DimensionY;
            cylinder.DimensionZ = m_DimensionZ;
            cylinder.GroupSize = m_GroupSize;
            cylinder.MaxCylinders = m_MaxCylinders;
            cylinder.Radius = m_Radius;
            cylinder.SegmentCount = m_SegmentCount;
            cylinder.StartNormal = m_StartNormal;
            cylinder.EndNormal = m_EndNormal;
            cylinder.Transform = m_Transform;

            cylinder.ShowOutline = m_ShowOutline;
            cylinder.OutlineColor = m_OutlineColor;
            cylinder.OutlineThickness = m_OutlineThickness;

            cylinder.ShowFill = m_ShowFill;
            cylinder.FillColor = m_FillColor;

            cylinder.ShowCapCross = m_ShowCapCross;
            cylinder.CrossColor = m_CrossColor;
            cylinder.CrossThickness = m_CrossThickness;

            cylinder.ShowStartRing = m_ShowStartRing;
            cylinder.ShowEndRing = m_ShowEndRing;

            cylinder.GeneratrixCount = m_GeneratrixCount;
            cylinder.GeneratrixColor = m_GeneratrixColor;

            cylinder.MarkDirty();
        }
    }

    /// <summary>
    /// 向量箭头（矢量场）元素的配置。
    ///
    /// <para><b>数据口径（2026-10-25 他澄清后定稿，规范 §25.2）</b>：</para>
    /// <list type="bullet">
    /// <item><b>前三维＝坐标 x/y/z</b>（立方体单位，与圆柱、散点同一套归一化）——不是"两组三维"。</item>
    /// <item><b>一支箭头＝相邻两条记录</b>：第 i 条是起点，第 i+1 条按 <see cref="VectorMode"/> 解释成
    /// "第二个点"或"方向矢量"（他原话："我们是 0 和 1，一个代表坐标，一个代表它的矢量的方向"）。</item>
    /// <item><b>第 4~7 维是颜色</b>：色相（0..256 色环）/ 饱和度 / 明度 / 不透明度，全部可选。
    /// 颜色随记录自带 ⇒ 一支箭头的起点色与终点色可以不同（七维信息的那个用法）。</item>
    /// </list>
    ///
    /// <para><b>连续绘制</b>（<see cref="Continuous"/>）：把每条记录的坐标按顺序连成折线——
    /// 这时"相对/绝对"没有意义（连起来的本来就是空间里的一条路径）⇒ 面板上那两项**隐藏**
    /// （互斥显示，见 <see cref="IsFieldHidden"/>）。</para>
    /// </summary>
    [Serializable]
    [XChartElement("arrows", "向量箭头", 50)]
    public sealed class XChartArrowConfig : XChartElementConfig
    {
        /// <summary>坐标要占几个数值维（x/y/z）</summary>
        public const int CoordinateDimensionCount = 3;
        /// <summary>颜色要几个数值维（色相/饱和度/明度 [+不透明度]）</summary>
        public const int ColorDimensionCount = 3;

        [Header("数据")]
        [SerializeField, Tooltip("坐标从第几个数值维开始（一条记录 3 维：x/y/z）；一支箭头由**相邻两条记录**构成")]
        private int m_CoordinateDimension;

        [SerializeField, Tooltip("绝对：第二条记录的坐标当作方向矢量（起点→末端）。" +
                                 "相对：它是第二个点，箭头＝第一点指向第二点。**连续绘制打开时这一项不适用**")]
        private XChartArrowVectorMode m_VectorMode = XChartArrowVectorMode.Absolute;

        [SerializeField, Tooltip("把每条记录的坐标按顺序连成一条路径（贝塞尔细分，接缝不断口）。" +
                                 "打开之后「相对/绝对」被隐藏——连起来的是一条空间路径，谈不到这两者")]
        private bool m_Continuous;

        [SerializeField, Range(0f, 1f), Tooltip("连续绘制时箭头放在路径的哪个位置（0＝起点、1＝末端）：" +
                                                "路径在这一处剪断，箭头顺那一点的切线。只有连续绘制用得上")]
        private float m_HeadPosition = 1f;

        [SerializeField, Range(2, 64), Tooltip("连续绘制时每段路径细分成几节（曲线渲染器的 tessellation）。" +
                                               "越大越圆也越贵。只有连续绘制用得上")]
        private int m_PathSegments = 12;

        [SerializeField, Tooltip("方向向量长度倍率（立方体单位）；相对模式下作用在差向量上")]
        private float m_DirectionScale = 0.25f;

        [SerializeField, Range(0, 100000), Tooltip("最多画多少支（0=不限）；防止十万条记录一口气铺满")]
        private int m_MaxArrowCount = 512;

        [SerializeField, Tooltip("箭头自己的变换（叠在图的空间之上）")]
        private XChartToolTransform m_Transform = XChartToolTransform.Identity;

        [Header("颜色（可选维度）")]
        [SerializeField, Tooltip("用额外的数值维给每支箭头上色。三个维依次是 色相(0..256 色环) / 饱和度(0..1) / 明度(0..1)")]
        private bool m_UseColorDimensions;

        [SerializeField, Tooltip("颜色三维从第几维开始（默认 3＝紧跟在坐标后面）。" +
                                 "这些维**不进**坐标语义，只是被读走做颜色；起止两端各自读自己那条记录的颜色")]
        private int m_ColorDimension = 3;

        [SerializeField, Tooltip("再加一维不透明度（0..1），读颜色起点维 +3")]
        private bool m_HasAlphaDimension;

        [Header("外观")]
        [SerializeField, Tooltip("没有给颜色维度时用的单色")]
        private Color m_ArrowColor = new Color(0.95f, 0.6f, 0.25f, 0.9f);
        [SerializeField, Min(0.1f), Tooltip("杆宽（像素）")]
        private float m_ShaftWidth = 2f;
        [SerializeField, Min(0.1f), Tooltip("箭头长（像素）")]
        private float m_HeadLength = 10f;
        [SerializeField, Min(0.1f), Tooltip("箭头宽（像素）")]
        private float m_HeadWidth = 7f;
        [SerializeField, Tooltip("零向量也画成一个极小四边形（不画的话它会整条消失）")]
        private bool m_ShowZeroVectorsAsDots;

        public override string ElementName => "Arrows";

        /// <summary>
        /// 这个元素要几个数值维才画得出来（面板据此报"表里只有几个"，不再"什么都不显示"）。
        /// 坐标 3 ＋（可选）颜色 3；**不透明度那一维不算硬需求**（多给就多读、没给退回单色 alpha）——
        /// 算进门槛的话，勾一下"有不透明度"会让整个元素因维数不够突然消失。
        /// </summary>
        public override int RequiredNumericDimensions
            => CoordinateDimensionCount + (m_UseColorDimensions ? ColorDimensionCount : 0);

        /// <summary>一支箭头要两条记录 ⇒ 记录少于 2 条时一支都画不出来（面板/报表同样要印出来）</summary>
        public override int RequiredRecordCount => 2;

        /// <summary>
        /// 互斥显示：连续绘制打开时「相对/绝对」没有意义（他 2026-10-25：勾一个就把排斥的那几个藏起来）。
        /// 判据只有一处，面板照着问，不在面板里写特例。
        /// </summary>
        public override bool IsFieldHidden(string fieldName)
        {
            if (fieldName == nameof(m_VectorMode)) return m_Continuous;
            // 这两项是"路径上的箭头"才有的概念：一支一支画的时候头永远在末端
            if (fieldName == nameof(m_HeadPosition) || fieldName == nameof(m_PathSegments)) return !m_Continuous;
            if (fieldName == nameof(m_ColorDimension) || fieldName == nameof(m_HasAlphaDimension))
                return !m_UseColorDimensions;
            return false;
        }

        public override XChartToolBase CreateTool() => new ChartToolVectorArrows();

        public override void ApplyTo(XChartToolBase tool)
        {
            if (!(tool is ChartToolVectorArrows arrows)) return;

            arrows.CoordinateDimension = m_CoordinateDimension;
            arrows.VectorMode = m_VectorMode;
            arrows.Continuous = m_Continuous;
            arrows.HeadPosition = m_HeadPosition;
            arrows.PathSegments = m_PathSegments;
            arrows.DirectionScale = m_DirectionScale;
            arrows.MaxArrowCount = m_MaxArrowCount;
            arrows.Transform = m_Transform;

            arrows.UseColorDimensions = m_UseColorDimensions;
            arrows.ColorDimension = m_ColorDimension;
            arrows.HasAlphaDimension = m_HasAlphaDimension;

            arrows.ArrowColor = m_ArrowColor;
            arrows.ShaftWidth = m_ShaftWidth;
            arrows.HeadLength = m_HeadLength;
            arrows.HeadWidth = m_HeadWidth;
            arrows.ShowZeroVectorsAsDots = m_ShowZeroVectorsAsDots;

            arrows.MarkDirty();
        }
    }
}
