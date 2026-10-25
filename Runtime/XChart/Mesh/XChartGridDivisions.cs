using System;

namespace XericUI.XChart.Mesh
{
    /// <summary>
    /// 网格分格数的唯一算法（规范 §8.2）。
    ///
    /// <para>立方体的线框要能和轴刻度逐线对上，分格数就不能只有一份——两条轴的范围不同，
    /// 需要的格数也不同（验收现场：<c>-100..100</c> 要 4 格、<c>-40..40</c> 要 8 格，
    /// 而网格当时只有一份"5"）。所以按方向给：S 一份、T 一份，<b>不给（0 或负）表示跟随统一值</b>。</para>
    ///
    /// <para><b>沿 S 的格线数由 T 方向的分格数决定</b>（"沿 S 走"的线就是"每个 T 分格边界一条"），
    /// 反过来也一样——这一句最容易写反，所以从绘制代码里抽出来，让离线探针钉住它。</para>
    /// </summary>
    public static class XChartGridDivisions
    {
        /// <summary>某个方向的分格数：给了正数就用它，否则回落到统一值（统一值也不正就取 1）</summary>
        public static int Resolve(int perDirection, int uniform)
        {
            if (perDirection >= 1) return perDirection;
            return uniform >= 1 ? uniform : 1;
        }

        /// <summary>沿 S 方向的格线数 = T 方向分格数 + 1（含两条边）</summary>
        public static int LinesAlongS(int divisionsS, int divisionsT, int uniform)
        {
            return Resolve(divisionsT, uniform) + 1;
        }

        /// <summary>沿 T 方向的格线数 = S 方向分格数 + 1（含两条边）</summary>
        public static int LinesAlongT(int divisionsS, int divisionsT, int uniform)
        {
            return Resolve(divisionsS, uniform) + 1;
        }

        /// <summary>统一值（不拆方向）时两个方向的格线数——留给"只有一份分格数"的旧配置用</summary>
        public static int UniformLineCount(int uniform)
        {
            return Resolve(0, uniform) + 1;
        }
    }
}