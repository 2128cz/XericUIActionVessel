using UnityEngine;
using UnityEngine.UI;
using XericUI.Helper;
using XericUI.ReflectionUIGenerator;

namespace XericUI.OverrideUI
{
    /// <summary>
    /// 带字段标记的增强输入框组件
    /// 继承 Unity InputField，额外支持 XuiavrgFieldTag 标记，便于反射界面生成器自动绑定
    /// </summary>
    [AddComponentMenu("Xeric UI Vessel/Attribute/InputField", 51)]
    public class XericUIrAttributeInputField : InputField, IXuiavrgFieldTagProvider
    {
        [SerializeField]
        [Tooltip("Entry 功能标记，用于自动绑定匹配")]
        private XuiavrgFieldTag _entryTag = XuiavrgFieldTag.Input;

        [SerializeField]
        [Tooltip("同一 Tag 存在多个时的索引区分")]
        private int _tagIndex = 0;

        public XuiavrgFieldTag EntryTag => _entryTag;
        public int TagIndex => _tagIndex;

        #region 组件替换

#if UNITY_EDITOR
        [UnityEditor.MenuItem("CONTEXT/InputField/Replace To/Xeric (Attribute)", true)]
        private static bool ValidateReplaceToAttribute(UnityEditor.MenuCommand command)
            => command.context is InputField and not XericUIrAttributeInputField;

        [UnityEditor.MenuItem("CONTEXT/InputField/Replace To/Xeric (Attribute)", false, 10)]
        private static void ReplaceToAttribute(UnityEditor.MenuCommand command)
            => command.ReplaceCommandContext<InputField, XericUIrAttributeInputField,
                (Graphic targetGraphic, string text, InputField.OnChangeEvent onValueChanged, InputField.EndEditEvent onEndEdit)>(
                o => (o.targetGraphic, o.text, o.onValueChanged, o.onEndEdit),
                (t, d) =>
                {
                    t.targetGraphic = d.targetGraphic;
                    t.text = d.text;
                    t.onValueChanged = d.onValueChanged;
                    t.onEndEdit = d.onEndEdit;
                });

        [UnityEditor.MenuItem("CONTEXT/InputField/Replace To/Unity InputField", true)]
        private static bool ValidateReplaceToUnity(UnityEditor.MenuCommand command)
            => command.context is XericUIrAttributeInputField;

        [UnityEditor.MenuItem("CONTEXT/InputField/Replace To/Unity InputField", false, 100)]
        private static void ReplaceToUnity(UnityEditor.MenuCommand command)
            => command.ReplaceCommandContext<XericUIrAttributeInputField, InputField,
                (Graphic targetGraphic, string text, InputField.OnChangeEvent onValueChanged, InputField.EndEditEvent onEndEdit)>(
                o => (o.targetGraphic, o.text, o.onValueChanged, o.onEndEdit),
                (t, d) =>
                {
                    t.targetGraphic = d.targetGraphic;
                    t.text = d.text;
                    t.onValueChanged = d.onValueChanged;
                    t.onEndEdit = d.onEndEdit;
                });
#endif

        #endregion
    }
}
