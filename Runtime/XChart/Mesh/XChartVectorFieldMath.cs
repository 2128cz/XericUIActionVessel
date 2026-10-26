using UnityEngine;
using XericUI.XChart;

namespace XericUI.XChart.Mesh
{
    /// <summary>
    /// 曲线渲染器的箭头标记长什么样（离线侧的形状，接线时一对一映射到库的 <c>ArrowShape</c>）。
    /// 只列我们真用的两档：三角形＝有方向的箭头，圆形＝零向量那种"就是一个点"。
    /// </summary>
    public enum XChartMarkerShape
    {
        Triangle,
        Circle,
    }

    /// <summary>
    /// 一支箭头的<b>头</b>：位置（在曲线上的参数 0..1）、尺寸（沿切线／垂直切线）、颜色。
    ///
    /// <para>这是<b>我们自己的</b>纯算术写法，不是库的结构体——离线探针引不到库那个 assembly，
    /// 所以判据打在这个结构上，工具里只做"对号入座"的映射（§29）。尺寸在 <see cref="XChartVectorFieldMath.Head"/>
    /// 里夹住而不是原样传："头长 0"进库里就是画不出来的东西，而那里的调用方是面板上的数字框。</para>
    /// </summary>
    public struct XChartArrowMarker
    {
        public XChartMarkerShape Shape;
        /// <summary>在曲线上的位置（0＝起点、1＝末端），已夹进 [0,1]</summary>
        public float Progress;
        /// <summary>沿切线方向的尺寸（库里叫 width）</summary>
        public float AlongTangent;
        /// <summary>垂直切线方向的尺寸（库里叫 height）</summary>
        public float AcrossTangent;
        /// <summary>+1＝头尖对准那个点；0＝中心对准（圆点用这个）</summary>
        public float TipAtPoint;
        public Color32 Color;
    }

    /// <summary>
    /// 矢量场（箭头）的<b>纯算术</b>：一条记录的两组三维怎么变成一支箭头，以及一组颜色维怎么变成颜色。
    /// 单独拎出来是因为这两条是"看图看不出来"的判据——
    /// 他 2026-10-25 现场："我把它添加进来了，之后它并没有显示出来"，以及"相对和绝对就是这两种区别"。
    /// 读数与算式放在一起，离线探针才能逐条钉住（规范 §25）。
    /// </summary>
    public static class XChartVectorFieldMath
    {
        /// <summary>
        /// 一条记录的原始三值 → <b>立方体单位</b>：每一维按<b>它自己的显示范围</b>映到 [-1,1]
        /// （±1 就是盒子边界），与散点、圆柱走的是同一套（<see cref="XChartBindingMath.TryNormalize"/>）。
        ///
        /// <para><b>为什么单独有这么一个函数</b>：箭头以前把数据值<b>直接当成立方体单位</b>用了，
        /// 于是一份 x∈[-100,50] 的表把所有箭头投到一万多像素之外——支数在算、面在提交、画面什么都没有
        /// （他 2026-10-26："矢量场现在没有在渲染"）。散点和圆柱一直归一化、只有箭头没归，
        /// 这就是"同一件事两套口径"的又一种形状：它不报错，只是看不见。</para>
        ///
        /// <para><b>任何一维给不出有效范围（跨度 ≤0、或值/端点非有限）就整体拒答</b>——
        /// 半归一化的坐标比不归一化的更难查：它会画出"看着像个箭头但方向不对"的东西。</para>
        /// </summary>
        public static bool TryCubePoint(double x, double y, double z,
            double minX, double maxX, double minY, double maxY, double minZ, double maxZ,
            out Vector3 cube)
        {
            cube = Vector3.zero;

            float nx, ny, nz;
            if (!XChartBindingMath.TryNormalize(x, minX, maxX, out nx)
                || !XChartBindingMath.TryNormalize(y, minY, maxY, out ny)
                || !XChartBindingMath.TryNormalize(z, minZ, maxZ, out nz)) return false;

            cube = new Vector3(nx, ny, nz);
            return true;
        }

        /// <summary>
        /// 三角形头。<paramref name="progress"/> 夹进 [0,1]；<b>不是有限数就落在末端</b>（1）——
        /// NaN 一路传进库里会得到一个位置未知、也报不出错的东西，那正是"画面和读数对不上"的来路。
        /// 两个尺寸都兜住最小值：0 长的头等于没画，而"没画"该由开关说话，不该由算术偷偷决定。
        /// </summary>
        public static XChartArrowMarker Head(float length, float width, float progress, Color color)
        {
            return new XChartArrowMarker
            {
                Shape = XChartMarkerShape.Triangle,
                Progress = IsFinite(progress) ? Mathf.Clamp01(progress) : 1f,
                AlongTangent = Mathf.Max(0.1f, length),
                AcrossTangent = Mathf.Max(0.1f, width),
                TipAtPoint = 1f,
                Color = color,
            };
        }

        /// <summary>零向量的圆点：位置在条目中间（0.5）、中心对准那个点。</summary>
        public static XChartArrowMarker Dot(float diameter, float width, Color color)
        {
            return new XChartArrowMarker
            {
                Shape = XChartMarkerShape.Circle,
                Progress = 0.5f,
                AlongTangent = Mathf.Max(0.1f, diameter),
                AcrossTangent = Mathf.Max(0.1f, width),
                TipAtPoint = 0f,
                Color = color,
            };
        }

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
        /// 一支箭头＝相邻两条记录 ⇒ 给定记录数与上限，能画出几支（上限 ≤0 ＝不限）。
        ///
        /// <para>单列出来是因为<b>"记录不够"与"维数不够"在画面上完全同形</b>（都是一条都不画），
        /// 而 N−1 与 0 这两个数是唯一分得清它们的读数（规范 §25.1）。</para>
        /// </summary>
        public static int ArrowCount(int recordCount, int maxArrows)
        {
            if (recordCount < 2) return 0;
            int pairs = recordCount - 1;
            if (maxArrows > 0 && maxArrows < pairs) return maxArrows;
            return pairs;
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