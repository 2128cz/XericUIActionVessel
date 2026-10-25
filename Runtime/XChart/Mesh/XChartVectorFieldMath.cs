using UnityEngine;

namespace XericUI.XChart.Mesh
{
    /// <summary>
    /// 矢量场（箭头）的<b>纯算术</b>：一条记录的两组三维怎么变成一支箭头，以及一组颜色维怎么变成颜色。
    /// 单独拎出来是因为这两条是"看图看不出来"的判据——
    /// 他 2026-10-25 现场："我把它添加进来了，之后它并没有显示出来"，以及"相对和绝对就是这两种区别"。
    /// 读数与算式放在一起，离线探针才能逐条钉住（规范 §25）。
    /// </summary>
    public static class XChartVectorFieldMath
    {
        /// <summary>
        /// 末端位置。<paramref name="mode"/>＝<see cref="XChartArrowVectorMode.Absolute"/> 时
        /// <paramref name="second"/> 是<b>方向矢量</b>本身（起点 + 方向×倍率）；
        /// ＝<see cref="XChartArrowVectorMode.Relative"/> 时它是<b>第二个点</b>（起点 + 差向量×倍率）。
        /// 两种读法在同一个数据上画出来是两幅不同的图：前者"刺猬"，后者"首尾相接"。
        /// </summary>
        public static Vector3 Tip(XChartArrowVectorMode mode, Vector3 first, Vector3 second, float scale)
        {
            Vector3 direction = mode == XChartArrowVectorMode.Relative ? second - first : second;
            return first + direction * scale;
        }

        /// <summary>
        /// 读一组颜色维：<paramref name="hue"/> 按 <b>0..256 的色环</b>（256 与 0 是同一个颜色，
        /// 取模，所以 300 也读得动），饱和度/明度/不透明度按 0..1 夹住。
        ///
        /// <para><b>任何一个不是有限数就拒绝</b>（返回 false，调用方退回单色）：
        /// NaN 经取模与 HSV 换算会得到一个看着像"黑色块"的东西——那不是数据的意思，
        /// 是我们在替它编一个颜色（同 §14.1 的"宁可这一帧不画"）。</para>
        /// </summary>
        public static bool TryColor(float hue, float saturation, float value, float alpha, out Color color)
        {
            color = Color.white;
            if (!IsFinite(hue) || !IsFinite(saturation) || !IsFinite(value) || !IsFinite(alpha)) return false;

            color = HsvToRgb(Mathf.Repeat(hue / 256f, 1f), Mathf.Clamp01(saturation), Mathf.Clamp01(value));
            color.a = Mathf.Clamp01(alpha);
            return true;
        }

        private static bool IsFinite(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>
        /// HSV → RGB（h∈[0,1)，s/v∈[0,1]）。
        /// <b>自己写而不是调 <c>Color.HSVToRGB</c></b>：后者是引擎侧的静态调用，
        /// 离线探针（mono + CoreModule）里跑不起来，而这条换算恰恰是要离线钉住的（规范 §25）。
        /// </summary>
        private static Color HsvToRgb(float h, float s, float v)
        {
            if (s <= 0f) return new Color(v, v, v, 1f);

            float sector = h * 6f;
            int index = Mathf.FloorToInt(sector) % 6;
            if (index < 0) index += 6;
            float f = sector - Mathf.Floor(sector);

            float p = v * (1f - s);
            float q = v * (1f - s * f);
            float t = v * (1f - s * (1f - f));

            switch (index)
            {
                case 0: return new Color(v, t, p, 1f);
                case 1: return new Color(q, v, p, 1f);
                case 2: return new Color(p, v, t, 1f);
                case 3: return new Color(p, q, v, 1f);
                case 4: return new Color(t, p, v, 1f);
                default: return new Color(v, p, q, 1f);
            }
        }
    }
}