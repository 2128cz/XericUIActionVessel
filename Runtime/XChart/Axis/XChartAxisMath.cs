using System;
using System.Collections.Generic;

namespace XericUI.XChart.Axis
{
    /// <summary>轴的空间类型：值 ↔ 轴上参数（0..1）的映射方式。</summary>
    public enum XChartAxisScale
    {
        /// <summary>线性：参数与值成正比</summary>
        Linear = 0,
        /// <summary>对数空间：等参数间距对应等<b>数量级</b>间距（要求范围两端同号且不为 0）</summary>
        Logarithmic = 1,
        /// <summary>指数空间：值按 <c>exp(k·参数)</c> 增长（<see cref="XChartAxisMath.Strength"/> 控制弯的程度）</summary>
        Exponential = 2,
    }

    /// <summary>
    /// 轴线的纯数学：值域 ↔ 参数、刻度取值。<b>不碰 Unity 类型</b>，
    /// 所以编译门里的 mono 探针能直接把这几条互逆关系和 nice-step 当断言跑。
    /// </summary>
    public static class XChartAxisMath
    {
        private const double k_Epsilon = 1e-9;

        /// <summary>指数空间强度太小时 <c>exp(k)-1</c> 会掉到 0，除法炸掉；这是下限。</summary>
        public const double MinStrength = 1e-3;

        // ── 值 ↔ 参数 ─────────────────────────────────────────────

        /// <summary>值 → 轴上参数 [0,1]。范围退化（max≈min）时一律给 0.5，不炸也不乱跳。</summary>
        public static double ToParameter(double value, double min, double max, XChartAxisScale scale, double strength)
        {
            double span = max - min;
            if (Math.Abs(span) <= k_Epsilon) return 0.5;

            switch (scale)
            {
                case XChartAxisScale.Logarithmic:
                {
                    // 只在两端同为正（或同为负）时对数空间才有意义；否则退回线性
                    if (min <= 0.0 || max <= 0.0) return (value - min) / span;
                    double lo = Math.Log(min), hi = Math.Log(max);
                    if (Math.Abs(hi - lo) <= k_Epsilon) return 0.5;
                    return (Math.Log(Math.Max(value, double.Epsilon)) - lo) / (hi - lo);
                }
                case XChartAxisScale.Exponential:
                {
                    double k = Math.Max(strength, MinStrength);
                    double denom = Math.Exp(k) - 1.0;
                    double ratio = (value - min) / span;                    // [0,1]
                    return Math.Log(1.0 + ratio * denom) / k;
                }
                default:
                    return (value - min) / span;
            }
        }

        /// <summary>轴上参数 [0,1] → 值。与 <see cref="ToParameter"/> 互为逆。</summary>
        public static double FromParameter(double parameter, double min, double max, XChartAxisScale scale, double strength)
        {
            double span = max - min;
            switch (scale)
            {
                case XChartAxisScale.Logarithmic:
                {
                    if (min <= 0.0 || max <= 0.0) return min + span * parameter;
                    double lo = Math.Log(min), hi = Math.Log(max);
                    return Math.Exp(lo + (hi - lo) * parameter);
                }
                case XChartAxisScale.Exponential:
                {
                    double k = Math.Max(strength, MinStrength);
                    return min + span * (Math.Exp(k * parameter) - 1.0) / (Math.Exp(k) - 1.0);
                }
                default:
                    return min + span * parameter;
            }
        }

        // ── 刻度 ──────────────────────────────────────────────────

        /// <summary>
        /// "好看"的步长：把目标条数折算成步长后，收拢到 <c>1 / 2 / 5 × 10^n</c>。
        /// 这样刻度值读起来是 0.5、1、2、5、10，而不是 1.37、2.74。
        /// </summary>
        public static double NiceStep(double span, int targetCount)
        {
            if (targetCount < 1) targetCount = 1;
            double magnitude = Math.Abs(span);
            if (magnitude <= k_Epsilon) return 1.0;

            double raw = magnitude / targetCount;
            double exponent = Math.Floor(Math.Log10(raw));
            double power = Math.Pow(10.0, exponent);
            double mantissa = raw / power;

            double nice;
            if (mantissa < 1.5) nice = 1.0;
            else if (mantissa < 3.0) nice = 2.0;
            else if (mantissa < 7.0) nice = 5.0;
            else nice = 10.0;
            return nice * power;
        }

        /// <summary>
        /// 生成主刻度<b>值</b>（升序）。线性/指数：从 <c>ceil(min/step)*step</c> 起按步长铺；
        /// 对数：按数量级铺 <c>1/2/5 × 10^n</c>（步长参数此时不用，因为等步长在数量级轴上没意义）。
        /// </summary>
        public static void BuildTicks(double min, double max, XChartAxisScale scale, double step,
            int targetCount, bool autoStep, List<double> output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            output.Clear();
            if (max < min) { double t = min; min = max; max = t; }

            if (scale == XChartAxisScale.Logarithmic && min > 0.0 && max > 0.0)
            {
                BuildDecadeTicks(min, max, output);
                return;
            }

            double delta = autoStep ? NiceStep(max - min, targetCount) : Math.Abs(step);
            if (delta <= k_Epsilon) return;

            // 用整数倍索引而不是累加，累加的浮点误差会在长轴上看得见
            double first = Math.Ceiling(min / delta) * delta;
            int index = 0;
            for (double value = first; value <= max + delta * 1e-6; value = first + (++index) * delta)
            {
                if (index > 100000) break;   // 步长给得离谱时的兜底，不至于挂死编辑器
                if (value < min - delta * 1e-6) continue;
                output.Add(SnapToZero(value, delta));
            }
        }

        /// <summary>对数轴：每个数量级上的 1/2/5</summary>
        private static void BuildDecadeTicks(double min, double max, List<double> output)
        {
            int start = (int)Math.Floor(Math.Log10(min));
            int end = (int)Math.Ceiling(Math.Log10(max));
            if (end - start > 12) end = start + 12;   // 12 个数级已经超出任何轴的表达范围

            double[] mantissas = { 1.0, 2.0, 5.0 };
            for (int k = start; k <= end; k++)
            {
                double power = Math.Pow(10.0, k);
                for (int m = 0; m < mantissas.Length; m++)
                {
                    double value = mantissas[m] * power;
                    if (value >= min * (1.0 - 1e-9) && value <= max * (1.0 + 1e-9)) output.Add(value);
                }
            }
        }

        /// <summary>把 -1e-17 这类"本该是 0"的刻度收正，否则标签会出现 <c>-0</c>。</summary>
        private static double SnapToZero(double value, double delta)
        {
            return Math.Abs(value) < delta * 1e-6 ? 0.0 : value;
        }

        /// <summary>
        /// 次刻度：在主刻度之间按<b>参数</b>等分。对数/指数空间里这样分出来的是几何级数，
        /// 视觉上才均匀（按值等分会在小值那头挤成一坨）。
        /// </summary>
        public static void BuildMinorTicks(double majorFrom, double majorTo, int minorCount,
            double min, double max, XChartAxisScale scale, double strength, List<double> output)
        {
            if (output == null || minorCount < 1) return;
            double p0 = ToParameter(majorFrom, min, max, scale, strength);
            double p1 = ToParameter(majorTo, min, max, scale, strength);
            for (int i = 1; i < minorCount; i++)
            {
                double u = p0 + (p1 - p0) * i / minorCount;
                output.Add(FromParameter(u, min, max, scale, strength));
            }
        }

        /// <summary>
        /// 数值 → 标签文本。格式化串为空时用"按量级自适应"的默认写法：
        /// 步长 ≥1 走整数，否则按步长的小数位数给（避免出现 0.30000001）。
        /// </summary>
        public static string FormatValue(double value, string format, double step)
        {
            if (!string.IsNullOrEmpty(format))
            {
                try { return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:" + format + "}", value); }
                catch (FormatException) { /* 格式化串写错就退回默认，别让一整条轴什么都标不出来 */ }
            }

            double magnitude = Math.Abs(step) > k_Epsilon ? Math.Abs(step) : Math.Abs(value);
            if (magnitude >= 1.0) return value.ToString("0", System.Globalization.CultureInfo.InvariantCulture);

            int decimals = 1;
            double probe = magnitude;
            while (probe < 1.0 && decimals < 6) { probe *= 10.0; decimals++; }
            string pattern = "0." + new string('#', decimals);
            return value.ToString(pattern, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
