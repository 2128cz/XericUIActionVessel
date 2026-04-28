using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 不可为空
    /// </summary>
    public class XuiavrgNotEmptyAttribute : Attribute, IXuiavrgAttribute
    {
        public void BindProperty(XuiavrgInspectorProperty property)
        {
            property.notEmpty = true;
        }
    }
}