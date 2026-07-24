using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 字段作为输入框（InputField）的专用特性
    /// 自动设置 Tag = Input，并携带占位文本
    /// 
    /// 使用示例：
    /// [XuiavrgFieldAsInputField("请输入名称")]
    /// public string name;
    /// 
    /// [XuiavrgFieldAsInputField("输入数字")]
    /// public float score;
    /// 
    /// UI 侧查询方式：
    /// metaData.Attributes.OfType<XuiavrgFieldAsInputFieldAttribute>()
    ///     .FirstOrDefault()?.PlaceholderText
    /// </summary>
    public class XuiavrgFieldAsInputFieldAttribute : XuiavrgFieldAsAttribute
    {
        /// <summary>输入框占位文本（无输入时显示的灰色提示）</summary>
        public string PlaceholderText { get; }

        public override XuiavrgFieldTag TargetTag => XuiavrgFieldTag.Input;

        /// <summary>
        /// 创建输入框专用特性
        /// </summary>
        /// <param name="placeholderText">占位文本（无输入时的提示）</param>
        public XuiavrgFieldAsInputFieldAttribute(string placeholderText)
        {
            PlaceholderText = placeholderText ?? "";
        }
    }
}
