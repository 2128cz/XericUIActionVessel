namespace XericUI.Form
{
    /// <summary>
    /// 窗口当前状态。
    /// </summary>
    public enum WindowState
    {
        /// <summary>普通窗口化</summary>
        Normal,

        /// <summary>全屏最大化</summary>
        Maximized,

        /// <summary>最小化（隐藏并保存在 Manager 中）</summary>
        Minimized,

        /// <summary>边缘自动隐藏（贴边滑出屏幕）</summary>
        EdgeHidden
    }
}
