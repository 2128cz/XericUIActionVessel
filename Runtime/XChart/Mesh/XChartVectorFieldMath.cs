using UnityEngine;
using XericUI.XChart;

namespace XericUI.XChart.Mesh
{
    /// <summary>
    /// 一支箭头的<b>方向从哪来</b>（他 2026-10-26 补的口径：光有"相对/绝对"两个词表达不出
    /// "一条记录自带方向"和"偶数点是点、奇数点是方向"这两种存法）。
    ///
    /// <para><b>编号是有意接旧的</b>：0/1 就是原来 <c>XChartArrowVectorMode</c> 的 Absolute/Relative，
    /// 所以场景里存过的 <c>m_VectorMode</c> 值不用动就还是同一个意思（枚举换类型而序号不改＝静默丢值的反面）。
    /// 2/3 是新增的两档"方向是矢量"的存法。</para>
    /// </summary>
    public enum XChartArrowDirectionSource
    {
        /// <summary>相邻两条、重叠：本条＝起点，下一条的<b>坐标</b>就是方向矢量（旧 Absolute）。箭数＝N−1</summary>
        NextRecordAsVector = 0,
        /// <summary>相邻两条、重叠：本条＝起点，下一条＝<b>第二个点</b>（旧 Relative，首尾相接）。箭数＝N−1</summary>
        NextRecordAsPoint = 1,
        /// <summary>一条记录自带：0~2 维是点、3~5 维是方向矢量。箭数＝N（要 6 个数值维）</summary>
        ThisRecordCarriesVector = 2,
        /// <summary>分段：偶数记录是点、奇数记录是方向（不重叠）。箭数＝N／2</summary>
        AlternatingRecords = 3,
    }

    /// <summary>
    /// 曲线渲染器的箭头标记长什么样（接线时一对一映射到库的 <c>ArrowShape</c>）。
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
        // "数据值 → 立方体单位（以及超出显示范围怎么办）"<b>不在这里</b>：散点、矢量、圆柱都要同一套折法，
        // 收在 XChartCubeFoldMath（§32）。两处各写一遍就出现过"散点把点夹在盒壁上、矢量却画到盒外"——
        // 同一张表两幅画，而且两边都"看着挺对"。

        /// <summary>
        /// 三角形头。<paramref name="progress"/> 与 <paramref name="anchorOnPoint"/> 都夹进 [0,1]；
        /// <b>不是有限数就退回默认</b>（位置＝末端、锚＝头尖对点）——
        /// NaN 一路传进库里会得到一个位置未知、也报不出错的东西，那正是"画面和读数对不上"的来路。
        /// 两个尺寸都兜住最小值：0 长的头等于没画，而"没画"该由开关说话，不该由算术偷偷决定。
        /// </summary>
        public static XChartArrowMarker Head(float length, float width, float progress, float anchorOnPoint,
            Color color)
        {
            return new XChartArrowMarker
            {
                Shape = XChartMarkerShape.Triangle,
                Progress = IsFinite(progress) ? Mathf.Clamp01(progress) : 1f,
                AlongTangent = Mathf.Max(0.1f, length),
                AcrossTangent = Mathf.Max(0.1f, width),
                TipAtPoint = AnchorToCompensation(anchorOnPoint),
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
        /// 末端位置。<paramref name="source"/>＝<see cref="XChartArrowDirectionSource.NextRecordAsPoint"/> 时
        /// <paramref name="second"/> 是<b>第二个点</b>（起点 + 差向量×倍率，首尾相接）；
        /// 其余三档它就是<b>方向矢量</b>本身（起点 + 方向×倍率）。
        /// 相邻两条的两档在同一个数据上画出来是两幅图：一片"刺猬"对一串首尾相接。
        /// </summary>
        public static Vector3 Tip(XChartArrowDirectionSource source, Vector3 first, Vector3 second, float scale)
        {
            // 只有"下一条记录是第二个点"这一档要作差；其余档里 second 本身就是方向矢量。
            // 点到点这一档的两端都是投影点，所以它的 2D 长度天然是投影结果，不需要再多算一次（他 2026-10-26）
            Vector3 direction = source == XChartArrowDirectionSource.NextRecordAsPoint ? second - first : second;
            return first + direction * scale;
        }

        /// <summary>
        /// 这一档方向来源<b>每个箭头自己要吃几个数值维</b>：自带方向那档要 6 个（0~2 点、3~5 方向），
        /// 其余三档只要 3 个（方向从别的记录来）。
        ///
        /// <para>面板拿它和表里现有的维数对印（§25.1）：维数不够时元素一支都不画，
        /// 报出来必须是"需要几个、现有几个"两个数，不能只说"没显示"。</para>
        /// </summary>
        public static int RequiredCoordinateDimensions(XChartArrowDirectionSource source)
            => source == XChartArrowDirectionSource.ThisRecordCarriesVector
                ? XChartArrowConfig.CoordinateDimensionCount * 2
                : XChartArrowConfig.CoordinateDimensionCount;

        /// <summary>
        /// 给定记录数与上限，这一档来源能画几支。四种算法各不同，所以都得在这里，
        /// 由调用方现问而不是在工具里各写一遍（"N−1／N／N÷2"三个数写错一个，画面上只是少几支，最难查）。
        /// </summary>
        public static int ArrowCount(XChartArrowDirectionSource source, int recordCount, int maxArrows)
        {
            // 没有"记录数 ≤0 就先返回 0"那道闸：四档算法各自都把 0/1 条算成 0（0−1、0/2、<2 那一支），
            // 加一道到不了的守卫只会多一个"看起来判过、其实永远不响"的地方（§29.4 那条教训）
            int pairs;
            switch (source)
            {
                case XChartArrowDirectionSource.ThisRecordCarriesVector: pairs = recordCount; break;
                case XChartArrowDirectionSource.AlternatingRecords: pairs = recordCount / 2; break;
                default: pairs = recordCount < 2 ? 0 : recordCount - 1; break;   // 相邻两条那两档
            }
            if (maxArrows > 0 && maxArrows < pairs) return maxArrows;
            return pairs;
        }

        /// <summary>
        /// 锚点 → 库的 <c>depthCompensation</c>：<b>0＝尾部落在数据点上、0.5＝中心、1＝头尖落在点上</b>
        /// （映射就是 anchor×2−1）。默认 1：矢量场的箭头指的就是那一点。
        /// 非有限数按 1 处理——"头该贴在哪"这件事不该因为一次写坏的值变成位置未知。
        /// </summary>
        public static float AnchorToCompensation(float anchorOnPoint)
            => IsFinite(anchorOnPoint) ? Mathf.Clamp01(anchorOnPoint) * 2f - 1f : 1f;

        /// <summary>
        /// <b>这条矢量的投影比例</b>：投影后画了多少像素 ÷ 它在立方体单位里该有多少像素
        /// （<paramref name="projectedPixels"/>＝|to−from|，<paramref name="cubeLength"/>＝|tip−origin|（立方体单位），
        /// <paramref name="pixelsPerCubeUnit"/>＝<c>space.CubeToPixel</c>）。
        /// 平行于屏幕＝1，指向屏幕里→0。库的标记吃的是<b>固定像素</b>（库里那行
        /// <c>pos += tangent * arrow.width * 0.5f * depthCompensation</c>），所以头的尺寸必须乘上这个比例，
        /// 否则所有姿态下头一样长——他 2026-10-26 看到的就是这个："圆周方向的长度在 2D 上的投影不可能完全一样长"。
        /// 除不出来（长度为 0 或不是有限数）就答 0：宁可头缩成没有，也不要一个凭空的固定尺寸。
        /// </summary>
        public static float ProjectedScale(float projectedPixels, float cubeLength, float pixelsPerCubeUnit)
        {
            if (!IsFinite(projectedPixels) || !IsFinite(cubeLength) || !IsFinite(pixelsPerCubeUnit)) return 0f;
            float reference = cubeLength * pixelsPerCubeUnit;
            if (reference <= 1e-6f) return 0f;
            float scale = projectedPixels / reference;
            return scale > 0f ? scale : 0f;
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