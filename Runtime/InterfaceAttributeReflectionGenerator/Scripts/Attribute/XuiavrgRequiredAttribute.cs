using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 必填
    /// </summary>
    public class XuiavrgRequiredAttribute : Attribute, IXuiavrgAttribute
    {
        public void BindProperty(XuiavrgInspectorProperty property)
        {
            property.required = true;
            property.notEmpty = true;
        }
    }
}