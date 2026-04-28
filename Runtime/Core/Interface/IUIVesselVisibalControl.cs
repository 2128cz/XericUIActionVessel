using XericUI.Helper;

namespace XericUI.Core.Interface
{
    /// <summary>
    /// Xeric UI Vessel容器控制接口
    /// </summary>
    public interface IUIVesselVisibalControl
    {
        /// <summary>
        /// 获取当前元素的可见性
        /// </summary>
        public bool GetUIVisibal { get; }
        /// <summary>
        /// 当前元素处于可见范围时（预载范围）
        /// </summary>
        public void UnderVisible();
        /// <summary>
        /// 当前元素离开可见范围时（预载范围）
        /// </summary>
        public void OutOfVisible();
                
        public void UpdateLayout(GridLayoutCalculator.LayoutItem layoutData);
    }
}