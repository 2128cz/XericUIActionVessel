using System;
using System.Collections.Generic;

namespace XericUI.XChart
{
    /// <summary>输入值不可用于绘图或统计时的处理方式。</summary>
    public enum XChartInvalidSamplePolicy
    {
        Reject,
        StoreGap,
        ClampToLastFinite,
    }

    /// <summary>按时间排列的单个时序样本。无效样本是折线断开标记，不参与数值统计。</summary>
    public struct XChartTimeSample
    {
        public readonly double Time;
        public readonly double Value;
        public readonly bool IsValid;

        public XChartTimeSample(double time, double value, bool isValid = true)
        {
            Time = time;
            Value = value;
            IsValid = isValid;
        }
    }

    [Flags]
    public enum XChartStatisticMask
    {
        None = 0,
        First = 1 << 0,
        Last = 1 << 1,
        Minimum = 1 << 2,
        Maximum = 1 << 3,
        Mean = 1 << 4,
        Sum = 1 << 5,
        Rms = 1 << 6,
        Count = 1 << 7,
        Default = First | Last | Minimum | Maximum | Mean | Count,
    }

    /// <summary>
    /// 一个屏幕列/时间桶的统计快照。绘制包络时使用 Minimum/Maximum；普通折线可选择 Mean 或 Last。
    /// BucketIndex 对应请求窗口的 [0, BucketCount) 横向列；HadInvalid 用于在此处断线。
    /// </summary>
    public struct XChartTimeBucket
    {
        public int BucketIndex;
        public int BucketCount;
        public int ValidCount;
        public int InvalidCount;
        public double StartTime;
        public double EndTime;
        public double First;
        public double Last;
        public double Minimum;
        public double Maximum;
        public double Sum;
        public double Mean;
        public double Rms;

        public bool HasValue => ValidCount > 0;
        public bool HadInvalid => InvalidCount > 0;
        public double NormalizedCenter => (BucketIndex + 0.5d) / BucketCount;

        internal void Add(in XChartTimeSample sample)
        {
            if (!sample.IsValid)
            {
                InvalidCount++;
                return;
            }

            if (ValidCount == 0)
            {
                First = Minimum = Maximum = sample.Value;
            }

            Last = sample.Value;
            Minimum = Math.Min(Minimum, sample.Value);
            Maximum = Math.Max(Maximum, sample.Value);
            Sum += sample.Value;
            Rms += sample.Value * sample.Value;
            ValidCount++;
        }

        internal void FinalizeStatistics()
        {
            if (ValidCount == 0)
            {
                First = Last = Minimum = Maximum = Sum = Mean = Rms = double.NaN;
                return;
            }

            Mean = Sum / ValidCount;
            Rms = Math.Sqrt(Rms / ValidCount);
        }
    }

    /// <summary>
    /// 面向实时图表的固定容量时序存储。
    ///
    /// 写入达到容量后覆盖最早数据；读取按显示窗口映射到固定数量的像素桶，并仅在数据版本或查询参数改变时重算。
    /// 它不持有 Unity 对象，可被后台采集线程调用；返回的数据被复制到调用方列表，避免暴露环形数组。
    /// </summary>
    public sealed class XChartTimeSeriesData
    {
        private readonly object m_Gate = new object();
        private readonly XChartTimeSample[] m_Ring;
        private readonly XChartTimeBucket[] m_WorkBuckets;
        private readonly List<XChartTimeBucket> m_CachedBuckets;
        private readonly int[] m_TouchedBucketIndices;

        private int m_Head;
        private int m_Count;
        private long m_Version;
        private bool m_HasLastFinite;
        private double m_LastFiniteValue;

        private long m_CachedVersion = -1;
        private double m_CachedStart;
        private double m_CachedEnd;
        private int m_CachedBucketCount;

        public XChartTimeSeriesData(int capacity, int maximumBucketCount = 2048,
            XChartInvalidSamplePolicy invalidSamplePolicy = XChartInvalidSamplePolicy.StoreGap)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (maximumBucketCount <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBucketCount));

            Capacity = capacity;
            MaximumBucketCount = maximumBucketCount;
            InvalidSamplePolicy = invalidSamplePolicy;
            m_Ring = new XChartTimeSample[capacity];
            m_WorkBuckets = new XChartTimeBucket[maximumBucketCount];
            m_TouchedBucketIndices = new int[maximumBucketCount];
            m_CachedBuckets = new List<XChartTimeBucket>(maximumBucketCount);
        }

        public int Capacity { get; }
        public int MaximumBucketCount { get; }
        public XChartInvalidSamplePolicy InvalidSamplePolicy { get; set; }
        public int Count { get { lock (m_Gate) return m_Count; } }
        public long Version { get { lock (m_Gate) return m_Version; } }
        private long m_DroppedSampleCount;
        public long DroppedSampleCount { get { lock (m_Gate) return m_DroppedSampleCount; } }

        /// <summary>写入一条样本。时间必须为有限数；非递增时间会被拒绝，保证窗口映射可预测。</summary>
        public bool TryAdd(double time, double value)
        {
            lock (m_Gate)
            {
                if (!IsFinite(time))
                {
                    m_DroppedSampleCount++;
                    return false;
                }

                if (m_Count > 0 && time <= GetLogicalSample(m_Count - 1).Time)
                {
                    m_DroppedSampleCount++;
                    return false;
                }

                bool valid = IsFinite(value);
                if (!valid && InvalidSamplePolicy == XChartInvalidSamplePolicy.Reject)
                {
                    m_DroppedSampleCount++;
                    return false;
                }

                if (!valid && InvalidSamplePolicy == XChartInvalidSamplePolicy.ClampToLastFinite && m_HasLastFinite)
                {
                    value = m_LastFiniteValue;
                    valid = true;
                }

                AddUnchecked(new XChartTimeSample(time, valid ? value : double.NaN, valid));
                if (valid)
                {
                    m_LastFiniteValue = value;
                    m_HasLastFinite = true;
                }
                m_Version++;
                return true;
            }
        }

        /// <summary>清除样本和缓存，不改变已配置容量。</summary>
        public void Clear()
        {
            lock (m_Gate)
            {
                m_Head = m_Count = 0;
                m_HasLastFinite = false;
                m_CachedVersion = -1;
                m_CachedBuckets.Clear();
                m_Version++;
            }
        }

        /// <summary>
        /// 返回 [startTime, endTime] 内的非空统计桶。bucketCount 通常设为绘图区实际像素宽度，超过上限会抛出异常以杜绝隐式扩容。
        /// 调用方应复用 output；方法不会分配新的托管数组。
        /// </summary>
        public void GetBuckets(double startTime, double endTime, int bucketCount, List<XChartTimeBucket> output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (!IsFinite(startTime) || !IsFinite(endTime) || endTime <= startTime)
                throw new ArgumentException("显示窗口必须为有限且递增的时间区间。");
            if (bucketCount <= 0 || bucketCount > MaximumBucketCount)
                throw new ArgumentOutOfRangeException(nameof(bucketCount));

            lock (m_Gate)
            {
                if (!IsCacheValid(startTime, endTime, bucketCount))
                    RebuildBuckets(startTime, endTime, bucketCount);

                output.Clear();
                for (int i = 0; i < m_CachedBuckets.Count; i++) output.Add(m_CachedBuckets[i]);
            }
        }

        private bool IsCacheValid(double startTime, double endTime, int bucketCount)
        {
            return m_CachedVersion == m_Version && m_CachedStart == startTime && m_CachedEnd == endTime && m_CachedBucketCount == bucketCount;
        }

        private void RebuildBuckets(double startTime, double endTime, int bucketCount)
        {
            m_CachedBuckets.Clear();
            int touchedCount = 0;
            double duration = endTime - startTime;

            for (int logicalIndex = 0; logicalIndex < m_Count; logicalIndex++)
            {
                XChartTimeSample sample = GetLogicalSample(logicalIndex);
                if (sample.Time < startTime || sample.Time > endTime) continue;

                int index = sample.Time == endTime
                    ? bucketCount - 1
                    : Math.Min(bucketCount - 1, (int)((sample.Time - startTime) / duration * bucketCount));
                ref XChartTimeBucket bucket = ref m_WorkBuckets[index];
                if (bucket.ValidCount == 0 && bucket.InvalidCount == 0)
                {
                    bucket.BucketIndex = index;
                    bucket.BucketCount = bucketCount;
                    bucket.StartTime = startTime + duration * index / bucketCount;
                    bucket.EndTime = startTime + duration * (index + 1) / bucketCount;
                    m_TouchedBucketIndices[touchedCount++] = index;
                }
                bucket.Add(sample);
            }

            Array.Sort(m_TouchedBucketIndices, 0, touchedCount);
            for (int i = 0; i < touchedCount; i++)
            {
                int index = m_TouchedBucketIndices[i];
                m_WorkBuckets[index].FinalizeStatistics();
                m_CachedBuckets.Add(m_WorkBuckets[index]);
                m_WorkBuckets[index] = default(XChartTimeBucket);
            }

            m_CachedStart = startTime;
            m_CachedEnd = endTime;
            m_CachedBucketCount = bucketCount;
            m_CachedVersion = m_Version;
        }

        private void AddUnchecked(in XChartTimeSample sample)
        {
            int writeIndex = (m_Head + m_Count) % Capacity;
            if (m_Count == Capacity)
            {
                m_Ring[m_Head] = sample;
                m_Head = (m_Head + 1) % Capacity;
            }
            else
            {
                m_Ring[writeIndex] = sample;
                m_Count++;
            }
        }

        private XChartTimeSample GetLogicalSample(int logicalIndex)
        {
            return m_Ring[(m_Head + logicalIndex) % Capacity];
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
