using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 数值检查
    /// </summary>
    public class XuiavrgNumberCheckAttribute : Attribute, IXuiavrgAttribute
    {
        public readonly float minValue;
        public readonly float maxValue;
        
        public XuiavrgNumberCheckAttribute(float minValue, float maxValue)
        {
            this.minValue = minValue;
            this.maxValue = maxValue;
        }
        
        public void BindProperty(XuiavrgInspectorProperty property)
        {
            property.numberMinValue = minValue; 
            property.numberMaxValue = maxValue; 
        }
    }
}