using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 属性检查标记
    /// 替代旧的 Required/NotEmpty 标记，扩展范围验证、警告、无效值检查
    /// 
    /// 使用示例：
    /// [XuiavrgPropertyCheck(Required = true)] public string name;
    /// [XuiavrgPropertyCheck(Required = true, ValidateReasonable = true)] public float speed;
    /// [XuiavrgPropertyCheck(CheckKey = "MyCustomKey", ValidateWarning = true)] public int score;
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class XuiavrgPropertyCheckAttribute : Attribute, IXuiavrgAttribute
    {
        /// <summary>
        /// 判断依据标识
        /// 默认使用类型全称（如 "System.Single"）与 AvrgValueRangeConfig 中的条目匹配
        /// 也可自定义其他名称
        /// </summary>
        public string CheckKey { get; set; }

        /// <summary>是否必填</summary>
        public bool Required { get; set; }

        /// <summary>是否启用合理值范围检查</summary>
        public bool ValidateReasonable { get; set; }

        /// <summary>是否启用警告状态检查</summary>
        public bool ValidateWarning { get; set; }

        /// <summary>是否启用无效值状态检查</summary>
        public bool ValidateInvalid { get; set; }

        public XuiavrgPropertyCheckAttribute()
        {
        }

        /// <summary>
        /// 创建必填检查标记
        /// </summary>
        public XuiavrgPropertyCheckAttribute(bool required)
        {
            Required = required;
        }

        public void BindMetaData(AvrgMemberMetaData metaData)
        {
            metaData.Required = Required;

            // 如果指定了 CheckKey，使用它；否则保持元数据默认（元数据默认用 Type.FullName）
            if (!string.IsNullOrEmpty(CheckKey))
                metaData.PropertyCheckKey = CheckKey;

            metaData.ValidateReasonable = ValidateReasonable;
            metaData.ValidateWarning = ValidateWarning;
            metaData.ValidateInvalid = ValidateInvalid;
        }
    }
}
