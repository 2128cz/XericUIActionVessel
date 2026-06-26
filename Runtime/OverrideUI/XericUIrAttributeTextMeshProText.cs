using XericUI.Helper;
using XericUI.ReflectionUIGenerator;

#if dUI_TextMeshPro
using TMPro;
using UnityEngine;
#endif

namespace XericUI.OverrideUI
{
    /// <summary>
    /// 带字段标记的增强 TextMeshPro 文本组件
    /// 附加在 TMP_Text 所在的 GameObject 上，提供 XuiavrgFieldTag 标记，
    /// 便于反射界面生成器自动绑定。同时支持从 TMP_Text 一键替换。
    /// </summary>
    [UnityEngine.AddComponentMenu("Xeric UI Vessel/Attribute/TextMeshPro Text", 51)]
    public class XericUIrAttributeTextMeshProText : UnityEngine.MonoBehaviour, IXuiavrgFieldTagProvider
    {
#if dUI_TextMeshPro
        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("关联的 TMP_Text 组件引用（自动获取或手动指定）")]
        private TMP_Text _targetText;

        /// <summary>文本内容（通过 _targetText 读写）</summary>
        public string TextContent
        {
            get => _targetText != null ? _targetText.text : string.Empty;
            set { if (_targetText) _targetText.text = value; }
        }

        /// <summary>文本颜色</summary>
        public UnityEngine.Color TextColor
        {
            get => _targetText != null ? _targetText.color : UnityEngine.Color.white;
            set { if (_targetText) _targetText.color = value; }
        }

        private void Awake()
        {
            if (_targetText == null)
                _targetText = GetComponent<TMP_Text>();
        }
#endif

        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("Entry 功能标记，用于自动绑定匹配")]
        private XuiavrgFieldTag _entryTag = XuiavrgFieldTag.Label;

        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("同一 Tag 存在多个时的索引区分")]
        private int _tagIndex = 0;

        public XuiavrgFieldTag EntryTag => _entryTag;
        public int TagIndex => _tagIndex;

        #region 组件替换

#if UNITY_EDITOR
        [UnityEditor.MenuItem("CONTEXT/TextMeshProUGUI/Replace To/Xeric (Attribute)", true)]
        private static bool ValidateReplaceToAttribute(UnityEditor.MenuCommand command)
        {
#if dUI_TextMeshPro
            return command.context is TextMeshProUGUI && !(command.context is XericUIrAttributeTextMeshProText);
#else
            return false;
#endif
        }

        [UnityEditor.MenuItem("CONTEXT/TextMeshProUGUI/Replace To/Xeric (Attribute)", false, 10)]
        private static void ReplaceToAttribute(UnityEditor.MenuCommand command)
        {
#if dUI_TextMeshPro
            var tmpComp = command.context as Component;
            if (tmpComp != null)
                tmpComp.gameObject.AddComponent<XericUIrAttributeTextMeshProText>();
#endif
        }

        [UnityEditor.MenuItem("CONTEXT/TextMeshProUGUI/Replace To/Unity TextMeshPro", true)]
        private static bool ValidateReplaceToUnity(UnityEditor.MenuCommand command)
            => command.context is XericUIrAttributeTextMeshProText;

        [UnityEditor.MenuItem("CONTEXT/TextMeshProUGUI/Replace To/Unity TextMeshPro", false, 100)]
        private static void ReplaceToUnity(UnityEditor.MenuCommand command)
        {
            var comp = command.context as XericUIrAttributeTextMeshProText;
            if (comp != null)
                UnityEngine.Object.DestroyImmediate(comp);
        }
#endif

        #endregion
    }
}
