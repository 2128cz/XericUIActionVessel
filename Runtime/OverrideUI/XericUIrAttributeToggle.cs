using UnityEngine;
using UnityEngine.UI;
using XericUI.Helper;
using XericUI.ReflectionUIGenerator;

namespace XericUI.OverrideUI
{
    /// <summary>
    /// 带字段标记的增强开关组件
    /// 继承 XericToggle，额外支持 XuiavrgFieldTag 标记，便于反射界面生成器自动绑定
    /// </summary>
    [AddComponentMenu("Xeric UI Vessel/Attribute/Toggle", 32)]
    public class XericUIrAttributeToggle : XericToggle, IXuiavrgFieldTagProvider
    {
        [SerializeField]
        [Tooltip("Entry 功能标记，用于自动绑定匹配")]
        private XuiavrgFieldTag _entryTag = XuiavrgFieldTag.Toggle;

        [SerializeField]
        [Tooltip("同一 Tag 存在多个时的索引区分")]
        private int _tagIndex = 0;

        public XuiavrgFieldTag EntryTag => _entryTag;
        public int TagIndex => _tagIndex;

        #region 组件替换

#if UNITY_EDITOR
        [UnityEditor.MenuItem("CONTEXT/Toggle/Replace To/Xeric (Attribute)", true)]
        private static bool ValidateReplaceToAttribute(UnityEditor.MenuCommand command)
            => command.context is Toggle and not XericUIrAttributeToggle;

        [UnityEditor.MenuItem("CONTEXT/Toggle/Replace To/Xeric (Attribute)", false, 12)]
        private static void ReplaceToAttribute(UnityEditor.MenuCommand command)
            => command.ReplaceCommandContext<Toggle, XericUIrAttributeToggle,
                (Graphic graphic, ToggleGroup group, Toggle.ToggleEvent onValueChanged)>(
                o => (o.graphic, o.group, o.onValueChanged),
                (t, d) =>
                {
                    t.graphic = d.graphic;
                    t.group = d.group;
                    t.onValueChanged = d.onValueChanged;
                });

        [UnityEditor.MenuItem("CONTEXT/Toggle/Replace To/Unity Toggle (Attribute)", true)]
        private static bool ValidateReplaceToUnity(UnityEditor.MenuCommand command)
            => command.context is XericUIrAttributeToggle;

        [UnityEditor.MenuItem("CONTEXT/Toggle/Replace To/Unity Toggle (Attribute)", false, 100)]
        private static void ReplaceToUnity(UnityEditor.MenuCommand command)
            => command.ReplaceCommandContext<XericUIrAttributeToggle, Toggle,
                (Graphic graphic, ToggleGroup group, Toggle.ToggleEvent onValueChanged)>(
                o => (o.graphic, o.group, o.onValueChanged),
                (t, d) =>
                {
                    t.graphic = d.graphic;
                    t.group = d.group;
                    t.onValueChanged = d.onValueChanged;
                });
#endif

        #endregion
    }
}
