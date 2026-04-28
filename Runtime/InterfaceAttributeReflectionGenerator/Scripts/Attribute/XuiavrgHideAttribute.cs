using System;
using UnityEngine.Serialization;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 隐藏标记
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class XuiavrgHideAttribute : Attribute, IXuiavrgAttribute
    {
        public readonly bool Hide;
        
        /// <summary>
        /// 标记属性
        /// </summary>
        /// <param name="hideInInspector">隐藏属性</param>
        public XuiavrgHideAttribute(bool hideInInspector = true)
        {
            Hide = hideInInspector;
        }

        public void BindProperty(XuiavrgInspectorProperty property)
        {
            property.hideInInspector = Hide;
        }
    }
}