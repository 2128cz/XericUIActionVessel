using UnityEngine;

namespace XericUI.XChart
{
    /// <summary>
    /// 数据落在<b>显示范围之外</b>时怎么办（每个数据图元自己选，规范 §32）。
    ///
    /// <para>显示范围的本分是"定标尺"：它决定网格多大、轴刻度到哪儿。它<b>不该</b>顺手决定数据画不画——
    /// 但历史上两件事不一致过：散点把值夹进盒子（超范围的点贴在盒壁上，看着像"数据到边就停"），
    /// 而矢量/圆柱直接把值按比例投出去（可以跑到盒子外面）。同一张表两套口径，
    /// 症状就是"散点看着钳制对了、别的组件钳制没做"（他 2026-10-26）。</para>
    /// </summary>
    public enum XChartValueOutOfRange
    {
        /// <summary>默认：不钳、不藏，按数据自己的值投（可以画到盒子外面）——"呈现自己准确的位置"。</summary>
        RenderAsIs = 0,
        /// <summary>夹到范围端点上（超出部分画在盒子边上）。</summary>
        ClampToRange = 1,
        /// <summary>整条记录隐藏（任何一轴越界就不画这一条）。</summary>
        HideOutOfRange = 2,
    }

    /// <summary>
    /// "值 → 盒子内比例"的<b>唯一</b>折法：三个数据图元（散点、矢量箭头、圆柱）都从这里取，
    /// 于是"超范围怎么处理"只有一个答案，而不是每个元素各写一遍 Clamp/不 Clamp。
    /// </summary>
    public static class XChartCubeFoldMath
    {
        /// <summary>
        /// 单轴折法：<c>0＝该轴的下端点、1＝上端点</c>，范围外按 <paramref name="behaviour"/> 办。
        ///
        /// <para><b>返回 false 只有两种情况</b>：值或端点不是有限数（那是坏数据，宁可不画这条，也不要拿别的值顶替）、
        /// 或者选了 <see cref="XChartValueOutOfRange.HideOutOfRange"/> 而这一轴越界。
        /// <b>一整列同一个数（跨度 ≤0）不算坏</b>：答居中（0.5）——为这个把整张图弄没，
        /// 是把"我还没钉范围"误报成"这里没有数据"（与 §25.1 的"需求 vs 现有"同一条精神）。</para>
        /// </summary>
        public static bool TryRatio01(double value, double minimum, double maximum,
            XChartValueOutOfRange behaviour, out float ratio01)
        {
            ratio01 = 0.5f;
            if (!IsFinite(value) || !IsFinite(minimum) || !IsFinite(maximum)) return false;

            double span = maximum - minimum;
            if (span <= 0d) return true;

            double r = (value - minimum) / span;
            if (r < 0d || r > 1d)
            {
                if (behaviour == XChartValueOutOfRange.HideOutOfRange) return false;
                if (behaviour == XChartValueOutOfRange.ClampToRange) r = r < 0d ? 0d : 1d;
            }
            ratio01 = (float)r;
            return true;
        }

        /// <summary>
        /// 三轴一起折到 <b>立方体单位</b>（[-1,1]，±1 就是盒子边界）。
        /// <b>全有或全无</b>：任何一轴要隐藏，这一条整体不画——只藏一半坐标的点
        /// 会画出一个"在数据里不存在"的位置，那比少画一条糟得多。
        /// </summary>
        public static bool TryFoldCube(double x, double y, double z,
            double minX, double maxX, double minY, double maxY, double minZ, double maxZ,
            XChartValueOutOfRange behaviour, out Vector3 cube)
        {
            cube = Vector3.zero;
            float nx, ny, nz;
            if (!TryRatio01(x, minX, maxX, behaviour, out nx)
                || !TryRatio01(y, minY, maxY, behaviour, out ny)
                || !TryRatio01(z, minZ, maxZ, behaviour, out nz)) return false;

            cube = new Vector3(nx * 2f - 1f, ny * 2f - 1f, nz * 2f - 1f);
            return true;
        }

        private static bool IsFinite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
