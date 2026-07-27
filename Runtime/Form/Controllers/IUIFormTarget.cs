using UnityEngine;

namespace XericUI.Form
{
    /// <summary>
    /// 窗体悬停目标接口。
    /// 实现此接口的组件可注册到 UIFormManager，由管理器统一检测鼠标悬停状态。
    /// </summary>
    public interface IUIFormTarget
    {
        /// <summary>
        /// 进入范围（屏幕空间 Rect，原点为左下角，单位为像素）。
        /// 鼠标进入此范围时触发 OnPointerEnter。
        /// </summary>
        Rect GetEnterRect();

        /// <summary>
        /// 退出范围（屏幕空间 Rect，原点为左下角，单位为像素）。
        /// 应大于等于 EnterRect，鼠标离开此范围时触发 OnPointerExit，避免边界抖动。
        /// </summary>
        Rect GetExitRect();

        /// <summary>
        /// 鼠标进入范围内时由 UIFormManager 调用。
        /// </summary>
        void OnPointerEnter();

        /// <summary>
        /// 鼠标离开退出范围时由 UIFormManager 调用。
        /// </summary>
        void OnPointerExit();

        /// <summary>
        /// 此目标是否处于激活状态。
        /// 返回 false 时管理器跳过此目标的检测。
        /// </summary>
        bool IsActive { get; }
    }
}
