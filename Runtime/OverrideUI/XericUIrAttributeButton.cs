using System;
using UnityEngine;
using UnityEngine.UI;
using XericUI.Helper;
using XericUI.ReflectionUIGenerator;

namespace XericUI.OverrideUI
{
    /// <summary>
    /// 带字段标记的增强按钮组件
    /// 继承 XericButton，额外支持 XuiavrgFieldTag 标记，便于反射界面生成器自动绑定
    /// </summary>
    [AddComponentMenu("Xeric UI Vessel/Attribute/Button", 51)]
    public class XericUIrAttributeButton : XericButton, IXuiavrgFieldTagProvider
    {
        [SerializeField]
        [Tooltip("Entry 功能标记，用于自动绑定匹配")]
        public XuiavrgFieldTag _entryTag = XuiavrgFieldTag.Button;

        [SerializeField]
        [Tooltip("同一 Tag 存在多个时的索引区分")]
        public int _tagIndex = 0;

        public XuiavrgFieldTag EntryTag => _entryTag;
        public int TagIndex => _tagIndex;

        #region 组件替换

#if UNITY_EDITOR
        [UnityEditor.MenuItem("CONTEXT/Button/Replace To/Xeric (Attribute)", true)]
        private static bool ValidateReplaceToXeric(UnityEditor.MenuCommand command)
            => command.context is Button and not XericUIrAttributeButton;

        [UnityEditor.MenuItem("CONTEXT/Button/Replace To/Xeric (Attribute)", false, 12)]
        private static void ReplaceToXeric(UnityEditor.MenuCommand command)
            => command.ReplaceCommandContext<Button, XericUIrAttributeButton,
                (Graphic targetGraphic, Button.ButtonClickedEvent onClick)>(
                o => (o.targetGraphic, o.onClick),
                (t, d) => { t.targetGraphic = d.targetGraphic; t.onClick = d.onClick; });

        [UnityEditor.MenuItem("CONTEXT/Button/Replace To/Unity Button (Attribute)", true)]
        private static bool ValidateReplaceToUnity(UnityEditor.MenuCommand command)
            => command.context is XericUIrAttributeButton;

        [UnityEditor.MenuItem("CONTEXT/Button/Replace To/Unity Button (Attribute)", false, 100)]
        private static void ReplaceToUnity(UnityEditor.MenuCommand command)
            => command.ReplaceCommandContext<XericUIrAttributeButton, Button,
                (Graphic targetGraphic, Button.ButtonClickedEvent onClick)>(
                o => (o.targetGraphic, o.onClick),
                (t, d) => { t.targetGraphic = d.targetGraphic; t.onClick = d.onClick; });
#endif

        #endregion
    }
}
