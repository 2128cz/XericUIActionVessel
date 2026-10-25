using UnityEngine;

namespace XericUI.XChart
{
    /// <summary>
    /// 一维的<b>覆盖请求</b>：哪几端被钉住、钉成多少。
    ///
    /// <para>为什么要分侧（<see cref="HasMin"/> / <see cref="HasMax"/> 各一个）：验收时他要的是
    /// "最小值必须得是 0，最大值让它自由发挥"——一个总的开关只能表达"两端都钉住"或"两端都自由"，
    /// 而"钉一端"是常见形态（把轴的原点按住，让另一端跟数据走）。</para>
    /// </summary>
    public readonly struct XChartDomainOverride
    {
        public readonly bool HasMin;
        public readonly double Min;
        public readonly bool HasMax;
        public readonly double Max;

        public XChartDomainOverride(bool hasMin, double min, bool hasMax, double max)
        {
            HasMin = hasMin;
            Min = min;
            HasMax = hasMax;
            Max = max;
        }

        /// <summary>两端都没钉（＝完全跟着该维聚合走）</summary>
        public static XChartDomainOverride None => new XChartDomainOverride(false, 0.0, false, 0.0);
    }

    /// <summary>
    /// 显示范围（domain）解析的<b>纯函数</b>：只吃标量，不碰 Unity 对象，
    /// 所以编译门里的 mono 探针能把这几条规则逐条当断言跑（含反向变异对照）。
    ///
    /// <para>规则（顺序就是优先级）：</para>
    /// <list type="number">
    /// <item><b>钉住的端优先</b>：每一端各问各的——钉了就用填的值，没钉就用该维聚合。</item>
    /// <item>只钉了一端、而另一端<b>连聚合都没有</b>时，以钉住的那端为中心展开 ±<see cref="DegeneratePad"/>：
    /// 这跟"两端都钉住但填成同一个值"同一处理，都是为了避开零宽域上的除零。</item>
    /// <item>钉住的一端比自由端的聚合还靠外（或写反了）⇒ <b>自动排序</b>。手滑填反不该让整条轴什么都不画，
    /// 也不该画出一段反向的轴。</item>
    /// <item>什么都没钉、聚合也没有 → false，调用者这帧什么都不画（不是画在 0 上）。</item>
    /// </list>
    ///
    /// <para><b>入口只有一个</b>：拿数据源问范围请走 <see cref="TryResolve(IXChartDataSource,int,out double,out double)"/>
    /// （它读维度行的覆盖 + 该维聚合，再调下面这条纯规则）。渲染上下文、面板、数据窗口都从这里问，
    /// 于是"这条轴现在画哪一段"这件事全图只有一份算法。</para>
    /// </summary>
    public static class XChartDomainMath
    {
        /// <summary>退化范围的中心展开半径（数据全是同一个值、或只钉了一端又没有聚合时用）</summary>
        public const double DegeneratePad = 0.5;

        /// <summary>这一维现在要钉住什么：覆盖请求就存在 chart 的维度行里（"范围归数据、工具只呈现"）</summary>
        public static XChartDomainOverride OverrideOf(IXChartDataSource data, int dimension)
        {
            XChartDimensionDefinition definition;
            if (data == null || !data.TryGetDimension(dimension, out definition) || definition == null)
                return XChartDomainOverride.None;
            return definition.DomainOverride;
        }

        /// <summary>
        /// 解析某维的显示范围（<b>所有人问范围的同一扇门</b>）：覆盖来自维度行，
        /// 自由端来自该维的聚合。规则明细见类型注释。
        /// </summary>
        public static bool TryResolve(IXChartDataSource data, int dimension, out double min, out double max)
        {
            min = 0.0;
            max = 1.0;
            if (data == null) return false;

            XChartAggregates aggregates = default;
            bool has = data.TryGetAggregates(dimension, XChartAggregateMask.Range, out aggregates)
                && aggregates.Has(XChartAggregateKind.Minimum)
                && aggregates.Has(XChartAggregateKind.Maximum);

            return TryResolve(OverrideOf(data, dimension), has,
                has ? aggregates.Minimum : 0.0, has ? aggregates.Maximum : 0.0, out min, out max);
        }

        public static bool TryResolve(XChartDomainOverride over, bool hasAggregates, double aggregateMin,
            double aggregateMax, out double min, out double max)
        {
            min = 0.0;
            max = 0.0;

            bool aggregatesUsable = hasAggregates
                && !double.IsNaN(aggregateMin) && !double.IsNaN(aggregateMax)
                && !double.IsInfinity(aggregateMin) && !double.IsInfinity(aggregateMax);

            // 两端都没钉：纯聚合口径
            if (!over.HasMin && !over.HasMax)
            {
                if (!aggregatesUsable) return false;
                if (aggregateMax > aggregateMin)
                {
                    min = aggregateMin;
                    max = aggregateMax;
                    return true;
                }
                min = aggregateMin - DegeneratePad;
                max = aggregateMin + DegeneratePad;
                return true;
            }

            // 钉住的一端用填的值，自由的一端用聚合（聚合没有就是 NaN，下面按"没有依据"处理）
            double lower = over.HasMin ? over.Min : (aggregatesUsable ? aggregateMin : double.NaN);
            double upper = over.HasMax ? over.Max : (aggregatesUsable ? aggregateMax : double.NaN);

            if (double.IsNaN(lower) || double.IsNaN(upper))
            {
                // 只钉了一端，另一端没有任何依据 ⇒ 以钉住的这端为中心展开（零宽的域画不出东西）
                double anchor = over.HasMin ? over.Min : over.Max;
                min = anchor - DegeneratePad;
                max = anchor + DegeneratePad;
                return true;
            }

            if (upper < lower)
            {
                double swap = lower;
                lower = upper;
                upper = swap;
            }

            if (upper == lower)
            {
                min = lower - DegeneratePad;
                max = lower + DegeneratePad;
                return true;
            }

            min = lower;
            max = upper;
            return true;
        }
    }
}
