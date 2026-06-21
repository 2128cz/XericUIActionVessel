using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XericUI.Helper;
using XericUI.ReflectionUIGenerator;

namespace XericUI.OverrideUI
{
    /// <summary>
    /// 带字段标记的增强下拉框组件
    /// 继承 Unity Dropdown，额外支持 XuiavrgFieldTag 标记，便于反射界面生成器自动绑定
    /// </summary>
    [AddComponentMenu("Xeric UI Vessel/Attribute/Dropdown", 51)]
    public class XericUIrAttributeDropdown : Dropdown, IXuiavrgFieldTagProvider
    {
        [SerializeField]
        [Tooltip("Entry 功能标记，用于自动绑定匹配")]
        private XuiavrgFieldTag _entryTag = XuiavrgFieldTag.Dropdown;

        [SerializeField]
        [Tooltip("同一 Tag 存在多个时的索引区分")]
        private int _tagIndex = 0;

        public XuiavrgFieldTag EntryTag => _entryTag;
        public int TagIndex => _tagIndex;

        #region 组件替换

#if UNITY_EDITOR
        [UnityEditor.MenuItem("CONTEXT/Dropdown/Replace To XericUIrAttributeDropdown", true)]
        private static bool ValidateReplaceToAttribute(UnityEditor.MenuCommand command)
            => command.context is Dropdown and not XericUIrAttributeDropdown;

        [UnityEditor.MenuItem("CONTEXT/Dropdown/Replace To XericUIrAttributeDropdown", false, 10)]
        private static void ReplaceToAttribute(UnityEditor.MenuCommand command)
            => command.ReplaceCommandContext<Dropdown, XericUIrAttributeDropdown,
                (Graphic targetGraphic, int value, List<OptionData> options, DropdownEvent onValueChanged)>(
                o =>
                    (o.targetGraphic, o.value, o.options, o.onValueChanged),
                (t, d) =>
                {
                    t.targetGraphic = d.targetGraphic;
                    t.value = d.value;
                    t.options = d.options;
                    t.onValueChanged = d.onValueChanged;
                });

        [UnityEditor.MenuItem("CONTEXT/Dropdown/Replace To Dropdown", true)]
        private static bool ValidateReplaceToUnity(UnityEditor.MenuCommand command)
            => command.context is XericUIrAttributeDropdown;

        [UnityEditor.MenuItem("CONTEXT/Dropdown/Replace To Dropdown", false, 10)]
        private static void ReplaceToUnity(UnityEditor.MenuCommand command)
            => command.ReplaceCommandContext<XericUIrAttributeDropdown, Dropdown,
                (Graphic targetGraphic, int value, List<OptionData> options, DropdownEvent onValueChanged)>(
                o =>
                    (o.targetGraphic, o.value, o.options, o.onValueChanged),
                (t, d) =>
                {
                    t.targetGraphic = d.targetGraphic;
                    t.value = d.value;
                    t.options = d.options;
                    t.onValueChanged = d.onValueChanged;
                });
#endif

        #endregion
    }
}
