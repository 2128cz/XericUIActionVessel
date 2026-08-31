using System;
using System.Collections.Generic;

namespace XericUI.XChart
{
    public struct XChartAggregateBucket
    {
        public int StartIndex;
        public int SampleCount;
        public float First;
        public float Last;
        public float Minimum;
        public float Maximum;
        public float Mean;
    }

    public sealed class XChartMultiResolutionSeries
    {
        private readonly float[] m_Values;
        private readonly List<XChartAggregateBucket[]> m_Levels = new List<XChartAggregateBucket[]>();
        private int m_Head;
        private int m_Count;
        private long m_Version;
        private long m_BuiltVersion = -1;

        public XChartMultiResolutionSeries(int capacity)
        {
            if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity));
            m_Values = new float[capacity];
        }

        public int Capacity => m_Values.Length;
        public int Count => m_Count;
        public long Version => m_Version;

        public void Add(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            int index = (m_Head + m_Count) % Capacity;
            if (m_Count == Capacity)
            {
                m_Values[m_Head] = value;
                m_Head = (m_Head + 1) % Capacity;
            }
            else
            {
                m_Values[index] = value;
                m_Count++;
            }
            m_Version++;
        }

        public void GetBuckets(int maximumBucketCount, List<XChartAggregateBucket> output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (maximumBucketCount < 1) throw new ArgumentOutOfRangeException(nameof(maximumBucketCount));
            RebuildLevels();
            output.Clear();
            if (m_Count == 0) return;
            int level = 0;
            while (level + 1 < m_Levels.Count && m_Levels[level + 1].Length >= maximumBucketCount) level++;
            var buckets = m_Levels[level];
            for (int i = 0; i < buckets.Length; i++) output.Add(buckets[i]);
        }

        private void RebuildLevels()
        {
            if (m_BuiltVersion == m_Version) return;
            m_Levels.Clear();
            var level = new XChartAggregateBucket[m_Count];
            for (int i = 0; i < m_Count; i++)
            {
                float value = m_Values[(m_Head + i) % Capacity];
                level[i] = new XChartAggregateBucket { StartIndex = i, SampleCount = 1, First = value, Last = value, Minimum = value, Maximum = value, Mean = value };
            }
            m_Levels.Add(level);
            while (level.Length > 1)
            {
                var next = new XChartAggregateBucket[(level.Length + 1) / 2];
                for (int i = 0; i < next.Length; i++)
                {
                    var left = level[i * 2];
                    if (i * 2 + 1 >= level.Length) { next[i] = left; continue; }
                    var right = level[i * 2 + 1];
                    int count = left.SampleCount + right.SampleCount;
                    next[i] = new XChartAggregateBucket
                    {
                        StartIndex = left.StartIndex,
                        SampleCount = count,
                        First = left.First,
                        Last = right.Last,
                        Minimum = Math.Min(left.Minimum, right.Minimum),
                        Maximum = Math.Max(left.Maximum, right.Maximum),
                        Mean = (left.Mean * left.SampleCount + right.Mean * right.SampleCount) / count,
                    };
                }
                m_Levels.Add(next);
                level = next;
            }
            m_BuiltVersion = m_Version;
        }
    }
}
