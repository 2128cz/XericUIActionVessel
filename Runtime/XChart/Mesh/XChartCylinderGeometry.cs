using System;
using UnityEngine;

namespace XericUI.XChart.Mesh
{
    /// <summary>
    /// 一片端面在 <b>2D</b> 上的像：中心 + 盘内一对基向量（已投影、<b>已乘半径</b>）。
    ///
    /// <para>正交投影是<b>仿射</b>的，所以 3D 圆盘的像正好是"把盘内基向量的像当半轴"的椭圆：
    /// <c>p(θ) = 中心 + U·cosθ + V·sinθ</c>。<b>只需投影中心与两个基各一次</b>，
    /// 不必对盘上每点做 3D 投影——这就是"投影从 2D 算起"（2026-10-23 他的要求）。</para>
    /// </summary>
    public struct XChartCylinderDisc2D
    {
        public Vector2 Center;
        /// <summary>盘内第一根半轴（已投影、已乘半径）</summary>
        public Vector2 U;
        /// <summary>盘内第二根半轴（已投影、已乘半径）</summary>
        public Vector2 V;

        public Vector2 PointAt(float angle)
        {
            return Center + U * Mathf.Cos(angle) + V * Mathf.Sin(angle);
        }

        /// <summary>第 <paramref name="index"/> 个采样点（角度均匀一圈）</summary>
        public Vector2 PointAtStep(int index, int count)
        {
            if (count <= 0) return Center;
            return PointAt(6.2831855f * index / count);
        }

        /// <summary>椭圆参数式的导数 <c>p'(θ) = -U·sinθ + V·cosθ</c>（贝塞尔控制点要用）</summary>
        public Vector2 TangentAt(float angle)
        {
            float c = Mathf.Cos(angle);
            float s = Mathf.Sin(angle);
            return new Vector2(-U.x * s + V.x * c, -U.y * s + V.y * c);
        }
    }

    /// <summary>
    /// 圆柱的纯几何：<b>不碰 Unity 对象</b>，所以离线 mono 探针能直接验"外轮廓是不是两片椭圆的凸包"
    /// "桥接点是否落在垂直于桥接方向的极值点上"这类断言。
    ///
    /// <para><b>为什么外轮廓＝凸包</b>：扫掠体的可见轮廓就是两个端面点集的<b>凸包</b>——
    /// 对凸截面这是精确解，而且平行、斜置、两端不同法线、两端不同心都是同一套代码，
    /// 不需要为"斜圆柱"再写一条切线公式。他要的那两条"过圆心的长轴方向连着外边框"的桥接线
    /// 就是凸包自动给出的那两段。</para>
    ///
    /// <para><b>母线只在显式要求时才画</b>：轮廓只画包边；"把两端顶点全连起来"那种透视线框
    /// 是可选元素（<c>GeneratrixCount</c>），不再是侧面填充的实现方式。</para>
    /// </summary>
    public static class XChartCylinderGeometry
    {
        /// <summary>
        /// 取与 <paramref name="normal"/> 垂直的一对单位向量。辅助轴按"离轴最远的那根世界轴"选，
        /// 这样叉积不会退化（法线与某世界轴平行时那个叉积接近 0）。
        /// </summary>
        public static bool TryOrthogonalBasis(Vector3 normal, out Vector3 u, out Vector3 v)
        {
            u = Vector3.zero;
            v = Vector3.zero;
            float length = normal.magnitude;
            if (length < 1e-6f) return false;

            Vector3 n = normal / length;
            Vector3 auxiliary = Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right;

            u = Vector3.Cross(n, auxiliary);
            float uLength = u.magnitude;
            if (uLength < 1e-6f) return false;
            u = u / uLength;
            v = Vector3.Cross(n, u);
            return true;
        }

        /// <summary>
        /// 端面盘 → 2D。只投影中心与两根基（基向量按 <paramref name="pixelsPerUnit"/> 缩放，
        /// 半径并进基向量里），所以调用方拿到的是"椭圆的半轴"而不是 3D 点集。
        /// </summary>
        public static bool TryProjectDisc(Vector2 centerPixels, Vector2 uPixels, Vector2 vPixels,
            float radius, out XChartCylinderDisc2D disc)
        {
            disc = default(XChartCylinderDisc2D);
            if (radius <= 0f) return false;

            disc.Center = centerPixels;
            disc.U = uPixels * radius;
            disc.V = vPixels * radius;
            return disc.U.sqrMagnitude > 1e-12f || disc.V.sqrMagnitude > 1e-12f;
        }

        /// <summary>
        /// 凸包（Andrew monotone chain），返回包上点数并写进 <paramref name="output"/>。
        /// <b><paramref name="points"/> 会被就地排序</b>（它同时充当 scratch，避免每帧分配）。
        /// <para><b>容量</b>：两条链各自最多 push 一次每个输入点 ⇒ 写入下标最大到 <c>2×count−3</c>，
        /// 所以 <paramref name="output"/> 要能装下 <b>2×count</b> 个点。这不是留白：写成"≥count"
        /// 在"输入点几乎全在包上"时会越界（我写的一支变异臂就是这么把它照出来的）。</para>
        /// </summary>
        public static int ConvexHull(Vector2[] points, int count, Vector2[] output)
        {
            if (points == null || output == null) throw new ArgumentNullException(nameof(points));
            if (output.Length < count * 2) throw new ArgumentException("output 至少要能装下 2×count 个点");
            if (count <= 0) return 0;
            if (count < 3)
            {
                int copy = Math.Min(count, output.Length);
                for (int i = 0; i < copy; i++) output[i] = points[i];
                return copy;
            }

            SortByXThenY(points, count);

            int total = 0;
            // 下凸链：从左到右；上凸链：从右到左。两链拼起来就是凸包（逆时针）
            for (int i = 0; i < count; i++)
            {
                while (total >= 2 && Cross(output[total - 2], output[total - 1], points[i]) <= 0f) total--;
                output[total++] = points[i];
            }

            int lower = total + 1;
            for (int i = count - 2; i >= 0; i--)
            {
                while (total >= lower && Cross(output[total - 2], output[total - 1], points[i]) <= 0f) total--;
                output[total++] = points[i];
            }

            return total - 1;   // 首点重复了一次
        }

        /// <summary>
        /// 外轮廓＝两片端面椭圆点集的凸包。<c>scratch</c> 装采样点（长度 ≥ 2×<paramref name="segmentCount"/>），
        /// <c>output</c> 按 <see cref="ConvexHull"/> 的容量口径要能装下 <b>2×采样数</b>（长度 ≥ 4×<paramref name="segmentCount"/>）。
        /// </summary>
        public static int BuildOutline(in XChartCylinderDisc2D first, in XChartCylinderDisc2D second,
            int segmentCount, Vector2[] scratch, Vector2[] output)
        {
            if (scratch == null || output == null) throw new ArgumentNullException(nameof(scratch));
            int samples = segmentCount * 2;
            if (scratch.Length < samples || output.Length < samples * 2)
                throw new ArgumentException("scratch 要装下两片椭圆的采样点，output 要装下 2×采样数");

            float step = 6.2831855f / segmentCount;
            for (int i = 0; i < segmentCount; i++)
            {
                float angle = i * step;
                scratch[i] = first.PointAt(angle);
                scratch[segmentCount + i] = second.PointAt(angle);
            }

            return ConvexHull(scratch, segmentCount * 2, output);
        }

        /// <summary>
        /// 两片端面的法线：显式给了就用显式；没给则<b>两端都用同一条轴、同向</b>。
        ///
        /// <para><b>为什么不是"各自朝另一端"</b>：`TryOrthogonalBasis(-n)` 会给出 <c>u' = -u</c>
        /// （叉积对第一个参数反号），于是两片的采样角是<b>镜像</b>的——同一个采样下标落在两片上的
        /// 方向不一致，可选元素里"两端对应点相连"的母线就会拧成 X 形，端面十字两端也是镜像的
        /// （他 2026-10-23 现场看出来的"两个端面是旋转了吗，它的连接顺序错了"）。
        /// 同向法线让两片参数化完全一致，采样下标 i 在两片上就是同一个 3D 方向。</para>
        /// </summary>
        public static void ResolveNormals(Vector3 explicitStart, Vector3 explicitEnd,
            Vector3 start, Vector3 end, out Vector3 startNormal, out Vector3 endNormal)
        {
            Vector3 axis = end - start;
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.forward;
            startNormal = explicitStart.sqrMagnitude > 1e-8f ? explicitStart : axis;
            endNormal = explicitEnd.sqrMagnitude > 1e-8f ? explicitEnd : axis;
        }

        /// <summary>多边形有向面积（逆时针为正）。探针用它比"凸包面积 ≥ 单个椭圆面积"。</summary>
        public static float SignedArea(Vector2[] polygon, int count)
        {
            if (polygon == null || count < 3) return 0f;
            float twice = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % count];
                twice += a.x * b.y - b.x * a.y;
            }
            return twice * 0.5f;
        }

        /// <summary>
        /// 椭圆上"带滚转信息"的四个点：+U 方向是 right、+V 是 top、-U 是 left、-V 是 bottom
        /// （<c>right - left = 2U</c>、<c>top - bottom = 2V</c>，这样十字的两臂方向就是盘自己的两根基）。
        /// 端面十字用它而不是用椭圆的长短轴：长短轴会随观察方向自己转、把滚转吃掉，
        /// 而这四个点就是<b>实际渲染用的那几个顶点</b>，所以十字永远与实际端面对得上。
        /// </summary>
        public static void CrossPoints(in XChartCylinderDisc2D disc, int segmentCount,
            out Vector2 left, out Vector2 top, out Vector2 right, out Vector2 bottom)
        {
            int quarter = Math.Max(1, segmentCount / 4);
            right = disc.PointAtStep(0, segmentCount);
            top = disc.PointAtStep(quarter, segmentCount);
            left = disc.PointAtStep(quarter * 2, segmentCount);
            bottom = disc.PointAtStep(quarter * 3, segmentCount);
        }

        private static float Cross(Vector2 origin, Vector2 a, Vector2 b)
        {
            return (a.x - origin.x) * (b.y - origin.y) - (a.y - origin.y) * (b.x - origin.x);
        }

        /// <summary>
        /// 反解椭圆参数：<c>p = C + U·cosθ + V·sinθ</c> ⇒ 解 2×2 线性方程得 (cosθ, sinθ)，
        /// 再按 <c>cos²+sin² ≈ 1</c> 判这点是否真的落在这片椭圆上。凸包分组（弧段 vs 桥接段）靠它。
        /// </summary>
        public static bool TryParameterOf(in XChartCylinderDisc2D disc, Vector2 point, out float angle)
        {
            angle = 0f;
            float det = disc.U.x * disc.V.y - disc.U.y * disc.V.x;
            if (Mathf.Abs(det) < 1e-9f) return false;   // 半轴退化（端面看到侧刃）⇒ 参数不唯一

            Vector2 d = point - disc.Center;
            float cos = (d.x * disc.V.y - d.y * disc.V.x) / det;
            float sin = (disc.U.x * d.y - disc.U.y * d.x) / det;
            if (Mathf.Abs(cos * cos + sin * sin - 1f) > 1e-3f) return false;
            angle = Mathf.Atan2(sin, cos);
            return true;
        }

        /// <summary>把一段扫过的角整理成"朝指定方向"的弧长：先折到 (-π, π]，再按方向补一圈。</summary>
        public static float SweepToDirection(float sweep, int direction)
        {
            const float twoPi = 6.2831855f;
            sweep = Mathf.Repeat(sweep + 3.1415927f, twoPi) - 3.1415927f;
            if (direction > 0 && sweep < 0f) sweep += twoPi;
            if (direction < 0 && sweep > 0f) sweep -= twoPi;
            return sweep;
        }

        /// <summary>按"最短路"定转向：折到 (-π, π] 后为正就 +1，否则 -1（弧段的转向靠它推断）。</summary>
        public static int ShortestDirection(float sweep)
        {
            float wrapped = Mathf.Repeat(sweep + 3.1415927f, 6.2831855f) - 3.1415927f;
            return wrapped > 0f ? 1 : -1;
        }

        /// <summary>
        /// 一段弧的<b>补弧</b>：起点接在它的终点、<b>同方向</b>走完剩下的那一圈（扫过 <c>2π-|sweep|</c>）。
        /// <para><b>不是"反过来走"</b>——这是它最容易写错的地方：反向走会原路覆盖同一段弧（半圆时尤其明显，
        /// 反着走一遍正好还是那半个圆），于是端面环又画回包边那条线上（他 2026-10-23 报的"补成外半环"）。
        /// 补弧与可见弧合起来才是整圈，所以两者<b>同号</b>、绝对值和 = 2π。</para>
        /// </summary>
        public static float ComplementSweep(float sweep)
        {
            const float twoPi = 6.2831855f;
            if (sweep > 0f) return twoPi - sweep;
            if (sweep < 0f) return -twoPi - sweep;
            return twoPi;
        }

        /// <summary>一段椭圆弧要拆成几节三次贝塞尔（每节 ≤ 90°，与圆的贝塞尔逼近同口径）</summary>
        public static int ArcPieceCount(float sweep)
        {
            float magnitude = Mathf.Abs(sweep);
            if (magnitude < 1e-6f) return 0;
            return Mathf.Max(1, Mathf.CeilToInt(magnitude / 1.5707964f));
        }

        /// <summary>
        /// 写出一节椭圆弧的三次贝塞尔控制点（4 个，顺序 P0/P1/P2/P3）。
        /// 系数 <c>k = 4/3·tan(Δ/4)</c> 是"圆弧 → 三次贝塞尔"的经典值。<b>椭圆是圆的仿射像</b>，
        /// 而贝塞尔控制点随仿射变换同样变换，所以同一个 k 对椭圆弧一样成立——
        /// 我只算控制点，<b>细分交给曲线渲染器</b>（他 2026-10-23 点名的
        /// <c>Generation/Curve</c> 那套，它的结构里就是贝塞尔细分）。
        /// </summary>
        public static void BuildArcPiece(in XChartCylinderDisc2D disc, float fromAngle, float sweep,
            int pieceCount, int index, Vector2[] output)
        {
            if (output == null || output.Length < 4) throw new ArgumentException("控制点数组至少要 4 个点");
            if (pieceCount <= 0) pieceCount = 1;
            float delta = sweep / pieceCount;
            float a = fromAngle + delta * index;
            float b = a + delta;
            float k = 1.3333333f * Mathf.Tan(delta * 0.25f);

            Vector2 pa = disc.PointAt(a);
            Vector2 pb = disc.PointAt(b);
            output[0] = pa;
            output[1] = pa + disc.TangentAt(a) * k;
            output[2] = pb - disc.TangentAt(b) * k;
            output[3] = pb;
        }

        /// <summary>直线段 → 一次三次贝塞尔（控制点在 1/3、2/3，形状就是这条直线）</summary>
        public static void BuildLineBezier(Vector2 a, Vector2 b, Vector2[] output)
        {
            if (output == null || output.Length < 4) throw new ArgumentException("控制点数组至少要 4 个点");
            Vector2 third = (b - a) / 3f;
            output[0] = a;
            output[1] = a + third;
            output[2] = a + third * 2f;
            output[3] = b;
        }

        /// <summary>
        /// 给轮廓每个点打"落在哪片端面上"的标记（1=第一片 / 2=第二片 / 0=都认不出）。
        /// 凸包按端面分组，才能把弧段当曲线、把跨端面那两段当直线（"过圆心的长轴方向连外框"那两条桥接线）。
        /// </summary>
        public static void ClassifyOutline(Vector2[] outline, int count, in XChartCylinderDisc2D first,
            in XChartCylinderDisc2D second, byte[] tags)
        {
            if (outline == null || tags == null) throw new ArgumentNullException(nameof(outline));
            if (tags.Length < count) throw new ArgumentException("tags 长度不足");
            for (int i = 0; i < count; i++)
            {
                tags[i] = TryParameterOf(first, outline[i], out float _) ? (byte)1
                    : TryParameterOf(second, outline[i], out float _) ? (byte)2
                    : (byte)0;
            }
        }

        /// <summary>
        /// 把凸包切成"同一片端面"的连续段（每段是那片的可见弧）。从"标记变化处"起步走一圈，
        /// 跨末尾的那一段才不会被劈成两半。各段写进 <paramref name="runStarts"/> / <paramref name="runEnds"/> /
        /// <paramref name="runTags"/>，返回段数（数组要能装下 count 段）。
        /// <para>凸包上每片凸集只贡献一段弧，所以正常就是"2 弧 + 2 桥接"；<b>远侧端面可能一段都没有</b>
        /// （它整个躲在外轮廓里），这时它没有"可见弧"可补——环只能自己画整圈。</para>
        /// </summary>
        public static int SplitOutlineRuns(Vector2[] outline, int count, in XChartCylinderDisc2D first,
            in XChartCylinderDisc2D second, byte[] tags, int[] runStarts, int[] runEnds, byte[] runTags)
        {
            if (outline == null || tags == null || runStarts == null || runEnds == null || runTags == null)
                throw new ArgumentNullException(nameof(outline));
            if (tags.Length < count || runStarts.Length < count || runEnds.Length < count || runTags.Length < count)
                throw new ArgumentException("缓冲长度不足");
            if (count < 3) return 0;

            ClassifyOutline(outline, count, first, second, tags);

            int start = 0;
            for (int i = 0; i < count; i++)
            {
                if (tags[i] != tags[(i + count - 1) % count]) { start = i; break; }
            }

            int runs = 0;
            int runStart = start;
            int runTag = tags[start];
            for (int step = 1; step <= count; step++)
            {
                int index = (start + step) % count;
                if (step < count && tags[index] == runTag) continue;

                runTags[runs] = (byte)runTag;
                runStarts[runs] = runStart;
                runEnds[runs] = (start + step - 1) % count;
                runs++;

                runStart = index;
                if (step < count) runTag = tags[index];
            }
            return runs;
        }

        /// <summary>
        /// 端面环＝包边那段可见弧的<b>补弧</b>：起点接在可见弧的终点、扫过 <c>2π-|可见|</c>（方向相反），
        /// 也就是端面圆上<b>躲在外轮廓里面</b>的那一半。外侧由包边画、内侧由环画，两边都不重复。
        /// </summary>
        public static void RingArc(float fromAngle, float visibleSweep, out float ringStartAngle, out float ringSweep)
        {
            ringStartAngle = fromAngle + visibleSweep;
            ringSweep = ComplementSweep(visibleSweep);
        }

        /// <summary>按 x 再按 y 排序（就地）。凸包要它，且顺序稳定才能让"包点顺序"可复现。</summary>
        private static void SortByXThenY(Vector2[] points, int count)
        {
            for (int i = 1; i < count; i++)
            {
                Vector2 key = points[i];
                int j = i - 1;
                while (j >= 0 && (points[j].x > key.x || (points[j].x == key.x && points[j].y > key.y)))
                {
                    points[j + 1] = points[j];
                    j--;
                }
                points[j + 1] = key;
            }
        }
    }
}