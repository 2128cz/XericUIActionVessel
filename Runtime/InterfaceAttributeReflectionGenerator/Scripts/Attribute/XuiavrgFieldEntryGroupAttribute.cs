using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 字段编组特性
    /// 将多个字段标记为属于同一个编组路径，共享一个 UI 组件实例
    /// 
    /// 编组不携带 Tag，Tag 由 [XuiavrgFieldTag] 单独标记。
    /// 使用示例：
    /// [XuiavrgFieldEntryGroup("field1")]
    /// [XuiavrgFieldTag(XuiavrgFieldTag.Label)]
    /// public string fieldLabel = "速度";
    /// 
    /// [XuiavrgFieldEntryGroup("field1")]
    /// [XuiavrgFieldTag(XuiavrgFieldTag.Context)]
    /// public float fieldValue = 0.1f;
    /// 
    /// [XuiavrgFieldEntryGroup("field1")]
    /// [XuiavrgFieldTag(XuiavrgFieldTag.Unit)]
    /// public string fieldUnit = "m/s";
    /// 
    /// 上述三个字段将合并为一个 Entry，分别绑定到 UI 组件上 Label、Context、Unit 三个子元素
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class XuiavrgFieldEntryGroupAttribute : Attribute, IXuiavrgAttribute
    {
        /// <summary>编组路径，以 "/" 分割层级。同一 groupPath 的字段合并到一个 Entry</summary>
        public readonly string GroupPath;

        /// <summary>
        /// 创建字段编组标记
        /// </summary>
        /// <param name="groupPath">编组路径 (如 "field1" 或 "Stats/Speed")</param>
        public XuiavrgFieldEntryGroupAttribute(string groupPath)
        {
            GroupPath = groupPath;
        }

        public void BindMetaData(AvrgMemberMetaData metaData)
        {
            metaData.GroupPath = GroupPath;
            // 不设置 Tag/TagIndex，留给 [XuiavrgFieldTag] 单独处理
        }
    }
}
