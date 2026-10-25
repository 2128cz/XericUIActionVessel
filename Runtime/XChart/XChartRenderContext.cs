using System;
using UnityEngine;
using XericUI.XChart.Mesh;
using XericUI.XChart.Space;

namespace XericUI.XChart
{
    /// <summary>
    /// 渲染上下文：<b>由 chart 建立并持有，工具只读</b>（规范 §2）。
    ///
    /// <para>它替代"工具自己去 FindTool 找邻居"的写法：空间、数据、显示范围都从这里拿。
    /// 于是"谁给谁投影"这件事在图这一层就定了，工具里没有第二条查找路径，
    /// 也不会出现两个工具各自算一遍投影、点落不进盒子那种事。</para>
    ///
    /// <para><b>可以返回 null</b>：<see cref="Space"/> 在初始化早期或重建过程中可能还没有，
    /// 工具必须判空并跳过这一帧（不抛、也不拿旧的空间凑数）。</para>
    /// </summary>
    public interface IXChartRenderContext
    {
        /// <summary>投影权威（图的空间工具）。没有就返回 null，调用方跳过本帧。</summary>
        ChartToolSpace Space { get; }

        /// <summary>
        /// 插槽板（规范 §11.2）：<b>定义网格的工具**发布**标定顶点，其余工具按 id **读取**</b>——
        /// 消费者认的是点，不是"第几个面"，所以换成异形网格也不用改消费者。
        /// 空板也一定非 null（没有网格时读到 false），调用方不必判 null。
        /// </summary>
        IXChartSlotBoard Slots { get; }

        /// <summary>数据中转口。默认实现是图表自己那份直通实现。</summary>
        IXChartDataSource Data { get; }

        /// <summary>
        /// 解析某维的显示范围。<b>手动范围不从这里传</b>——它归 chart 的维度行（数据板块，
        /// 见 <see cref="XChartDimensionDefinition.DomainOverride"/>），工具只说要哪一维。
        /// 规则明细见 <see cref="XChartDomainMath"/>：覆盖优先、没覆盖回落该维聚合、
        /// 聚合也没有就返回 false（调用者应清空自己的绘制，而不是画在 0 上）。
        /// </summary>
        bool TryResolveDomain(int dimension, out double min, out double max);
    }

    /// <summary>
    /// <see cref="IXChartRenderContext"/> 的默认实现：空间靠一个由 chart 提供的解析委托现找，
    /// 数据直通图表的 <c>XChartDataModel</c>。
    ///
    /// <para><b>为什么每次现找而不缓存空间实例</b>：重建（<c>RebuildAll</c>、增删元素）会换掉空间工具，
    /// 缓存下来的引用不会报错、只会安静地画到旧空间上——那比 null 难查得多。每帧一次线性查找
    /// 只在渲染前做一次，代价远小于查错对象。</para>
    /// </summary>
    public sealed class XChartRenderContext : IXChartRenderContext
    {
        private readonly IXChartDataSource m_Data;
        private readonly Func<ChartToolSpace> m_SpaceResolver;

        public XChartRenderContext(IXChartDataSource data, Func<ChartToolSpace> spaceResolver)
        {
            m_Data = data;
            m_SpaceResolver = spaceResolver;
        }

        public ChartToolSpace Space => m_SpaceResolver != null ? m_SpaceResolver() : null;

        public IXChartSlotBoard Slots { get; } = new XChartSlotBoard();

        public IXChartDataSource Data => m_Data;

        public bool TryResolveDomain(int dimension, out double min, out double max)
        {
            // 范围的算法只有一份（XChartDomainMath 的那扇门）：这里就是转发，
            // 覆盖来自 chart 的维度行、自由端来自该维聚合——工具不再各存一份范围
            return XChartDomainMath.TryResolve(m_Data, dimension, out min, out max);
        }
    }
}
