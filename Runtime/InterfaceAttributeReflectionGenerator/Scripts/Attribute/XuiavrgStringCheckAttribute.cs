using System;
using UnityEngine;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 字符串检查
    /// </summary>
    public class XuiavrgStringCheckAttribute : Attribute, IXuiavrgAttribute
    {
        public readonly int minLength;
        public readonly int maxLength;
        public readonly string regexPattern;
        public readonly bool onlyLetters;
        public readonly bool onlyNumber;
        public readonly bool onlyEmail;
        public readonly bool onlyPhone;
        public readonly bool onlyUrl;

        public XuiavrgStringCheckAttribute(
            int minLength = 0, int maxLength = int.MaxValue,
            string regexPattern = "",
            bool onlyLetters = false, bool onlyNumber = false, bool onlyEmail = true, bool onlyPhone = true,
            bool onlyUrl = true)
        {
            this.minLength = minLength;
            this.maxLength = maxLength;
            this.regexPattern = regexPattern;
            this.onlyLetters = onlyLetters;
            this.onlyNumber = onlyNumber;
            this.onlyEmail = onlyEmail;
            this.onlyPhone = onlyPhone;
            this.onlyUrl = onlyUrl;
        }

        public void BindProperty(XuiavrgInspectorProperty property)
        {
            property.stringMinLength = minLength;
            property.stringMaxLength = maxLength;
            property.stringRegexPattern = regexPattern;
            property.stringOnlyLetters = onlyLetters;
            property.stringOnlyNumber = onlyNumber;
            property.stringOnlyEmail = onlyEmail;
            property.stringOnlyPhone = onlyPhone;
            property.stringOnlyUrl = onlyUrl;
        }
    }
}