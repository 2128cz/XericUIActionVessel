using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 为 Dropdown UI 提供预设选项
    /// 
    /// 使用示例：
    /// [XuiavrgFieldDropdownValue("选项A", "选项B", "选项C", MergeMode = PresetOptionsMergeMode.Union)]
    /// public string quality = "选项A";
    /// 
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class XuiavrgFieldDropdownValueAttribute : Attribute, IXuiavrgAttribute
    {
        /// <summary>预设选项数组</summary>
        public readonly string[] Options;

        /// <summary>与已有选项（如 enum 名）的合并模式</summary>
        public PresetOptionsMergeMode MergeMode { get; set; } = PresetOptionsMergeMode.Replace;

        public XuiavrgFieldDropdownValueAttribute(params string[] options)
        {
            Options = options ?? Array.Empty<string>();
        }

        public void BindMetaData(AvrgMemberMetaData metaData)
        {
            if (metaData == null) return;

            metaData.PresetOptions = Options;
            metaData.OptionsMergeMode = MergeMode;

            // 自动设置 Tag 为 Dropdown
            if (metaData.Tag == XuiavrgFieldTag.None)
                metaData.Tag = XuiavrgFieldTag.Dropdown;
        }
    }
}
