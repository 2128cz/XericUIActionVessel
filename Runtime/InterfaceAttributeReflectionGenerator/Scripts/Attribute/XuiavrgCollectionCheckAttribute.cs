using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 集合检查
    /// </summary>
    public class XuiavrgCollectionCheckAttribute : Attribute, IXuiavrgAttribute
    {
        public readonly int minCount;
        public readonly int maxCount;
        public readonly bool notEmpty;

        public XuiavrgCollectionCheckAttribute(int minCount, int maxCount, bool notEmpty)
        {
            this.minCount = minCount;
            this.maxCount = maxCount;
            this.notEmpty = notEmpty;
        }

        public void BindProperty(XuiavrgInspectorProperty property)
        {
            property.collectionMinCount = minCount;
            property.collectionMaxCount = maxCount;
            property.collectionNotEmpty = notEmpty;
        }
    }
}