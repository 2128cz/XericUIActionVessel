using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace XericUI.XChart
{
    public enum XChartDimensionKind
    {
        Number,
        Time,
        Label,
    }

    [Serializable]
    public sealed class XChartDimensionDefinition
    {
        [SerializeField] private string m_Key;
        [SerializeField] private string m_DisplayName;
        [SerializeField] private XChartDimensionKind m_Kind;
        [SerializeField] private string m_Format = "0.##";
        [SerializeField] private bool m_Visible = true;

        // ── 显示范围（归 chart 的数据维度行：工具只呈现，不自己声明范围）────────
        // 两端<b>各自</b>可以钉住："最小值必须是 0、最大值跟着数据跑"是常态，
        // 而一个总的"覆盖范围"开关只表达得出"两端都钉住"或"两端都自由"。
        [SerializeField] private bool m_OverrideRange;   // 旧：两端一起覆盖（分侧开关一旦写过就清掉它）
        [SerializeField] private bool m_OverrideMin;
        [SerializeField] private bool m_OverrideMax;
        [SerializeField] private Vector2 m_ManualRange = Vector2.zero;

        public string Key => m_Key;
        /// <summary>
        /// 显示名。<b>可写</b>是为了"改列标题"：改的只是显示名，维度序号与 key 都不动
        /// （序号是数据的事实——工具的维度绑定按序号走）。
        /// </summary>
        public string DisplayName
        {
            get => m_DisplayName;
            set => m_DisplayName = value;
        }
        public XChartDimensionKind Kind => m_Kind;

        /// <summary>数值格式串（可写：文本导入要能把格式一起带回来）</summary>
        public string Format
        {
            get => m_Format;
            set => m_Format = value;
        }

        public bool Visible => m_Visible;

        /// <summary>旧口径：两端一起覆盖。留着是因为已有场景里带这个字段，而且它＝两个分侧开关同时为真</summary>
        public bool OverrideRange
        {
            get => m_OverrideRange;
            set => m_OverrideRange = value;
        }

        /// <summary>覆盖范围的两个端点（<c>x</c>=最小、<c>y</c>=最大）。<b>没钉住的那一端这里的值不参与解析</b>，
        /// 但要保持可读——数据窗口里两个格子随时显示这里存的数，勾上就立刻生效。</summary>
        public Vector2 ManualRange
        {
            get => m_ManualRange;
            set => m_ManualRange = value;
        }

        /// <summary>旧主开关的生效条件：勾上了<b>而且</b>两端确实填了不同的值（"勾了但没填"不算覆盖，
        /// 这条规则从 P4 起就在，它保住了"默认 0/0 状态不会凭空造出一段范围"）</summary>
        private bool LegacyBothPinned => m_OverrideRange && m_ManualRange.x != m_ManualRange.y;

        /// <summary>这一端现在是不是被钉住了（旧的主开关两端一起算）</summary>
        public bool OverrideMin => LegacyBothPinned || m_OverrideMin;

        /// <summary>同上：最大端</summary>
        public bool OverrideMax => LegacyBothPinned || m_OverrideMax;

        /// <summary>
        /// 交给域解析的覆盖请求（<b>唯一出口</b>）：没钉住的端由 <see cref="XChartDomainMath"/> 回落到该维聚合。
        /// </summary>
        public XChartDomainOverride DomainOverride
            => new XChartDomainOverride(OverrideMin, m_ManualRange.x, OverrideMax, m_ManualRange.y);

        /// <summary>
        /// 分侧设覆盖：<paramref name="min"/> / <paramref name="max"/> <b>总是留着</b>（哪怕这一端没钉），
        /// 所以"先填好数、再勾选"和"先勾选、再改数"两条路都不会把另一端的数字抹掉。
        /// </summary>
        public void SetDomainOverride(bool overrideMin, double min, bool overrideMax, double max)
        {
            m_OverrideRange = false;   // 分侧开关写过之后，"两端一起"这个旧状态就不再参与
            m_OverrideMin = overrideMin;
            m_OverrideMax = overrideMax;
            m_ManualRange = new Vector2((float)min, (float)max);
        }

        /// <summary>脚本侧一次设完两端（<paramref name="min"/> == <paramref name="max"/> 表示不覆盖）</summary>
        public void SetRangeOverride(double min, double max)
        {
            bool over = min != max;
            m_OverrideRange = over;
            m_ManualRange = new Vector2((float)min, (float)max);
        }

        public XChartDimensionDefinition(string key, string displayName, XChartDimensionKind kind)
        {
            m_Key = key;
            m_DisplayName = displayName;
            m_Kind = kind;
        }
    }

    [Serializable]
    public sealed class XChartRecord
    {
        [SerializeField] private long m_RecordId;
        [SerializeField] private double m_Timestamp;
        [SerializeField] private float[] m_Values = Array.Empty<float>();
        [SerializeField] private string[] m_Labels = Array.Empty<string>();

        public long RecordId => m_RecordId;
        public double Timestamp => m_Timestamp;
        public IReadOnlyList<float> Values => m_Values;
        public IReadOnlyList<string> Labels => m_Labels;

        internal XChartRecord(long recordId, double timestamp, float[] values, string[] labels)
        {
            m_RecordId = recordId;
            m_Timestamp = timestamp;
            m_Values = values;
            m_Labels = labels;
        }
    }

    public sealed class XChartDataSnapshot : IDisposable
    {
        public NativeArray<double> Times { get; private set; }
        public NativeArray<long> RecordIds { get; private set; }
        public NativeArray<float> Values { get; private set; }
        public int RecordCount { get; private set; }
        public int NumericDimensionCount { get; private set; }
        public long Version { get; private set; }

        internal XChartDataSnapshot(IReadOnlyList<XChartRecord> records, int numericDimensionCount, long version)
        {
            RecordCount = records.Count;
            NumericDimensionCount = numericDimensionCount;
            Version = version;
            var times = new NativeArray<double>(RecordCount, Allocator.Persistent);
            var recordIds = new NativeArray<long>(RecordCount, Allocator.Persistent);
            var values = new NativeArray<float>(RecordCount * numericDimensionCount, Allocator.Persistent);
            for (int i = 0; i < RecordCount; i++)
            {
                times[i] = records[i].Timestamp;
                recordIds[i] = records[i].RecordId;
                for (int j = 0; j < numericDimensionCount; j++) values[i * numericDimensionCount + j] = records[i].Values[j];
            }
            Times = times;
            RecordIds = recordIds;
            Values = values;
        }

        public void Dispose()
        {
            if (Times.IsCreated) Times.Dispose();
            if (RecordIds.IsCreated) RecordIds.Dispose();
            if (Values.IsCreated) Values.Dispose();
        }
    }

    /// <summary>
    /// 图表的默认数据模型：维度定义 + 记录表 + 版本号，并作为 <see cref="IXChartDataSource"/> 的
    /// <b>直通实现</b>（不做任何映射，范围游标就是下标遍历，聚合现算）。
    ///
    /// 记录表按时间非递减（<see cref="TryAddRecord"/> 强制），所以时间窗可以直接二分定位。
    /// </summary>
    [Serializable]
    public sealed class XChartDataModel : IXChartDataSource
    {
        [SerializeField] private int m_Capacity = 4096;
        [SerializeField] private List<XChartDimensionDefinition> m_Dimensions = new List<XChartDimensionDefinition>();
        [SerializeField] private List<XChartRecord> m_Records = new List<XChartRecord>();
        [NonSerialized] private XChartDataSnapshot m_Snapshot;
        [NonSerialized] private long m_SnapshotVersion = -1;
        [NonSerialized] private long m_Version;
        [NonSerialized] private long m_NextRecordId = 1;
        [NonSerialized] private long m_DroppedCount;

        // 聚合缓存：写一次改版本，同一版本内重复查询（同一帧里多个元素问同一个维度）不重算
        [NonSerialized] private long m_AggregateVersion = -1;
        [NonSerialized] private int m_AggregateDimension = -1;
        [NonSerialized] private XChartAggregates m_AggregateCache;
        [NonSerialized] private double[] m_MedianScratch;

        public IReadOnlyList<XChartDimensionDefinition> Dimensions => m_Dimensions;
        public IReadOnlyList<XChartRecord> Records => m_Records;

        /// <summary>
        /// 只读快照（NativeArray）。惰性构建：写入时只标脏，谁先要谁触发一次重建。
        /// 旧快照在新快照建好之后才释放，避免把手里的 NativeArray 提前释放掉。
        /// </summary>
        public XChartDataSnapshot Snapshot
        {
            get
            {
                EnsureSnapshot();
                return m_Snapshot;
            }
        }

        public long Version => m_Version;
        public int Capacity { get => m_Capacity; set => m_Capacity = Mathf.Max(1, value); }

        /// <summary>
        /// 因为触顶被丢掉的记录条数（累计事实，不会因为改大 Capacity 而减少——那些记录已经不在表里了）。
        /// 现场就是靠它解释"我改了容量怎么还是 4096 条"：丢掉的找不回来，得重新灌。
        /// </summary>
        public long DroppedCount => m_DroppedCount;

        /// <summary>
        /// 数值维个数＝每条记录值数组的长度。口径只有一份（<see cref="XChartColumnMath.ValueCount"/>），
        /// 数据窗口按同一个函数决定"哪一列有值格"，于是这里的数和表上的列不会各说各话。
        /// </summary>
        public int NumericDimensionCount => XChartColumnMath.ValueCount(m_Dimensions);

        /// <summary>
        /// 标签列个数＝每条记录标签数组的长度（<see cref="XChartColumnMath.LabelCount"/>，唯一那一条规则）。
        /// <b>1:1 是形状而不是一句承诺</b>：记录身上可以写出比标签列更多的字符串，那些字符串没有一个列
        /// 叫得出名字，屏幕上就凭空多出一个标签，而报表里看不出是谁写的。
        /// </summary>
        public int LabelDimensionCount => XChartColumnMath.LabelCount(m_Dimensions);

        public void SetDimensions(IList<XChartDimensionDefinition> dimensions)
        {
            m_Dimensions.Clear();
            if (dimensions != null) for (int i = 0; i < dimensions.Count; i++) m_Dimensions.Add(dimensions[i]);
            int numericCount = NumericDimensionCount;
            int labelCount = LabelDimensionCount;
            for (int i = 0; i < m_Records.Count; i++)
            {
                var values = new float[numericCount];
                int copyCount = Math.Min(values.Length, m_Records[i].Values.Count);
                for (int j = 0; j < copyCount; j++) values[j] = m_Records[i].Values[j];
                m_Records[i] = new XChartRecord(m_Records[i].RecordId, m_Records[i].Timestamp, values,
                    XChartColumnMath.SizedLabels(m_Records[i].Labels, labelCount));
            }
            Publish();
        }

        /// <summary>
        /// 给某一维改<b>显示名</b>（列标题）。序号与 key 都不动——序号是数据的事实，
        /// 工具的维度绑定按序号走；空名拒收（列标题空了就认不出是哪一列）。
        /// </summary>
        public bool TryRenameDimension(int index, string displayName)
        {
            if (index < 0 || index >= m_Dimensions.Count) return false;
            if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(displayName.Trim())) return false;

            m_Dimensions[index].DisplayName = displayName.Trim();
            Publish();
            return true;
        }

        /// <summary>
        /// 给某一维设<b>分侧覆盖</b>（数据窗口的两个勾选框走这里）。下标是<b>值下标</b>
        /// （与 <see cref="TryGetDimension"/> 一致）；非有限数一律拒绝：覆盖值会被当成坐标算，
        /// NaN 进去就是整条轴安静地什么都不画。
        /// </summary>
        public bool TrySetDomainOverride(int dimension, bool overrideMin, double min, bool overrideMax, double max)
        {
            var definition = XChartColumnMath.DefinitionOfValueSlot(m_Dimensions, dimension);
            if (definition == null) return false;
            if (double.IsNaN(min) || double.IsInfinity(min) || double.IsNaN(max) || double.IsInfinity(max)) return false;

            definition.SetDomainOverride(overrideMin, min, overrideMax, max);
            Publish();   // 范围变了：按版本缓存聚合/快照的工具要重读
            return true;
        }

        /// <summary>
        /// 删掉一列（＝一个维度定义）。数值列里对应的那一格会跟着从每条记录上摘掉，
        /// 所以删完之后每一行的值个数还是等于数值维个数。
        ///
        /// <para><b>序号会整体前移</b>：删第 0 列，原来的第 1 列就成了第 0 列——而工具的维度绑定按序号走
        /// （轴的 Dimension、圆柱的 X/Y/Z）。这里不回改绑定，只把事实做对；绑定要不要跟着挪是决定层的事。</para>
        /// </summary>
        public bool TryRemoveDimension(int index, out string reason)
        {
            reason = null;
            if (index < 0 || index >= m_Dimensions.Count)
            {
                reason = "列号越界（现在一共 " + m_Dimensions.Count + " 列）";
                return false;
            }

            var definition = m_Dimensions[index];
            if (definition == null)
            {
                reason = "第 " + index + " 列的定义是空的，删不动";
                return false;
            }

            // 值数组按"数值维中的序号"排，不是按维度表下标排：中间夹一个非数值维时两者会错开
            // 值下标由那一条规则给（非数值列返回 -1：本来就没有格要摘）
            int column = XChartColumnMath.ValueSlot(m_Dimensions, index);
            int labelSlot = XChartColumnMath.LabelSlotOf(m_Dimensions, index);

            m_Dimensions.RemoveAt(index);

            if (column >= 0 || labelSlot >= 0)
            {
                for (int i = 0; i < m_Records.Count; i++)
                {
                    var record = m_Records[i];

                    // 标签列同理：摘掉的是"这一列在标签数组里那一格"，不是"第 index 个字符串"
                    var values = new float[column >= 0 ? record.Values.Count - 1 : record.Values.Count];
                    for (int j = 0, k = 0; j < record.Values.Count; j++)
                        if (column < 0 || j != column) values[k++] = record.Values[j];

                    var labels = new string[labelSlot >= 0 ? record.Labels.Count - 1 : record.Labels.Count];
                    for (int j = 0, k = 0; j < record.Labels.Count; j++)
                        if (labelSlot < 0 || j != labelSlot) labels[k++] = record.Labels[j];

                    m_Records[i] = new XChartRecord(record.RecordId, record.Timestamp, values, CopyLabels(labels));
                }
            }

            Publish();
            return true;
        }

        /// <summary>
        /// 加一条记录。两个数组都必须<b>对得上当前的列</b>：值数组长度＝数值列个数，
        /// 标签数组长度＝标签列个数（或 <c>null</c>＝这一条还没有标签）。
        /// 长度不对就整条拒收、不改一半——"多出来的那个字符串"没有列叫得出它的名字，
        /// 落进表里就是屏幕上一个没主的标签（1:1 靠门口这一关守住，不靠调用方自觉）。
        /// </summary>
        public bool TryAddRecord(double timestamp, float[] values, string[] labels = null)
        {
            if (double.IsNaN(timestamp) || double.IsInfinity(timestamp) || values == null || values.Length != NumericDimensionCount) return false;
            for (int i = 0; i < values.Length; i++) if (float.IsNaN(values[i]) || float.IsInfinity(values[i])) return false;
            if (labels != null && labels.Length != LabelDimensionCount) return false;
            if (m_Records.Count > 0 && timestamp < m_Records[m_Records.Count - 1].Timestamp) return false;
            if (m_Records.Count == Capacity)
            {
                m_Records.RemoveAt(0);
                m_DroppedCount++;
            }
            var copy = new float[values.Length];
            Array.Copy(values, copy, values.Length);
            m_Records.Add(new XChartRecord(m_NextRecordId++, timestamp, copy, CopyLabels(labels)));
            Publish();
            return true;
        }

        public void Clear()
        {
            m_Records.Clear();
            Publish();
        }

        // ── 就地编辑（数据窗口用；改完就 Publish，让工具按新版本重读）──────

        /// <summary>改某条记录的某个数值维。<b>越界或 NaN/Infinity 一律拒绝</b>，不半改。</summary>
        public bool TrySetValue(int recordIndex, int numericDimension, double value)
        {
            if (recordIndex < 0 || recordIndex >= m_Records.Count) return false;
            var record = m_Records[recordIndex];
            if (numericDimension < 0 || numericDimension >= record.Values.Count) return false;
            if (double.IsNaN(value) || double.IsInfinity(value)) return false;

            var values = CopyValues(record.Values);
            values[numericDimension] = (float)value;
            m_Records[recordIndex] = new XChartRecord(record.RecordId, record.Timestamp, values, CopyLabels(record.Labels));
            Publish();
            return true;
        }

        /// <summary>
        /// 改时间戳。记录表必须<b>时间非递减</b>：新值要落在左右邻居之间（含端点）才接受，
        /// 否则拒绝并把原因说清——把它放宽会让"时间窗二分定位"整个失效。
        /// </summary>
        public bool TrySetTime(int recordIndex, double time, out string reason)
        {
            reason = null;
            if (recordIndex < 0 || recordIndex >= m_Records.Count) { reason = "行号越界"; return false; }
            if (double.IsNaN(time) || double.IsInfinity(time)) { reason = "不是有限数"; return false; }

            double lower = recordIndex > 0 ? m_Records[recordIndex - 1].Timestamp : double.NegativeInfinity;
            double upper = recordIndex + 1 < m_Records.Count ? m_Records[recordIndex + 1].Timestamp : double.PositiveInfinity;
            if (time < lower || time > upper)
            {
                reason = "要让时间保持非递减：得落在 " + lower.ToString("0.###") + " 与 " + upper.ToString("0.###") + " 之间";
                return false;
            }

            var record = m_Records[recordIndex];
            m_Records[recordIndex] = new XChartRecord(record.RecordId, time, CopyValues(record.Values), CopyLabels(record.Labels));
            Publish();
            return true;
        }

        /// <summary>
        /// 改某条记录在某一标签列上的字符串。<b>下标是标签下标</b>（第几个标签列，不是表里的行号），
        /// 由 <see cref="XChartColumnMath.LabelSlotOf"/> 换算。行号或下标越界拒绝；<b>空串是合法值</b>
        /// （把标签清空），所以清空与"这一列本来没有格"分得开——后者压根进不到这里。
        /// </summary>
        public bool TrySetLabel(int recordIndex, int labelSlot, string text)
        {
            if (recordIndex < 0 || recordIndex >= m_Records.Count) return false;
            var record = m_Records[recordIndex];
            if (labelSlot < 0 || labelSlot >= LabelDimensionCount) return false;

            var labels = CopyLabels(record.Labels);
            labels[labelSlot] = text ?? string.Empty;
            m_Records[recordIndex] = new XChartRecord(record.RecordId, record.Timestamp, CopyValues(record.Values), labels);
            Publish();
            return true;
        }

        /// <summary>
        /// 删一条记录。<b>时间非递减不会被破坏</b>：有序序列去掉一个元素还是有序的，
        /// 所以时间窗的二分定位照旧成立（这也是为什么这里不需要重排）。
        /// </summary>
        public bool TryRemoveRecord(int recordIndex, out string reason)
        {
            reason = null;
            if (recordIndex < 0 || recordIndex >= m_Records.Count)
            {
                reason = "行号越界（现在一共 " + m_Records.Count + " 行）";
                return false;
            }

            m_Records.RemoveAt(recordIndex);
            Publish();
            return true;
        }

        private static float[] CopyValues(IReadOnlyList<float> values)
        {
            var copy = new float[values.Count];
            for (int i = 0; i < values.Count; i++) copy[i] = values[i];
            return copy;
        }

        /// <summary>
        /// 标记数据已变更：只递增版本号并失效缓存。
        /// 以前这里每次写入都会重建一次 NativeArray 快照（O(n) 分配），流式写入下开销随条数线性增长；
        /// 现在改成惰性，谁读 <see cref="Snapshot"/> 谁触发一次重建。
        /// </summary>
        public void Publish()
        {
            m_Version++;
            m_SnapshotVersion = -1;
            m_AggregateVersion = -1;
        }

        private void EnsureSnapshot()
        {
            if (m_Snapshot != null && m_SnapshotVersion == m_Version) return;
            var oldSnapshot = m_Snapshot;
            m_Snapshot = new XChartDataSnapshot(m_Records, NumericDimensionCount, m_Version);
            m_SnapshotVersion = m_Version;
            oldSnapshot?.Dispose();
        }

        public void DisposeRuntime()
        {
            m_Snapshot?.Dispose();
            m_Snapshot = null;
            m_SnapshotVersion = -1;
            m_AggregateVersion = -1;
        }

        // ── IXChartDataSource（直通实现） ─────────────────────────

        /// <summary>直通实现：记录表本身就是存储，不需要任何映射层</summary>
        public int RecordCount => m_Records.Count;

        /// <summary>直通实现：无映射，任何条数都能取</summary>
        public bool HasData => m_Records.Count > 0;

        /// <summary>直通实现能当场给出全部聚合（含中位数，按需求算）</summary>
        public XChartAggregateMask SupportedAggregates => XChartAggregateMask.All;

        /// <summary>取某条记录的时间戳</summary>
        public double GetTimeAt(int recordIndex)
        {
            if (recordIndex < 0 || recordIndex >= m_Records.Count) return double.NaN;
            return m_Records[recordIndex].Timestamp;
        }

        /// <summary>取某条记录某维的数值</summary>
        public double GetValueAt(int recordIndex, int dimension)
        {
            if (recordIndex < 0 || recordIndex >= m_Records.Count) return double.NaN;
            var values = m_Records[recordIndex].Values;
            if (dimension < 0 || dimension >= values.Count) return double.NaN;
            return values[dimension];
        }

        /// <summary>
        /// 取某一维的作者元数据。<b>这里的下标是值下标</b>（与 <see cref="GetValueAt"/> /
        /// <see cref="TryGetAggregates"/> 同一个口径：工具绑的就是这一个数），所以表里夹着
        /// Label/Time 列时，它给的是"第 N 个数值列"那一行，而不是维度表第 N 行
        /// （换算只有一处：<see cref="XChartColumnMath.DefinitionOfValueSlot"/>）。
        /// </summary>
        public bool TryGetDimension(int dimension, out XChartDimensionDefinition definition)
        {
            definition = XChartColumnMath.DefinitionOfValueSlot(m_Dimensions, dimension);
            return definition != null;
        }

        /// <summary>
        /// 取某条记录在某一标签列上的字符串（<b>标签下标</b>，第几个标签列）。
        /// 行号或下标越界返回 false；列存在但那一格是空串时返回 true + 空串——
        /// "这一列没这一格"和"这一格是空的"是两件事，画不画由调用方决定。
        /// </summary>
        public bool TryGetLabelAt(int recordIndex, int labelSlot, out string text)
        {
            text = null;
            if (recordIndex < 0 || recordIndex >= m_Records.Count) return false;
            if (labelSlot < 0 || labelSlot >= LabelDimensionCount) return false;
            text = m_Records[recordIndex].Labels[labelSlot] ?? string.Empty;
            return true;
        }

        /// <summary>
        /// 一次扫描算 Count/First/Last/Min/Max/Sum/Mean/Rms；中位数单独排序一次。
        /// 结果按（版本, 维度）缓存，请求的是缓存子集时直接复用。
        /// </summary>
        public bool TryGetAggregates(int dimension, XChartAggregateMask mask, out XChartAggregates aggregates)
        {
            aggregates = default;
            if (mask == XChartAggregateMask.None) return false;
            if (dimension < 0 || dimension >= NumericDimensionCount) return false;

            if (m_AggregateVersion == m_Version && m_AggregateDimension == dimension
                && (mask & ~m_AggregateCache.Computed) == XChartAggregateMask.None)
            {
                aggregates = m_AggregateCache;
                return true;
            }

            int count = m_Records.Count;
            double minimum = double.MaxValue, maximum = double.MinValue;
            double sum = 0d, squareSum = 0d;
            double first = double.NaN, last = double.NaN;
            int valid = 0;

            for (int i = 0; i < count; i++)
            {
                double value = GetValueAt(i, dimension);
                if (double.IsNaN(value) || double.IsInfinity(value)) continue;
                if (valid == 0) first = value;
                last = value;
                if (value < minimum) minimum = value;
                if (value > maximum) maximum = value;
                sum += value;
                squareSum += value * value;
                valid++;
            }

            var result = default(XChartAggregates);
            if (valid == 0)
            {
                result.Set(XChartAggregateKind.Count, 0);
                if ((mask & XChartAggregateMask.First) != 0) result.Set(XChartAggregateKind.First, double.NaN);
                if ((mask & XChartAggregateMask.Last) != 0) result.Set(XChartAggregateKind.Last, double.NaN);
                if ((mask & XChartAggregateMask.Minimum) != 0) result.Set(XChartAggregateKind.Minimum, double.NaN);
                if ((mask & XChartAggregateMask.Maximum) != 0) result.Set(XChartAggregateKind.Maximum, double.NaN);
                if ((mask & XChartAggregateMask.Sum) != 0) result.Set(XChartAggregateKind.Sum, 0d);
                if ((mask & XChartAggregateMask.Mean) != 0) result.Set(XChartAggregateKind.Mean, double.NaN);
                if ((mask & XChartAggregateMask.Rms) != 0) result.Set(XChartAggregateKind.Rms, double.NaN);
                if ((mask & XChartAggregateMask.Median) != 0) result.Set(XChartAggregateKind.Median, double.NaN);
            }
            else
            {
                if ((mask & XChartAggregateMask.Count) != 0) result.Set(XChartAggregateKind.Count, valid);
                if ((mask & XChartAggregateMask.First) != 0) result.Set(XChartAggregateKind.First, first);
                if ((mask & XChartAggregateMask.Last) != 0) result.Set(XChartAggregateKind.Last, last);
                if ((mask & XChartAggregateMask.Minimum) != 0) result.Set(XChartAggregateKind.Minimum, minimum);
                if ((mask & XChartAggregateMask.Maximum) != 0) result.Set(XChartAggregateKind.Maximum, maximum);
                if ((mask & XChartAggregateMask.Sum) != 0) result.Set(XChartAggregateKind.Sum, sum);
                if ((mask & XChartAggregateMask.Mean) != 0) result.Set(XChartAggregateKind.Mean, sum / valid);
                if ((mask & XChartAggregateMask.Rms) != 0) result.Set(XChartAggregateKind.Rms, Math.Sqrt(squareSum / valid));
                if ((mask & XChartAggregateMask.Median) != 0) result.Set(XChartAggregateKind.Median, ComputeMedian(dimension));
            }

            m_AggregateVersion = m_Version;
            m_AggregateDimension = dimension;
            m_AggregateCache = result;
            aggregates = result;
            return true;
        }

        /// <summary>
        /// 中位数：把该维的有效样本拷进复用的 scratch 数组排序后取中位。
        /// 桶/窗口级样本量小，排序足够；真要全量流式中位数再考虑 P² / t-digest。
        /// </summary>
        private double ComputeMedian(int dimension)
        {
            int count = m_Records.Count;
            int valid = 0;
            if (m_MedianScratch == null || m_MedianScratch.Length < count)
                m_MedianScratch = new double[Mathf.Max(count, 16)];

            for (int i = 0; i < count; i++)
            {
                double value = GetValueAt(i, dimension);
                if (double.IsNaN(value) || double.IsInfinity(value)) continue;
                m_MedianScratch[valid++] = value;
            }
            if (valid == 0) return double.NaN;

            Array.Sort(m_MedianScratch, 0, valid);
            int middle = valid / 2;
            if ((valid & 1) == 1) return m_MedianScratch[middle];
            return (m_MedianScratch[middle - 1] + m_MedianScratch[middle]) * 0.5d;
        }

        /// <summary>
        /// 直通的范围游标：时间非递减（写入时强制），二分定位窗口。
        /// 以后换成桶/环形缓冲实现时，外面这一层调用方式不变。
        /// </summary>
        public XChartRangeCursor OpenRange(int dimension, double fromTime, double toTime)
        {
            var cursor = new XChartRangeCursor
            {
                Source = this,
                Dimension = dimension,
                Index = LowerBound(fromTime),
                EndExclusive = UpperBound(toTime),
            };
            return cursor;
        }

        /// <summary>第一个时间戳 &gt;= time 的下标</summary>
        private int LowerBound(double time)
        {
            int low = 0, high = m_Records.Count;
            while (low < high)
            {
                int mid = (low + high) / 2;
                if (m_Records[mid].Timestamp < time) low = mid + 1;
                else high = mid;
            }
            return low;
        }

        /// <summary>第一个时间戳 &gt; time 的下标</summary>
        private int UpperBound(double time)
        {
            int low = 0, high = m_Records.Count;
            while (low < high)
            {
                int mid = (low + high) / 2;
                if (m_Records[mid].Timestamp <= time) low = mid + 1;
                else high = mid;
            }
            return low;
        }

        /// <summary>标签数组的拷贝<b>并且按那一条规则定长</b>——长度只由标签列个数决定，
        /// 所以任何一次写入之后，"这一行的标签个数"都还等于表里的标签列数。</summary>
        private string[] CopyLabels(IReadOnlyList<string> labels)
            => XChartColumnMath.SizedLabels(labels, LabelDimensionCount);
    }

    public class XChartDataModel3D
    {
        [Serializable]
        public struct DataPoint
        {
            public double x;
            public double y;
            public double z;

            public DataPoint(double x, double y, double z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }

            public Vector3 ToVector3() => new Vector3((float)x, (float)y, (float)z);
        }

        private List<DataPoint> m_Points = new List<DataPoint>();
        public IReadOnlyList<DataPoint> Points => m_Points;
        public int PointCount => m_Points.Count;
        public bool HasData => m_Points.Count > 0;
        public double MinX { get; private set; }
        public double MaxX { get; private set; }
        public double MinY { get; private set; }
        public double MaxY { get; private set; }
        public double MinZ { get; private set; }
        public double MaxZ { get; private set; }
        public double RangeX => MaxX - MinX;
        public double RangeY => MaxY - MinY;
        public double RangeZ => MaxZ - MinZ;
        public double MaxRange => Math.Max(Math.Max(RangeX, RangeY), RangeZ);

        [Flags]
        public enum DirtyFlags
        {
            None = 0,
            DataChanged = 1 << 0,
            RangeChanged = 1 << 1,
            All = DataChanged | RangeChanged,
        }

        public DirtyFlags CurrentDirty { get; private set; }
        public void MarkDirty(DirtyFlags flag) { CurrentDirty |= flag; }
        public void ClearDirty(DirtyFlags flag) { CurrentDirty &= ~flag; }
        public bool IsDirty(DirtyFlags flag) => (CurrentDirty & flag) != 0;

        public void SetPoints(IList<DataPoint> points)
        {
            m_Points.Clear();
            if (points != null) for (int i = 0; i < points.Count; i++) m_Points.Add(points[i]);
            RecalculateRange();
            MarkDirty(DirtyFlags.All);
        }

        public void SetPoints(double[] xs, double[] ys, double[] zs)
        {
            m_Points.Clear();
            int count = Math.Min(Math.Min(xs.Length, ys.Length), zs.Length);
            for (int i = 0; i < count; i++) m_Points.Add(new DataPoint(xs[i], ys[i], zs[i]));
            RecalculateRange();
            MarkDirty(DirtyFlags.All);
        }

        public void AddPoint(double x, double y, double z)
        {
            m_Points.Add(new DataPoint(x, y, z));
            UpdateRangeForPoint(x, y, z);
            MarkDirty(DirtyFlags.DataChanged);
        }

        public void ClearAll()
        {
            m_Points.Clear();
            MinX = MaxX = MinY = MaxY = MinZ = MaxZ = 0;
            MarkDirty(DirtyFlags.All);
        }

        public Vector3[] GetNormalizedPoints()
        {
            if (m_Points.Count == 0) return Array.Empty<Vector3>();
            double range = MaxRange;
            if (range < double.Epsilon) range = 1.0;
            double cx = (MinX + MaxX) * 0.5;
            double cy = (MinY + MaxY) * 0.5;
            double cz = (MinZ + MaxZ) * 0.5;
            var result = new Vector3[m_Points.Count];
            for (int i = 0; i < m_Points.Count; i++)
                result[i] = new Vector3((float)((m_Points[i].x - cx) / range * 2.0), (float)((m_Points[i].y - cy) / range * 2.0), (float)((m_Points[i].z - cz) / range * 2.0));
            return result;
        }

        private void RecalculateRange()
        {
            if (m_Points.Count == 0)
            {
                MinX = MaxX = MinY = MaxY = MinZ = MaxZ = 0;
                return;
            }
            MinX = MaxX = m_Points[0].x;
            MinY = MaxY = m_Points[0].y;
            MinZ = MaxZ = m_Points[0].z;
            for (int i = 1; i < m_Points.Count; i++) UpdateRangeForPoint(m_Points[i].x, m_Points[i].y, m_Points[i].z);
        }

        private void UpdateRangeForPoint(double x, double y, double z)
        {
            if (x < MinX) MinX = x;
            if (x > MaxX) MaxX = x;
            if (y < MinY) MinY = y;
            if (y > MaxY) MaxY = y;
            if (z < MinZ) MinZ = z;
            if (z > MaxZ) MaxZ = z;
        }
    }
}
