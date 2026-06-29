using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XericLibrary.Runtime.SuperStyleSheet;

namespace XericUI.XTable.Rendering.Elements
{
    /// <summary>
    /// 样式绑定元素——将 Image 或 TMP_Text 挂勾到样式表路径。
    /// 自动从 StyleManager 拉取值并应用，样式变更时自动刷新。
    /// 通过对象池复用时调用 Bind/Unbind 切换绑定目标。
    /// </summary>
    public class StyleBoundElement
    {
        #region 字段

        /// <summary>绑定的 Image 组件（可能为 null）</summary>
        public Image ImageComponent;

        /// <summary>绑定的 TMP_Text 组件（可能为 null）</summary>
        public TMP_Text TextComponent;

        /// <summary>样式命名空间</summary>
        private string m_StyleNamespace;

        /// <summary>各路径的 StyleField 缓存</summary>
        private System.Collections.Generic.Dictionary<string, StyleField> m_Fields
            = new System.Collections.Generic.Dictionary<string, StyleField>();

        /// <summary>是否已绑定</summary>
        private bool m_IsBound;

        /// <summary>样式变更时触发（通知表格重绘）</summary>
        public event System.Action OnStyleChangedCallback;

        /// <summary>当前绑定的命名空间（空字符串表示未绑定）</summary>
        public string BoundNamespace => m_IsBound ? (m_StyleNamespace ?? "") : "";

        #endregion

        #region 公开方法

        /// <summary>
        /// 绑定到指定命名空间并开始监听样式。
        /// 运行时注册 StyleField 实现被动更新（push）；编辑器下在渲染时直接 pull。
        /// </summary>
        public void Bind(string styleNamespace)
        {
            if (m_IsBound) Unbind();
            m_StyleNamespace = styleNamespace;

            if (string.IsNullOrEmpty(styleNamespace)) return;

            // 检查命名空间是否存在，避免 Register → GetValue 报错
            var namespaces = StyleManager.GetAvailableNamespaces();
            if (namespaces == null || !namespaces.Contains(styleNamespace))
                return;

            // 预注册常用路径
            RegisterField("/cell/bg/color", OnStyleChanged);
            RegisterField("/cell/fg/color", OnStyleChanged);
            RegisterField("/cell/border/top/color", OnStyleChanged);
            RegisterField("/cell/border/bottom/color", OnStyleChanged);
            RegisterField("/cell/border/left/color", OnStyleChanged);
            RegisterField("/cell/border/right/color", OnStyleChanged);
            RegisterField("/cell/font/size", OnStyleChanged);
            RegisterField("/cell/font/alignment", OnStyleChanged);
            RegisterField("/cell/font/richText", OnStyleChanged);
            RegisterField("/cell/font/fontAsset", OnStyleChanged);

            m_IsBound = true;
            RefreshAll();
        }

        /// <summary>
        /// 解绑并停止监听样式。
        /// </summary>
        public void Unbind()
        {
            if (!m_IsBound) return;

            foreach (var kvp in m_Fields)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.Member.OnValueChanged -= OnStyleChanged;
                    kvp.Value.Unregister();
                }
            }

            m_Fields.Clear();
            m_IsBound = false;
        }

        /// <summary>
        /// 刷新全部样式值到组件。
        /// </summary>
        public void RefreshAll()
        {
            if (!m_IsBound || string.IsNullOrEmpty(m_StyleNamespace)) return;

            // 背景颜色 → Image.color
            StyleValue bgVal = GetStyleValue("/cell/bg/color");
            if (bgVal != null && ImageComponent != null)
                ImageComponent.color = bgVal;

            // 前景颜色 → Text.color
            StyleValue fgVal = GetStyleValue("/cell/fg/color");
            if (fgVal != null && TextComponent != null)
                TextComponent.color = fgVal;

            // 字体大小
            StyleValue sizeVal = GetStyleValue("/cell/font/size");
            if (sizeVal != null && TextComponent != null)
                TextComponent.fontSize = (int)sizeVal;

            // 对齐
            StyleValue alignVal = GetStyleValue("/cell/font/alignment");
            if (alignVal != null && TextComponent != null)
                TextComponent.alignment = alignVal.Convert<TextAlignmentOptions>();

            // 富文本
            StyleValue richVal = GetStyleValue("/cell/font/richText");
            if (richVal != null && TextComponent != null)
                TextComponent.richText = (bool)richVal;

            // 字体资源
            StyleValue fontVal = GetStyleValue("/cell/font/fontAsset");
            if (fontVal != null && TextComponent != null)
            {
                try
                {
                    TextComponent.font = fontVal.GetValueAs<TMP_FontAsset>();
                }
                catch
                {
                    /* 类型不匹配时忽略 */
                }
            }
        }

        #endregion

        #region 私有方法

        private void RegisterField(string path, System.Action<StyleValue> callback)
        {
            var field = new StyleField
            {
                Namespace = m_StyleNamespace,
                StylePath = path
            };
            field.Register();
            field.Member.OnValueChanged += callback;
            m_Fields[path] = field;
        }

        private StyleValue GetStyleValue(string path)
        {
            return StyleManager.GetValue(m_StyleNamespace, path);
        }

        private void OnStyleChanged(StyleValue val)
        {
            RefreshAll();
            OnStyleChangedCallback?.Invoke();
        }

        #endregion
    }

    /// <summary>
    /// 用于 TextAnchor → TextAlignmentOptions 的转换扩展。
    /// </summary>
    internal static class StyleBoundElementExtensions
    {
        public static TextAlignmentOptions Convert<T>(this StyleValue val)
        {
            string s = val?.GetValueAs<string>();
            if (string.IsNullOrEmpty(s)) return TextAlignmentOptions.Midline;

            switch (s)
            {
                case "UpperLeft": return TextAlignmentOptions.TopLeft;
                case "UpperCenter": return TextAlignmentOptions.Top;
                case "UpperRight": return TextAlignmentOptions.TopRight;
                case "MiddleLeft": return TextAlignmentOptions.MidlineLeft;
                case "MiddleCenter": return TextAlignmentOptions.Midline;
                case "MiddleRight": return TextAlignmentOptions.MidlineRight;
                case "LowerLeft": return TextAlignmentOptions.BottomLeft;
                case "LowerCenter": return TextAlignmentOptions.Bottom;
                case "LowerRight": return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Midline;
            }
        }
    }
}