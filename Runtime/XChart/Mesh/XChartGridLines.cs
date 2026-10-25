using System.Collections.Generic;
using UnityEngine;
using XericUI.XChart.Axis;

namespace XericUI.XChart.Mesh
{
    /// <summary>
    /// 网格线的位置从哪儿来（P9）：<b>网格是定义方</b>——按绑定的那一维的数据范围与目标条数
    /// 算出一组"好看"的刻度值，再折成面参数比例发布出去；轴线以后按这些发布的线吸附，
    /// 不再两边各算一遍（他 2026-10-23 的原话："立方体网格上的分割线也都统一通过 chart 上的
    /// 数据范围控制"）。
    ///
    /// <para><b>不绑数据（维度 &lt; 0）时退回等分</b>：比例 = <c>i/(count-1)</c>，与旧行为逐位相同——
    /// 所以"没配维度"这件事不会改变任何画面。</para>
    ///
    /// <para>刻度值那一套（nice step + 升序铺 + 指数/对数换算）直接复用
    /// <see cref="XChartAxisMath"/>：**网格与刻度同源**，"逐条坐标相等"才有基础。</para>
    /// </summary>
    public static class XChartGridLines
    {
        /// <summary>等分（不绑数据）：<paramref name="count"/> 条线，含两条边，比例 i/(count-1)。</summary>
        public static int BuildUniformRatios(int count, float[] ratios)
        {
            if (ratios == null || count <= 0) return 0;
            count = Mathf.Min(count, ratios.Length);
            for (int i = 0; i < count; i++)
                ratios[i] = count > 1 ? (float)i / (count - 1) : 0f;
            return count;
        }

        /// <summary>
        /// 按数据范围算线位置：用 <see cref="XChartAxisMath.BuildTicks"/> 求出刻度值，
        /// 每个值经 <see cref="XChartAxisMath.ToParameter"/> 折成面参数比例并<b>夹到 [0,1]</b>
        /// （范围外的刻度值不该画到面外去）。
        /// </summary>
        public static int BuildRatios(double min, double max, XChartAxisScale scale, double strength,
            int targetCount, double manualStep, bool autoStep, float[] ratios, List<double> scratch)
        {
            if (ratios == null || scratch == null) return 0;

            XChartAxisMath.BuildTicks(min, max, scale, manualStep, targetCount, autoStep, scratch);

            int count = 0;
            for (int i = 0; i < scratch.Count && count < ratios.Length; i++)
            {
                double ratio = XChartAxisMath.ToParameter(scratch[i], min, max, scale, strength);
                if (double.IsNaN(ratio) || double.IsInfinity(ratio)) continue;
                ratios[count++] = Mathf.Clamp01((float)ratio);
            }
            return count;
        }
    /// <summary>
        /// 把 <paramref name="ratio"/> 吸附到<b>最近的一条格线比例</b>上（P9 的"轴线跟随"那一步）。
        /// 最近那条的距离超过 <paramref name="maxDistance"/> 就<b>不吸</b>——宁可不对齐，也不要把刻度
        /// 扭到老远的地方（那会让人以为数据变了）。吸不上时 <paramref name="snapped"/> 原样回填，返回 false。
        /// </summary>
        public static bool TrySnapToGrid(float[] ratios, int count, float ratio, float maxDistance, out float snapped)
        {
            snapped = ratio;
            if (ratios == null || count <= 0) return false;

            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float distance = Mathf.Abs(ratios[i] - ratio);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            if (best < 0 || bestDistance > maxDistance) return false;
            snapped = ratios[best];
            return true;
        }
    }
}