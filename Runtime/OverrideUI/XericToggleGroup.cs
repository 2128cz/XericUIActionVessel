using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XericUI.Helper;

namespace XericUI.OverrideUI
{
    [AddComponentMenu("Xeric UI Vessel/UI/ToggleGroup", 31)]
    [DisallowMultipleComponent]
    public class XericToggleGroup : ToggleGroup
    {
        
        #region 组件替换
#if UNITY_EDITOR
        [UnityEditor.MenuItem("CONTEXT/ToggleGroup/Replace To XericToggleGroup", true)]
        private static bool ValidateReplaceToXeric(UnityEditor.MenuCommand command)
            => command.context is ToggleGroup and not XericToggleGroup;

        [UnityEditor.MenuItem("CONTEXT/ToggleGroup/Replace To XericToggleGroup", false, 10)]
        private static void ReplaceToXeric(UnityEditor.MenuCommand command) 
            => command.ReplaceCommandContext<ToggleGroup, XericToggleGroup>();

        [UnityEditor.MenuItem("CONTEXT/ToggleGroup/Replace To ToggleGroup", true)]
        private static bool ValidateReplaceToUnity(UnityEditor.MenuCommand command)
            => command.context is XericToggleGroup;

        [UnityEditor.MenuItem("CONTEXT/ToggleGroup/Replace To ToggleGroup", false, 10)]
        private static void ReplaceToUnity(UnityEditor.MenuCommand command) 
            => command.ReplaceCommandContext<XericToggleGroup, ToggleGroup>();
#endif
        #endregion
    }
}