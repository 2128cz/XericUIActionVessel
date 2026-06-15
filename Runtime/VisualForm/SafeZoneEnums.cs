using System;

namespace XericUI.VisualForm
{
    /// <summary>
    /// 安全区检测模式
    /// </summary>
    public enum SafeZoneMode
    {
        /// <summary>矩形安全区</summary>
        Rect,
        /// <summary>椭圆形安全区</summary>
        Ellipse
    }

    /// <summary>
    /// 窗口溢出状态（Flags枚举，支持位运算叠加）
    /// </summary>
    [Flags]
    public enum OverflowState
    {
        /// <summary>在安全区范围内</summary>
        InRange = 0,
        /// <summary>超出安全区</summary>
        OutOfSafeZone = 1 << 0,
        /// <summary>超出屏幕</summary>
        OutOfScreen = 1 << 1
    }

    /// <summary>
    /// 安全区坐标单位
    /// </summary>
    public enum SafeZoneUnit
    {
        /// <summary>像素（Canvas本地空间绝对值）</summary>
        Pixel,
        /// <summary>屏幕比例（0-1，相对于Canvas尺寸）</summary>
        Ratio
    }
}
