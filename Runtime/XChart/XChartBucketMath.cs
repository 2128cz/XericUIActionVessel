using System;

namespace XericUI.XChart
{
    /// <summary>
    /// 桶采样：把<b>一段记录</b>简化成 N 个点，每点取桶内一个"最有意义"的统计量。
    ///
    /// <para><b>为什么要有它</b>：元素按记录数长几何（散点一个点一条记录、箭头一条记录两个四边形），
    /// 几万条记录就会把顶点预算和帧率一起吃掉。桶采样把"这一段时间里发生了什么"压成一个点——
    /// 折线要最新值就取 <c>Last</c>、要看包络就取 <c>Minimum</c>/<c>Maximum</c>、要看趋势就取 <c>Mean</c>。</para>
    ///
    /// <para><b>分桶的硬要求</b>：整数均分，<b>不留缝、不重叠、每条记录恰好属于一个桶</b>
    /// （用 <c>recordCount * i / bucketCount</c> 的整数算术，不是浮点即兴切）；
    /// 目标点数 ≥ 记录数或 ≤ 0 就是"不聚合"，一个桶都不多。</para>
    ///
    /// <para>纯算术 + 只读数据源 ⇒ 离线可判（见 <c>math_probe.cs</c> 的 bucket 断言与 p4 的对照臂）：
    /// "少一条记录""桶边界错一格"这类错误在画面上只是"点少了一个"，眼睛看不出来。</para>
    /// </summary>
    public static class XChartBucketMath
    {
        /// <summary>
        /// 要分几个桶。<paramref name="targetPoints"/> ≤ 0 或 ≥ 记录数＝不聚合（桶数 = 记录数），
        /// 记录数为 0 时返回 0。
        /// </summary>
        public static int BucketCount(int recordCount, int targetPoints)
        {
            if (recordCount <= 0) return 0;
            if (targetPoints <= 0 || targetPoints >= recordCount) return recordCount;
            return targetPoints;
        }

        /// <summary>
        /// 第 <paramref name="bucketIndex"/> 个桶覆盖 <c>[firstRecord, endExclusive)</c>。
        /// 整数均分：桶与桶严丝合缝（前一个的 end == 后一个的 first），全部桶并起来正好是 [0, recordCount)。
        /// </summary>
        public static bool TryBucketRange(int recordCount, int bucketIndex, int bucketCount,
            out int firstRecord, out int endExclusive)
        {
            firstRecord = endExclusive = 0;
            if (recordCount <= 0 || bucketCount <= 0 || bucketCount > recordCount) return false;
            if (bucketIndex < 0 || bucketIndex >= bucketCount) return false;

            firstRecord = (int)((long)recordCount * bucketIndex / bucketCount);
            endExclusive = (int)((long)recordCount * (bucketIndex + 1) / bucketCount);
            return endExclusive > firstRecord;
        }

        /// <summary>
        /// 桶内归约：取该维在 <c>[firstRecord, endExclusive)</c> 上的一个统计量。
        /// NaN/Inf 一律跳过（它们不是数据点）；整桶都是坏值返回 false——**宁可少画一个点，
        /// 也不要往几何里塞一个假位置**。<paramref name="scratch"/> 只给中位数用（要么给够长度、
        /// 要么别选中位数），长度不够就拒收而不是内部悄悄分配。
        /// </summary>
        public static bool TryReduce(IXChartDataSource data, int dimension, int firstRecord, int endExclusive,
            XChartAggregateKind reduce, double[] scratch, out double value)
        {
            value = double.NaN;
            if (data == null || dimension < 0) return false;
            if (firstRecord < 0 || endExclusive > data.RecordCount || endExclusive <= firstRecord) return false;
            if (reduce == XChartAggregateKind.Median && scratch == null) return false;

            double sum = 0d, squareSum = 0d, first = 0d, last = 0d, minimum = 0d, maximum = 0d;
            int valid = 0;

            for (int i = firstRecord; i < endExclusive; i++)
            {
                double v = data.GetValueAt(i, dimension);
                if (double.IsNaN(v) || double.IsInfinity(v)) continue;

                if (valid == 0) { first = minimum = maximum = v; }
                last = v;
                sum += v;
                squareSum += v * v;
                if (v < minimum) minimum = v;
                if (v > maximum) maximum = v;
                if (scratch != null && valid < scratch.Length) scratch[valid] = v;
                valid++;
            }

            if (valid == 0) return false;

            switch (reduce)
            {
                case XChartAggregateKind.Count: value = valid; return true;
                case XChartAggregateKind.First: value = first; return true;
                case XChartAggregateKind.Last: value = last; return true;
                case XChartAggregateKind.Minimum: value = minimum; return true;
                case XChartAggregateKind.Maximum: value = maximum; return true;
                case XChartAggregateKind.Sum: value = sum; return true;
                case XChartAggregateKind.Mean: value = sum / valid; return true;
                case XChartAggregateKind.Rms: value = Math.Sqrt(squareSum / valid); return true;
                case XChartAggregateKind.Median:
                    if (scratch.Length < valid) return false;
                    Array.Sort(scratch, 0, valid);
                    value = (valid & 1) == 1
                        ? scratch[valid / 2]
                        : (scratch[valid / 2 - 1] + scratch[valid / 2]) * 0.5;
                    return true;
                default:
                    return false;
            }
        }
    }
}