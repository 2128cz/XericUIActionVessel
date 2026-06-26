using UnityEngine;
using UnityEngine.UI;
using XericUI.Helper;
using XericUI.ReflectionUIGenerator;

namespace XericUI.OverrideUI
{
    /// <summary>
    /// 带字段标记的增强文本组件
    /// 继承 XericImage，额外支持 XuiavrgFieldTag 标记。
    /// 主要用于预制体中标记 Label/Output/Sign 等文本功能元素
    /// </summary>
    [AddComponentMenu("Xeric UI Vessel/Attribute/Text", 51)]
    public class XericUIrAttributeText : XericImage, IXuiavrgFieldTagProvider
    {
        [SerializeField]
        [Tooltip("关联的 Text 组件引用（可选，用于读取/写入文本内容）")]
        private Text _targetText;

        [SerializeField]
        [Tooltip("Entry 功能标记，用于自动绑定匹配")]
        private XuiavrgFieldTag _entryTag = XuiavrgFieldTag.Label;

        [SerializeField]
        [Tooltip("同一 Tag 存在多个时的索引区分")]
        private int _tagIndex = 0;

        public XuiavrgFieldTag EntryTag => _entryTag;
        public int TagIndex => _tagIndex;

        /// <summary>文本内容（通过 _targetText 读写）</summary>
        public string TextContent
        {
            get => _targetText != null ? _targetText.text : string.Empty;
            set { if (_targetText) _targetText.text = value; }
        }

        /// <summary>文本颜色</summary>
        public Color TextColor
        {
            get => _targetText != null ? _targetText.color : Color.white;
            set { if (_targetText) _targetText.color = value; }
        }

        #region 组件替换

#if UNITY_EDITOR
        [UnityEditor.MenuItem("CONTEXT/Image/Replace To/Xeric (AttributeText)", true)]
        private static bool ValidateReplaceToAttribute(UnityEditor.MenuCommand command)
            => command.context is Image and not XericUIrAttributeText;

        [UnityEditor.MenuItem("CONTEXT/Image/Replace To/Xeric (AttributeText)", false, 13)]
        private static void ReplaceToAttribute(UnityEditor.MenuCommand command)
            => command.ReplaceCommandContext<Image, XericUIrAttributeText, Sprite>(
                o => o.sprite,
                (t, d) => { t.sprite = d; });

        [UnityEditor.MenuItem("CONTEXT/Image/Replace To/Unity Image", true)]
        private static bool ValidateReplaceToUnity(UnityEditor.MenuCommand command)
            => command.context is XericUIrAttributeText;

        [UnityEditor.MenuItem("CONTEXT/Image/Replace To/Unity Image", false, 100)]
        private static void ReplaceToUnity(UnityEditor.MenuCommand command)
            => command.ReplaceCommandContext<XericUIrAttributeText, Image, Sprite>(
                o => o.sprite,
                (t, d) => { t.sprite = d; });
#endif

        #endregion
    }
}
