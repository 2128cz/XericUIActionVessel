using System;

namespace XericUI.XChart
{
    /// <summary>可向数据源索取的聚合量</summary>
    public enum XChartAggregateKind
    {
        Count,
        First,
        Last,
        Minimum,
        Maximum,
        Sum,
        Mean,
        Rms,
        Median,
    }

    /// <summary>聚合量集合（可按位组合，一次扫描一并算出）</summary>
    [Flags]
    public enum XChartAggregateMask
    {
        None = 0,
        Count = 1 << 0,
        First = 1 << 1,
        Last = 1 << 2,
        Minimum = 1 << 3,
        Maximum = 1 << 4,
        Sum = 1 << 5,
        Mean = 1 << 6,
        Rms = 1 << 7,
        Median = 1 << 8,

        /// <summary>数据范围（轴线自动缩放最常用的两个）</summary>
        Range = Minimum | Maximum,
        /// <summary>默认集合</summary>
        Default = Count | Minimum | Maximum,
        All = Count | First | Last | Minimum | Maximum | Sum | Mean | Rms | Median,
    }

    /// <summary>
    /// 聚合结果。<see cref="Computed"/> 标明哪些字段真的被算过（其余字段无意义）。
    /// </summary>
    public struct XChartAggregates
    {
        public XChartAggregateMask Computed;

        public int Count;
        public double First;
        public double Last;
        public double Minimum;
        public double Maximum;
        public double Sum;
        public double Mean;
        public double Rms;
        public double Median;

        /// <summary>是否算过某项</summary>
        public bool Has(XChartAggregateKind kind) => (Computed & ToMask(kind)) != 0;

        /// <summary>数据范围（需算过 Minimum/Maximum）</summary>
        public double Range => Maximum - Minimum;

        /// <summary>取某项，没算过则返回 fallback</summary>
        public double Get(XChartAggregateKind kind, double fallback)
        {
            switch (kind)
            {
                case XChartAggregateKind.Count: return Has(kind) ? Count : fallback;
                case XChartAggregateKind.First: return Has(kind) ? First : fallback;
                case XChartAggregateKind.Last: return Has(kind) ? Last : fallback;
                case XChartAggregateKind.Minimum: return Has(kind) ? Minimum : fallback;
                case XChartAggregateKind.Maximum: return Has(kind) ? Maximum : fallback;
                case XChartAggregateKind.Sum: return Has(kind) ? Sum : fallback;
                case XChartAggregateKind.Mean: return Has(kind) ? Mean : fallback;
                case XChartAggregateKind.Rms: return Has(kind) ? Rms : fallback;
                case XChartAggregateKind.Median: return Has(kind) ? Median : fallback;
                default: return fallback;
            }
        }

        public static XChartAggregateMask ToMask(XChartAggregateKind kind) => (XChartAggregateMask)(1 << (int)kind);

        /// <summary>把单项结果并进结果集</summary>
        public void Set(XChartAggregateKind kind, double value)
        {
            switch (kind)
            {
                case XChartAggregateKind.Count: Count = (int)value; break;
                case XChartAggregateKind.First: First = value; break;
                case XChartAggregateKind.Last: Last = value; break;
                case XChartAggregateKind.Minimum: Minimum = value; break;
                case XChartAggregateKind.Maximum: Maximum = value; break;
                case XChartAggregateKind.Sum: Sum = value; break;
                case XChartAggregateKind.Mean: Mean = value; break;
                case XChartAggregateKind.Rms: Rms = value; break;
                case XChartAggregateKind.Median: Median = value; break;
            }
            Computed |= ToMask(kind);
        }
    }

    /// <summary>
    /// 范围游标（虚拟指针）：按时间窗在某一维上顺序读取，<b>不暴露底层存储</b>。
    ///
    /// <para>它是为"以后换成桶结构 / 环形缓冲 / 分层预聚合"预留的把手：调用方只认
    /// "打开一个范围、逐个 MoveNext"，底层是数组直通、桶、还是 ring，都换得来。
    /// 默认实现（<see cref="XChartDataModel"/> 直通）就是按下标走记录表。</para>
    /// </summary>
    public struct XChartRangeCursor
    {
        internal IXChartDataSource Source;
        internal int Dimension;
        internal int EndExclusive;

        /// <summary>当前记录下标；开始时等于范围的起点</summary>
        public int Index;

        /// <summary>游标是否还有当前位置</summary>
        public bool IsValid => Source != null && Index >= 0 && Index < EndExclusive;

        /// <summary>当前记录的时间戳</summary>
        public double Time => Source != null ? Source.GetTimeAt(Index) : double.NaN;

        /// <summary>当前记录的数值（打开游标时指定的维度）</summary>
        public double Value => Source != null ? Source.GetValueAt(Index, Dimension) : double.NaN;

        /// <summary>前进一格；返回新位置是否有效</summary>
        public bool MoveNext()
        {
            Index++;
            return IsValid;
        }

        /// <summary>范围内剩余记录数（含当前）</summary>
        public int Remaining => Source == null ? 0 : Math.Max(0, EndExclusive - Index);
    }

    /// <summary>
    /// 图表数据访问的<b>唯一中转口</b>。
    ///
    /// <para><b>为什么要有它</b>：元素（网格背景 / 轴线 / 数据图元）不该直接摸记录数组。
    /// 一旦它们只认这个接口，"数据怎么存"就变成可替换的实现细节——今天是数组直通，
    /// 明天可以是一个包着环形缓冲 + 桶统计的实现（<see cref="XChartTimeSeriesData"/> 已经
    /// 具备那套能力），元素一行都不用改。</para>
    ///
    /// <para><b>版本号是脏判定的统一依据</b>：任何写入都会让 <see cref="Version"/> 递增，
    /// 需要缓存的元素只需比对这个号，不要再自己发明"成员变了吗"这类判据。</para>
    /// </summary>
    public interface IXChartDataSource
    {
        /// <summary>数据版本号：每次写入递增</summary>
        long Version { get; }

        /// <summary>数值维度数量</summary>
        int NumericDimensionCount { get; }

        /// <summary>
        /// 标签（String）列数量＝每条记录身上标签数组的长度。<b>元素只能读到这里说得出的那些格</b>：
        /// 记录里比这更多的字符串没有一个列能叫出名字，所以这一数就是"1:1"的形状判据。
        /// </summary>
        int LabelDimensionCount { get; }

        /// <summary>是否有数据</summary>
        bool HasData { get; }

        /// <summary>记录条数</summary>
        int RecordCount { get; }

        /// <summary>本实现能直接给出的聚合集合（其余可现算，或声明不支持）</summary>
        XChartAggregateMask SupportedAggregates { get; }

        /// <summary>
        /// 取某一维的聚合量。默认实现一次扫描算出 Count/First/Last/Min/Max/Sum/Rms，
        /// 中位数另需排序（桶内样本数小，直接排序即可）。
        /// </summary>
        bool TryGetAggregates(int dimension, XChartAggregateMask mask, out XChartAggregates aggregates);

        /// <summary>取某条记录的时间戳（直通实现不需要映射，直接读）</summary>
        double GetTimeAt(int recordIndex);

        /// <summary>取某条记录某维的数值</summary>
        double GetValueAt(int recordIndex, int dimension);

        /// <summary>
        /// 取某一维的作者元数据（显示名 / 格式化串 / 维度类型）。轴线的"数据直驱标签"
        /// 用它拿默认格式，标签模式用它拿显示名。下标越界返回 false（不抛，渲染路径里调用）。
        /// </summary>
        bool TryGetDimension(int dimension, out XChartDimensionDefinition definition);

        /// <summary>
        /// 取某条记录在某一标签列上的字符串。<b>下标是标签下标</b>（同一个口径：
        /// <see cref="XChartColumnMath.LabelSlotOf"/> 把"表里那一列"换算成它）。
        /// 越界返回 false（渲染路径里调用，不抛）——"这一格没有"和"这一格是空串"要分得开，
        /// 所以空串照实返回 true，由调用方决定要不要画。
        /// </summary>
        bool TryGetLabelAt(int recordIndex, int labelSlot, out string text);

        /// <summary>按时间窗打开一个范围游标（虚拟指针）</summary>
        XChartRangeCursor OpenRange(int dimension, double fromTime, double toTime);
    }
}