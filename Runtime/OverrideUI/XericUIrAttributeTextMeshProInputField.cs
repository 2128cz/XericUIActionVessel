using XericUI.ReflectionUIGenerator;

#if dUI_TextMeshPro
using TMPro;
#endif

namespace XericUI.OverrideUI
{
    /// <summary>
    /// 带字段标记的增强 TextMeshPro 输入框组件
    /// 附加在 TMP_InputField 所在的 GameObject 上，提供 XuiavrgFieldTag 标记，
    /// 便于反射界面生成器自动绑定。支持从 TMP_InputField 一键替换。
    /// </summary>
    [UnityEngine.AddComponentMenu("Xeric UI Vessel/Attribute/TextMeshPro InputField", 51)]
    public class XericUIrAttributeTextMeshProInputField : UnityEngine.MonoBehaviour, IXuiavrgFieldTagProvider
    {
#if dUI_TextMeshPro
        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("关联的 TMP_InputField 组件引用（自动获取或手动指定）")]
        private TMP_InputField _targetInputField;

        /// <summary>输入文本内容</summary>
        public string TextContent
        {
            get => _targetInputField != null ? _targetInputField.text : string.Empty;
            set { if (_targetInputField) _targetInputField.text = value; }
        }

        /// <summary>占位符文本</summary>
        public string Placeholder
        {
            get => _targetInputField != null && _targetInputField.placeholder is TMPro.TMP_Text ph
                ? ph.text : string.Empty;
            set
            {
                if (_targetInputField && _targetInputField.placeholder is TMPro.TMP_Text ph)
                    ph.text = value;
            }
        }

        private void Awake()
        {
            if (_targetInputField == null)
                _targetInputField = GetComponent<TMP_InputField>();
        }
#endif

        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("Entry 功能标记，用于自动绑定匹配")]
        private XuiavrgFieldTag _entryTag = XuiavrgFieldTag.Input;

        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("同一 Tag 存在多个时的索引区分")]
        private int _tagIndex = 0;

        public XuiavrgFieldTag EntryTag => _entryTag;
        public int TagIndex => _tagIndex;

        #region 组件替换

#if UNITY_EDITOR
        [UnityEditor.MenuItem("CONTEXT/TMP_InputField/Replace To/Xeric (Attribute)", true)]
        private static bool ValidateReplaceToAttribute(UnityEditor.MenuCommand command)
        {
#if dUI_TextMeshPro
            return command.context is TMP_InputField && !(command.context is XericUIrAttributeTextMeshProInputField);
#else
            return false;
#endif
        }

        [UnityEditor.MenuItem("CONTEXT/TMP_InputField/Replace To/Xeric (Attribute)", false, 10)]
        private static void ReplaceToAttribute(UnityEditor.MenuCommand command)
        {
#if dUI_TextMeshPro
            var tmpComp = command.context as UnityEngine.Component;
            if (tmpComp != null)
                tmpComp.gameObject.AddComponent<XericUIrAttributeTextMeshProInputField>();
#endif
        }

        [UnityEditor.MenuItem("CONTEXT/TMP_InputField/Replace To/Unity TMP_InputField", true)]
        private static bool ValidateReplaceToUnity(UnityEditor.MenuCommand command)
            => command.context is XericUIrAttributeTextMeshProInputField;

        [UnityEditor.MenuItem("CONTEXT/TMP_InputField/Replace To/Unity TMP_InputField", false, 100)]
        private static void ReplaceToUnity(UnityEditor.MenuCommand command)
        {
            var comp = command.context as XericUIrAttributeTextMeshProInputField;
            if (comp != null)
                UnityEngine.Object.DestroyImmediate(comp);
        }
#endif

        #endregion
    }
}
