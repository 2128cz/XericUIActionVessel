using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 字段功能标记特性
    /// 单独设置字段的功能标记，与编组分离
    /// 
    /// 使用示例：
    /// [XuiavrgFieldEntryGroup("field1")]
    /// [XuiavrgFieldTag(XuiavrgFieldTag.Label)]
    /// public string fieldLabel = "速度";
    /// 
    /// [XuiavrgFieldEntryGroup("field1")]
    /// [XuiavrgFieldTag(XuiavrgFieldTag.Context)]
    /// public float fieldValue = 0.1f;
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class XuiavrgFieldTagAttribute : Attribute, IXuiavrgAttribute
    {
        /// <summary>功能标记</summary>
        public readonly XuiavrgFieldTag Tag;

        /// <summary>同一 Tag 存在多个时的索引区分</summary>
        public readonly int TagIndex;

        /// <summary>
        /// 创建字段功能标记
        /// </summary>
        /// <param name="tag">功能标记</param>
        /// <param name="tagIndex">标记索引（同一 Tag 存在多个时区分）</param>
        public XuiavrgFieldTagAttribute(XuiavrgFieldTag tag, int tagIndex = 0)
        {
            Tag = tag;
            TagIndex = tagIndex;
        }

        public void BindMetaData(AvrgMemberMetaData metaData)
        {
            metaData.Tag = Tag;
            metaData.TagIndex = TagIndex;
        }
    }
}
