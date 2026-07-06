using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using XericUI.Helper;

namespace XericUI.OverrideUI
{
    /// <summary>
    /// 图像组件
    /// </summary>
    /// <remarks>
    /// 这个组件主要为了提供graphic层的事件，用于应对部分无法重写的ui组件事件
    /// </remarks>
    [AddComponentMenu("Xeric UI Vessel/UI/Image", 50)]
    public class XericImage : Image
    {
        /// <summary>
        /// 针对toggle的回报(ison)
        /// </summary>
        internal Action<bool> OnToggleCrossFadeCall;
        public override void CrossFadeColor(Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha, bool useRGB)
        {
            base.CrossFadeColor(targetColor, duration, ignoreTimeScale, useAlpha, useRGB);
            OnToggleCrossFadeCall?.Invoke(targetColor.a > 0);
        }

        #region 组件替换
#if UNITY_EDITOR
        [UnityEditor.MenuItem("CONTEXT/Image/Replace To/Xeric Image", true)]
        private static bool ValidateReplaceToXeric(UnityEditor.MenuCommand command)
            => command.context is Image and not XericImage;

        [UnityEditor.MenuItem("CONTEXT/Image/Replace To/Xeric Image", false, 10)]
        private static void ReplaceToXeric(UnityEditor.MenuCommand command) 
            => command.ReplaceCommandContext<Image, XericImage, Sprite>(
                o =>
                    o.sprite,
                (t, d) =>
                {
                    t.sprite = d;
                });

        [UnityEditor.MenuItem("CONTEXT/Image/Replace To/Unity Image (Native)", true)]
        private static bool ValidateReplaceToUnity(UnityEditor.MenuCommand command)
            => command.context is XericImage;

        [UnityEditor.MenuItem("CONTEXT/Image/Replace To/Unity Image (Native)", false, 100)]
        private static void ReplaceToUnity(UnityEditor.MenuCommand command) 
            => command.ReplaceCommandContext<XericImage, Image, Sprite>(
                o =>
                    o.sprite,
                (t, d) =>
                {
                    t.sprite = d;
                });
#endif
        #endregion
    }
}