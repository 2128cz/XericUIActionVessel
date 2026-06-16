using UnityEngine;

namespace XericUI.VisualForm
{
    /// <summary>
    /// 碰撞分离静态工具类。
    /// 提供两个矩形之间的排斥力计算，用于屏幕空间UI碰撞分离。
    /// 算法参考 BubblePhysicsSolver.CalculateRepulsionForce。
    /// </summary>
    public static class CollisionUtils
    {
        /// <summary>
        /// 计算两个重叠矩形的相互排斥向量（从A推离B的方向）。
        /// 沿最小重叠轴方向推开，推力大小 = 重叠深度 × 推力系数。
        /// </summary>
        /// <param name="rectA">矩形A</param>
        /// <param name="rectB">矩形B</param>
        /// <param name="pushForce">推力系数（0到1），默认0.5</param>
        /// <returns>A应受到的推离向量（B应施加相反向量）</returns>
        public static Vector2 CalculateRepulsion(Rect rectA, Rect rectB, float pushForce = 0.5f)
        {
            Vector2 centerA = rectA.center;
            Vector2 centerB = rectB.center;
            Vector2 delta = centerA - centerB;

            float halfW = (rectA.width + rectB.width) * 0.5f;
            float halfH = (rectA.height + rectB.height) * 0.5f;

            float overlapX = halfW - Mathf.Abs(delta.x);
            float overlapY = halfH - Mathf.Abs(delta.y);

            if (overlapX <= 0f || overlapY <= 0f)
                return Vector2.zero;

            // 沿最小重叠轴推开
            if (overlapX < overlapY)
            {
                return new Vector2(Mathf.Sign(delta.x) * overlapX * pushForce, 0f);
            }
            else
            {
                return new Vector2(0f, Mathf.Sign(delta.y) * overlapY * pushForce);
            }
        }

        /// <summary>
        /// 判断两个矩形是否重叠
        /// </summary>
        public static bool Overlaps(Rect a, Rect b)
        {
            return a.xMin < b.xMax && a.xMax > b.xMin &&
                   a.yMin < b.yMax && a.yMax > b.yMin;
        }
    }
}
