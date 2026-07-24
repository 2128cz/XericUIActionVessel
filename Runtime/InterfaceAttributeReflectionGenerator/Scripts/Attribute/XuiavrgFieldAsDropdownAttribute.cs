using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 字段作为下拉框（Dropdown）的专用特性
    /// 自动设置 Tag = Dropdown，并携带默认选项列表
    /// 
    /// 使用示例：
    /// [XuiavrgFieldAsDropdown("选项A", "选项B", "选项C")]
    /// public float mode;
    /// 
    /// [XuiavrgFieldAsDropdown(1, 2, 3)]  // 也支持数字
    /// public int intMode;
    /// 
    /// UI 侧查询方式：
    /// metaData.Attributes.OfType<XuiavrgFieldAsDropdownAttribute>()
    ///     .FirstOrDefault()?.DefaultValues
    /// </summary>
    public class XuiavrgFieldAsDropdownAttribute : XuiavrgFieldAsAttribute
    {
        /// <summary>默认选项列表</summary>
        public object[] DefaultValues { get; }

        public override XuiavrgFieldTag TargetTag => XuiavrgFieldTag.Dropdown;

        /// <summary>
        /// 创建下拉框专用特性
        /// </summary>
        /// <param name="defaultValues">默认选项值列表（params 参数，支持任意编译期常量类型）</param>
        public XuiavrgFieldAsDropdownAttribute(params object[] defaultValues)
        {
            DefaultValues = defaultValues ?? Array.Empty<object>();
        }
    }
}
