using System;
using UnityEngine;

namespace XericUI.XChart.Mesh
{
    /// <summary>
    /// 箭头的像素空间几何：<b>杆 + 头</b>两组四边形。
    ///
    /// <para>放在像素空间算是有意的：箭头的<b>宽度</b>要恒定（不管视角怎么转、向量投得多短，
    /// 头都得是个看得见的头），而长度和方向已经由 <see cref="Space.ChartToolSpace"/> 投影给出来了。
    /// 所以这里只做"沿单位方向量长度、按垂直方向量宽度"这一件事，纯数学、离线可验。</para>
    ///
    /// <para><see cref="HeadLength"/> 会被钳到向量屏幕长度的 60%，因此短箭头的杆不会被头穿过去；
    /// 屏幕长度接近 0 时返回 false，由调用方决定是画个点还是什么都不画。</para>
    /// </summary>
    public static class XChartArrowGeometry
    {
        /// <summary>杆最长能占屏幕长度的比例上限之外，头也不得超过这个比例</summary>
        public const float HeadLengthRatioCap = 0.6f;

        /// <summary>屏幕长度小于这个值就当"没有方向"，交给调用方的退化分支。</summary>
        public const float MinScreenLength = 1e-3f;

        /// <summary>杆的四角（沿箭头方向绕一圈）</summary>
        public static void BuildShaft(Vector2 from, Vector2 unit, Vector2 side, float shaftEndDistance,
            float width, out Vector2 a, out Vector2 b, out Vector2 c, out Vector2 d)
        {
            float half = width * 0.5f;
            Vector2 end = from + unit * shaftEndDistance;
            a = from + side * half;
            b = end + side * half;
            c = end - side * half;
            d = from - side * half;
        }

        /// <summary>头的四角：两个底角 + 尖点（尖点重复一次，四边形即三角形）</summary>
        public static void BuildHead(Vector2 baseCenter, Vector2 tip, Vector2 side, float headWidth,
            out Vector2 a, out Vector2 b, out Vector2 c, out Vector2 d)
        {
            float half = headWidth * 0.5f;
            a = baseCenter + side * half;
            b = tip;
            c = tip;
            d = baseCenter - side * half;
        }

        /// <summary>
        /// 一条箭头的完整拆分。<paramref name="to"/> 是屏幕上的尖点。
        /// 返回 false 表示屏幕长度退化。
        /// </summary>
        public static bool TryBuild(Vector2 from, Vector2 to, float shaftWidth, float headLength,
            float headWidth, out Vector2 shaftA, out Vector2 shaftB, out Vector2 shaftC, out Vector2 shaftD,
            out Vector2 headA, out Vector2 headB, out Vector2 headC, out Vector2 headD)
        {
            shaftA = shaftB = shaftC = shaftD = headA = headB = headC = headD = Vector2.zero;

            Vector2 axis = to - from;
            float length = axis.magnitude;
            if (length < MinScreenLength) return false;

            Vector2 unit = axis / length;
            Vector2 side = new Vector2(-unit.y, unit.x);
            float head = Mathf.Min(Mathf.Max(0f, headLength), length * HeadLengthRatioCap);

            BuildShaft(from, unit, side, length - head, shaftWidth, out shaftA, out shaftB, out shaftC, out shaftD);
            BuildHead(from + unit * (length - head), to, side, headWidth, out headA, out headB, out headC, out headD);
            return true;
        }
    }
}
