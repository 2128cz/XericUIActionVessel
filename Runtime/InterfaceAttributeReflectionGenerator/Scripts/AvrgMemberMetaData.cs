using System;
using System.Collections.Generic;
using System.Reflection;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 单个成员的元数据数据结构
    /// 记录从反射 + Attribute 标记中提取的所有信息
    /// </summary>
    public class AvrgMemberMetaData
    {
        // ============ 反射信息 ============

        /// <summary>字段/属性名</summary>
        public string MemberName { get; set; }

        /// <summary>反射信息 (FieldInfo 或 PropertyInfo)</summary>
        public MemberInfo MemberInfo { get; set; }

        /// <summary>值类型</summary>
        public Type ValueType { get; set; }

        /// <summary>是否为属性 (false 为字段)</summary>
        public bool IsProperty { get; set; }

        /// <summary>绑定的目标实例 (可选，Step1 时可空)</summary>
        public object TargetInstance { get; set; }

        // ============ 从 IXuiavrgAttribute 融合的元数据 ============

        /// <summary>显示标签</summary>
        public string LabelName { get; set; }

        /// <summary>单位文本</summary>
        public string UnitLabelName { get; set; }

        /// <summary>功能标记</summary>
        public XuiavrgFieldTag Tag { get; set; } = XuiavrgFieldTag.None;

        /// <summary>标记索引 (同 Tag 多实例时区分)</summary>
        public int TagIndex { get; set; }

        /// <summary>排序值 (越小越靠前)</summary>
        public int FieldOrder { get; set; }

        /// <summary>编组路径 (以 "/" 分割层级)</summary>
        public string GroupPath { get; set; }

        /// <summary>预制体覆盖名称</summary>
        public string PrefabOverrideName { get; set; }

        /// <summary>是否在 UI 中隐藏</summary>
        public bool Hide { get; set; }

        /// <summary>值变更时触发的反射调用方法名</summary>
        public string OnTriggerCall { get; set; }

        /// <summary>
        /// 类的初始默认值（由类字段初始化器决定）
        /// 例如 public float speed = 10f; → DefaultValue = 10f
        /// </summary>
        public object DefaultValue { get; set; }

        // ============ 属性检查 (由 [PropertyCheck] 填充) ============

        /// <summary>
        /// 判断依据标识
        /// 默认使用 ValueType.FullName 与 AvrgValueRangeConfig 中的条目匹配
        /// </summary>
        public string PropertyCheckKey { get; set; }

        /// <summary>是否必填</summary>
        public bool Required { get; set; }

        /// <summary>是否不可为空</summary>
        public bool NotEmpty { get; set; }

        /// <summary>是否启用合理值范围检查</summary>
        public bool ValidateReasonable { get; set; }

        /// <summary>是否启用警告状态检查</summary>
        public bool ValidateWarning { get; set; }

        /// <summary>是否启用无效值状态检查</summary>
        public bool ValidateInvalid { get; set; }

        // ============ (已弃用字段保留) 旧的验证约束 ============

        /// <summary>字符串最小长度</summary>
        [Obsolete("将移至 ValueRangeConfig 管理")]
        public int StringMinLength { get; set; }

        /// <summary>字符串最大长度</summary>
        public int StringMaxLength { get; set; } = int.MaxValue;

        /// <summary>数值最小值</summary>
        [Obsolete("将移至 ValueRangeConfig 管理")]
        public float NumberMinValue { get; set; }

        /// <summary>数值最大值</summary>
        [Obsolete("将移至 ValueRangeConfig 管理")]
        public float NumberMaxValue { get; set; }

        // ============ 原始 Attribute 引用 ============

        /// <summary>
        /// 收集到的所有 IXuiavrgAttribute 列表
        /// 保留原始引用，供扩展和自定义处理使用
        /// </summary>
        public List<IXuiavrgAttribute> Attributes { get; set; } = new List<IXuiavrgAttribute>();

        /// <summary>
        /// 从绑定目标获取值
        /// </summary>
        public object GetValue()
        {
            if (TargetInstance == null) return null;
            switch (MemberInfo)
            {
                case PropertyInfo pi:
                    return pi.GetValue(TargetInstance);
                case FieldInfo fi:
                    return fi.GetValue(TargetInstance);
                default:
                    return null;
            }
        }

        /// <summary>
        /// 设置值到绑定目标
        /// </summary>
        public void SetValue(object value)
        {
            if (TargetInstance == null) return;
            switch (MemberInfo)
            {
                case PropertyInfo pi:
                    if (pi.CanWrite)
                        pi.SetValue(TargetInstance, value);
                    break;
                case FieldInfo fi:
                    fi.SetValue(TargetInstance, value);
                    break;
            }
        }
    }
}
