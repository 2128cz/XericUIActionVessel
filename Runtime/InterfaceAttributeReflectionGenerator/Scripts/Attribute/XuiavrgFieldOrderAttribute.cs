using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 字段排序特性
    /// 控制 Entry 在界面中的显示顺序，替代原有 XuiavrgAttribute.InspectorOrder
    /// 
    /// 使用示例：
    /// [XuiavrgFieldOrder(5)] public string someField;
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class XuiavrgFieldOrderAttribute : Attribute, IXuiavrgAttribute
    {
        /// <summary>排序值，值越小越靠前。0 表示使用默认发现顺序</summary>
        public readonly int Order;

        /// <summary>
        /// 创建排序标记
        /// </summary>
        /// <param name="order">排序值</param>
        public XuiavrgFieldOrderAttribute(int order)
        {
            Order = order;
        }

        public void BindProperty(XuiavrgInspectorProperty property)
        {
            property.inspectorOrder = Order;
        }
    }
}
