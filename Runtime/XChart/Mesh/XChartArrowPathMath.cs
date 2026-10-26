using UnityEngine;

namespace XericUI.XChart.Mesh
{
    /// <summary>
    /// 矢量场"连续绘制"那条路径的<b>纯算术</b>：一串点 → 一条平滑折线（三次贝塞尔链）、
    /// 在链上取点/取切线、把"路径上的位置"换成"第几节的哪个参数"，以及把两点写成一条直线贝塞尔。
    ///
    /// <para><b>为什么要单独一个文件</b>：这几条是"画出来对不对"唯一能离线判的部分——
    /// 曲线渲染器那一层（库里的 <c>UICurveRenderer</c>）吃的是<b>控制点</b>，
    /// 控制点算错了画面只会"看着不太对"，不会报错。箭头放在路径的哪一处、切线朝哪，
    /// 全部由这里的算式决定，所以每条都要有判据（规范 §25、§26）。</para>
    ///
    /// <para><b>平滑用的是 Catmull-Rom 转贝塞尔</b>：给一串途经点，每段 (p1→p2) 的两个内控制点
    /// 由相邻点 p0/p3 推出（张力 1/6 那档，标准写法）。这样曲线<b>真的经过每个数据点</b>——
    /// 矢量场的点是数据本身，用"不经过控制点"的自由贝塞尔就等于把数据挪了位置。</para>
    /// </summary>
    public static class XChartArrowPathMath
    {
        /// <summary>一段贝塞尔的控制点个数</summary>
        public const int ControlPointsPerSegment = 4;

        /// <summary>
        /// 由四个途经点 p0,p1,p2,p3 推出<b>段 p1→p2</b> 的三次贝塞尔控制点（写进
        /// <paramref name="bezier"/>[0..3]）。端点外的点（首段的前一点、末段的后一点）
        /// 由调用方<b>重复端点</b>给出——那是标准做法，效果是端点处切线指向段内。
        /// </summary>
        public static void CatmullRomToBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Vector2[] bezier)
        {
            if (bezier == null || bezier.Length < ControlPointsPerSegment) return;

            bezier[0] = p1;
            bezier[1] = p1 + (p2 - p0) / 6f;
            bezier[2] = p2 - (p3 - p1) / 6f;
            bezier[3] = p2;
        }

        /// <summary>贝塞尔上参数 t 处的点（de Casteljau，t 夹到 [0,1]）</summary>
        public static Vector2 Evaluate(Vector2[] bezier, float t)
        {
            if (bezier == null || bezier.Length < ControlPointsPerSegment) return Vector2.zero;
            t = Mathf.Clamp01(t);

            Vector2 a = Vector2.Lerp(bezier[0], bezier[1], t);
            Vector2 b = Vector2.Lerp(bezier[1], bezier[2], t);
            Vector2 c = Vector2.Lerp(bezier[2], bezier[3], t);
            Vector2 d = Vector2.Lerp(a, b, t);
            Vector2 e = Vector2.Lerp(b, c, t);
            return Vector2.Lerp(d, e, t);
        }

        /// <summary>
        /// 贝塞尔在参数 t 处的<b>切线</b>（导数，未归一化；退化时返回零向量，由调用方决定怎么办）。
        /// 箭头要"顺着路径"就必须吃这条，而不是吃两端点的连线——后者在弯曲处会偏。
        /// </summary>
        public static Vector2 Tangent(Vector2[] bezier, float t)
        {
            if (bezier == null || bezier.Length < ControlPointsPerSegment) return Vector2.zero;
            t = Mathf.Clamp01(t);

            float u = 1f - t;
            // B'(t) = 3(1-t)^2 (P1-P0) + 6(1-t)t (P2-P1) + 3t^2 (P3-P2)
            return 3f * u * u * (bezier[1] - bezier[0])
                + 6f * u * t * (bezier[2] - bezier[1])
                + 3f * t * t * (bezier[3] - bezier[2]);
        }

        /// <summary>
        /// 两点之间的<b>共线</b>三次贝塞尔控制点（内控制点落在 1/3、2/3）——细分多少节都是那条直线。
        /// 一支箭头的杆走这一条：杆和路径用同一个渲染器、同一套宽度与起止色，
        /// 就不需要"填充四边形"那第二套画法（他 2026-10-26 的纠正，§29）。
        /// </summary>
        public static void Straight(Vector2 from, Vector2 to, Vector2[] bezier)
        {
            bezier[0] = from;
            bezier[1] = Vector2.Lerp(from, to, 1f / 3f);
            bezier[2] = Vector2.Lerp(from, to, 2f / 3f);
            bezier[3] = to;
        }

        /// <summary>
        /// 把"整条路径上的位置"（0＝起点，1＝末端）换成<b>段号 + 段内参数</b>。
        /// 段数 ≤0 时给 0 段、参数 1（调用方按"没有路径"处理）。
        /// </summary>
        public static void ResolvePosition(int segmentCount, float along, out int segment, out float t)
        {
            segment = 0;
            t = 1f;
            if (segmentCount <= 0) return;

            float scaled = Mathf.Clamp01(along) * segmentCount;
            int index = Mathf.FloorToInt(scaled);
            if (index >= segmentCount) index = segmentCount - 1;
            segment = index;
            t = scaled - index;
            if (index == segmentCount - 1 && t > 0f) t = scaled - index;   // 末段允许走到 1
        }
    }
}