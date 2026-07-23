using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 字段编组特性
    /// 将多个字段标记为属于同一个 Entry，共享一个 UI 组件实例
    /// 
    /// 使用示例：
    /// [XuiavrgFieldEntryGroup("field1", XuiavrgFieldTag.Label)] public string fieldLabel = "速度";
    /// [XuiavrgFieldEntryGroup("field1", XuiavrgFieldTag.Context)] public float fieldValue = 0.1f;
    /// [XuiavrgFieldEntryGroup("field1", XuiavrgFieldTag.Unit)] public string fieldUnit = "m/s";
    /// 
    /// 上述三个字段将合并为一个 Entry，分别绑定到 UI 组件上 Label、Context、Unit 三个子元素
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class XuiavrgFieldEntryGroupAttribute : Attribute, IXuiavrgAttribute
    {
        /// <summary>编组路径，以 "/" 分割层级。同一 groupPath 的字段合并到一个 Entry</summary>
        public readonly string GroupPath;
        
        /// <summary>该字段在编组 Entry 中的功能标记</summary>
        public readonly XuiavrgFieldTag Tag;
        
        /// <summary>同一 Tag 存在多个时的索引区分</summary>
        public readonly int TagIndex;

        /// <summary>
        /// 创建字段编组标记
        /// </summary>
        /// <param name="groupPath">编组路径 (如 "field1" 或 "field1/child")</param>
        /// <param name="tag">功能标记</param>
        /// <param name="tagIndex">标记索引 (同一 Tag 存在多个时区分)</param>
        public XuiavrgFieldEntryGroupAttribute(string groupPath, XuiavrgFieldTag tag, int tagIndex = 0)
        {
            GroupPath = groupPath;
            Tag = tag;
            TagIndex = tagIndex;
        }

        public void BindMetaData(AvrgMemberMetaData metaData)
        {
            metaData.GroupPath = GroupPath;
            metaData.Tag = Tag;
            metaData.TagIndex = TagIndex;
        }
    }
}
