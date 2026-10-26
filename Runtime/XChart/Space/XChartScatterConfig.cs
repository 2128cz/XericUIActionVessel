using System;
using UnityEngine;
using XericLibrary.Runtime.UIGraph;
using PrimitiveType = XericLibrary.Runtime.UIGraph.PrimitiveType;
using XericUI.XChart.Pseudo3D;
using XericUI.XChart.Space;

namespace XericUI.XChart.Space
{
    /// <summary>
    /// 散点元素的配置。数据默认<b>直接读图的数据源</b>（按维度下标 + 聚合范围归一化），
    /// 所以配好三个维度下标就能出图，不需要在图表上为散点再写一遍数据。
    ///
    /// <para>关掉 <see cref="m_AutoFromData"/> 就变成"脚本喂点"模式：由调用方
    /// <c>SetCubePoints</c> 给立方体单位的点（[-1,1] 就是盒子内部）。</para>
    /// </summary>
    [Serializable]
    [XChartElement("scatter", "散点", 30)]
    public sealed class XChartScatterConfig : XChartElementConfig
    {
        [Header("数据来源")]
        [SerializeField, Tooltip("真=按下面的维度下标读图的数据源；假=脚本用 SetCubePoints 喂")]
        private bool m_AutoFromData = true;

        [SerializeField, Tooltip("X / Y / Z 轴取哪几个维度下标（绑定；显示范围在 chart 的数据板块逐维给）")]
        private int m_DimensionX;
        [SerializeField] private int m_DimensionY = 1;
        [SerializeField] private int m_DimensionZ = 2;

        [SerializeField, Tooltip("点云自己的变换（叠在图的空间之上）")]
        private XChartToolTransform m_Transform = XChartToolTransform.Identity;

        [SerializeField, Min(0), Tooltip("桶采样目标点数：0 或 ≥ 记录数＝不聚合；填了数就把记录按桶归约成这么多个点")]
        private int m_BucketTargetPoints;

        [SerializeField, Tooltip("桶内取哪个统计量当这个点：抽稀用 First/Last（不造新值），看趋势用 Mean/Median，看包络用 Minimum/Maximum")]
        private XChartAggregateKind m_BucketReduce = XChartAggregateKind.Last;

        [SerializeField, Tooltip("点落在显示范围之外时怎么办：原样画（默认，2026-10-26 起；以前散点总是夹住，" +
                                 "与矢量/圆柱不一致）/ 夹到范围端点 / 整条隐藏。颜色映射那一档总是夹（越界取端色）")]
        private XChartValueOutOfRange m_OutOfRange = XChartValueOutOfRange.RenderAsIs;

        [Header("外观")]
        [SerializeField, Min(0.5f), Tooltip("点的大小（像素）")]
        private float m_PointSize = 8f;

        [SerializeField, Tooltip("图元类型")]
        private PrimitiveType m_PointType = PrimitiveType.Polygon;

        [SerializeField, Tooltip("图元边数（多边形时有效）")]
        private int m_SideCount = 8;

        [SerializeField, Tooltip("填充色")]
        private Color m_PointColor = new Color(0.33f, 0.67f, 1f, 1f);

        [SerializeField, Tooltip("中心色；与填充色不同就走径向渐变")]
        private Color m_CenterColor = Color.white;

        [SerializeField, Tooltip("边框色")]
        private Color m_BorderColor = new Color(0.2f, 0.2f, 0.2f, 1f);

        [SerializeField, Min(0f), Tooltip("边框宽度")]
        private float m_BorderThickness = 1f;

        [Header("按数值上色")]
        [SerializeField, Tooltip("用某一维的数值做颜色渐变")]
        private bool m_EnableColorMapping;

        [SerializeField, Tooltip("拿哪一维的数值")]
        private int m_ColorMapDimension = 2;

        [SerializeField, Tooltip("范围下限对应颜色")]
        private Color m_ColorMapMin = Color.blue;

        [SerializeField, Tooltip("范围上限对应颜色")]
        private Color m_ColorMapMax = Color.red;

        public override string ElementName => "Scatter";

        public override XChartToolBase CreateTool() => new ChartToolScatterPoints();

        public override void ApplyTo(XChartToolBase tool)
        {
            if (!(tool is ChartToolScatterPoints scatter)) return;

            scatter.AutoFromData = m_AutoFromData;
            scatter.DimensionX = m_DimensionX;
            scatter.DimensionY = m_DimensionY;
            scatter.DimensionZ = m_DimensionZ;
            scatter.Transform = m_Transform;
            scatter.BucketTargetPoints = m_BucketTargetPoints;
            scatter.BucketReduce = m_BucketReduce;
            scatter.OutOfRange = m_OutOfRange;

            scatter.PointSize = m_PointSize;
            scatter.PointType = m_PointType;
            scatter.SideCount = m_SideCount;
            scatter.PointColor = m_PointColor;
            scatter.CenterColor = m_CenterColor;
            scatter.BorderColor = m_BorderColor;
            scatter.BorderThickness = m_BorderThickness;

            scatter.EnableColorMapping = m_EnableColorMapping;
            scatter.ColorMapDimension = m_ColorMapDimension;
            scatter.ColorMapMin = m_ColorMapMin;
            scatter.ColorMapMax = m_ColorMapMax;

            scatter.MarkDataDirty();
        }

        public override void CaptureFrom(XChartToolBase tool)
        {
            if (!(tool is ChartToolScatterPoints scatter)) return;

            m_AutoFromData = scatter.AutoFromData;
            m_PointSize = scatter.PointSize;
            m_PointColor = scatter.PointColor;
            m_Transform = scatter.Transform;
        }
    }
}
