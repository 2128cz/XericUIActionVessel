using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 为 ToggleGroup UI 提供预设选项
    /// 
    /// 使用示例：
    /// [XuiavrgFieldToggleGroupValue("低", "中", "高", MergeMode = PresetOptionsMergeMode.Replace)]
    /// public string detailLevel = "中";
    /// 
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class XuiavrgFieldToggleGroupValueAttribute : Attribute, IXuiavrgAttribute
    {
        /// <summary>预设选项数组</summary>
        public readonly string[] Options;

        /// <summary>与已有选项（如 enum 名）的合并模式</summary>
        public PresetOptionsMergeMode MergeMode { get; set; } = PresetOptionsMergeMode.Replace;

        public XuiavrgFieldToggleGroupValueAttribute(params string[] options)
        {
            Options = options ?? Array.Empty<string>();
        }
        public XuiavrgFieldToggleGroupValueAttribute(PresetOptionsMergeMode mergeMode, params string[] options)
        {
            MergeMode = mergeMode;
            Options = options ?? Array.Empty<string>();
        }

        public void BindMetaData(AvrgMemberMetaData metaData)
        {
            if (metaData == null) return;

            metaData.PresetOptions = Options;
            metaData.OptionsMergeMode = MergeMode;

            // 自动设置 Tag 为 ToggleGroup
            if (metaData.Tag == XuiavrgFieldTag.None)
                metaData.Tag = XuiavrgFieldTag.ToggleGroup;
        }
    }
}
