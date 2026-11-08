using System;
using TMPro;
using UnityEngine;

namespace XericUI.XChart.Text
{
    /// <summary>标签矩形相对它那一点的位置：9 档（四角 + 四边中点 + 中心）。</summary>
    public enum XChartLabelAnchor
    {
        LowerLeft,
        LowerCenter,
        LowerRight,
        MiddleLeft,
        Center,
        MiddleRight,
        UpperLeft,
        UpperCenter,
        UpperRight,
    }

    /// <summary>
    /// 锚点的<b>纯算</b>部分（离线可验）。
    ///
    /// <para><b>为什么锚点与对齐必须出自同一张表</b>：锚定在左下角的矩形如果把字居中对齐，
    /// 字就横跨在那一点两侧，"标签贴在点上"这句话当场作废。以前这两件事各写各的
    /// （锚点一处、对齐来自池的默认值），所以改锚点而字仍居中是画面上最难归因的一种错。</para>
    /// </summary>
    public static class XChartLabelAnchorMath
    {
        /// <summary>九档锚点各自的那个位置（[0,1] 内的矩形锚位）。九档互不相同是判据，不是巧合。</summary>
        public static Vector2 AnchorOf(XChartLabelAnchor anchor)
        {
            switch (anchor)
            {
                case XChartLabelAnchor.LowerLeft: return new Vector2(0f, 0f);
                case XChartLabelAnchor.LowerCenter: return new Vector2(0.5f, 0f);
                case XChartLabelAnchor.LowerRight: return new Vector2(1f, 0f);
                case XChartLabelAnchor.MiddleLeft: return new Vector2(0f, 0.5f);
                case XChartLabelAnchor.MiddleRight: return new Vector2(1f, 0.5f);
                case XChartLabelAnchor.UpperLeft: return new Vector2(0f, 1f);
                case XChartLabelAnchor.UpperCenter: return new Vector2(0.5f, 1f);
                case XChartLabelAnchor.UpperRight: return new Vector2(1f, 1f);
                default: return new Vector2(0.5f, 0.5f);   // Center
            }
        }

        /// <summary>
        /// 这一档锚点对应的文字对齐：<b>由同一个锚点算出来</b>（横分量取锚点的 x、纵分量取锚点的 y）。
        /// 这样"贴在左下角"和"字从左下角起排"是同一件事，不再有第二处定义。
        /// </summary>
        public static TextAlignmentOptions AlignmentFor(XChartLabelAnchor anchor)
        {
            Vector2 a = AnchorOf(anchor);
            if (Mathf.Abs(a.x - 0.5f) < 1e-4f && Mathf.Abs(a.y - 0.5f) < 1e-4f) return TextAlignmentOptions.Center;
            if (Mathf.Abs(a.y - 0.5f) < 1e-4f) return a.x < 0.5f ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
            if (Mathf.Abs(a.x - 0.5f) < 1e-4f) return a.y < 0.5f ? TextAlignmentOptions.Bottom : TextAlignmentOptions.Top;
            if (a.y < 0.5f) return a.x < 0.5f ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.BottomRight;
            return a.x < 0.5f ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.TopRight;
        }

        /// <summary>
        /// 偏移：<b>非有限数当成零</b>。NaN 进 anchoredPosition 会把这一件字整个挪出画面，
        /// 而且它不报错——这正是"计数全对、画面空无一物"那一族。
        /// </summary>
        public static Vector2 SafeOffset(Vector2 offset)
        {
            if (float.IsNaN(offset.x) || float.IsInfinity(offset.x)) offset.x = 0f;
            if (float.IsNaN(offset.y) || float.IsInfinity(offset.y)) offset.y = 0f;
            return offset;
        }
    }
}
