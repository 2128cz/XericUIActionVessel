using System;
using System.Collections.Generic;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 单个成员的元数据数据结构
    /// 纯数据描述 — 不持有反射信息（MemberInfo）和实例引用（TargetInstance）
    /// 
    /// 数据的实际读写通过 IAvrgValueAccessorFactory 在 BuildMDVM 阶段创建的 IAvrgValueAccessor 完成。
    /// 这使得同一个元数据模板可复用于 C# 反射对象、Material Shader 属性等不同数据源。
    /// </summary>
    public class AvrgMemberMetaData
    {
        // ============ 基本标识 ============

        /// <summary>字段/属性名（或 Shader 属性名如 "_MainTex"）</summary>
        public string MemberName { get; set; }

        /// <summary>UI 显示标签</summary>
        public string LabelName { get; set; }

        /// <summary>单位文本（如 "m/s"）</summary>
        public string UnitLabelName { get; set; }

        /// <summary>格式化字符串（如 "{0:F2}"）</summary>
        public string FormatText { get; set; }

        /// <summary>值类型</summary>
        public Type ValueType { get; set; }

        /// <summary>
        /// 样式表命名空间（用于 SuperStyleSheet 样式查询）
        /// 默认 "default"。可由类级或字段级 [XuiavrgStyleNamespace] 覆写
        /// </summary>
        public string StyleNamespace { get; set; } = "default";

        // ============ UI 映射 ============

        /// <summary>功能标记</summary>
        public XuiavrgFieldTag Tag { get; set; } = XuiavrgFieldTag.None;

        /// <summary>标记索引 (同 Tag 多实例时区分)</summary>
        public int TagIndex { get; set; }

        /// <summary>排序值 (越小越靠前)</summary>
        public int FieldOrder { get; set; }

        /// <summary>编组路径 (以 "/" 分割层级)</summary>
        public string GroupPath { get; set; }

        /// <summary>是否在 UI 中隐藏</summary>
        public bool Hide { get; set; }

        // ============ 默认值与验证 ============

        /// <summary>默认值（用于 UI 初始化和回退参考）</summary>
        public object DefaultValue { get; set; }

        /// <summary>属性检查依据标识（默认使用 ValueType.FullName）</summary>
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

        /// <summary>Shader Range 属性下限（null 表示非 Range 属性）</summary>
        public float? RangeMin { get; set; }

        /// <summary>Shader Range 属性上限（null 表示非 Range 属性）</summary>
        public float? RangeMax { get; set; }

        // ============ 拆分属性支持 ============

        /// <summary>
        /// 子属性路径（仅拆分属性有效）
        /// 用于从父值中访问子字段/属性，如 "x"、"y"、"width"
        /// </summary>
        public string PropertyPath { get; set; }

        /// <summary>
        /// 拆分子元数据列表
        /// 由 [XuiavrgSplit] 特性填充
        /// </summary>
        public List<AvrgMemberMetaData> SubMetaDataList { get; set; }

        // ============ 预设选项 (Dropdown/ToggleGroup) ============

        /// <summary>预设选项列表</summary>
        public IReadOnlyList<string> PresetOptions { get; set; }

        /// <summary>预设选项合并模式</summary>
        public PresetOptionsMergeMode OptionsMergeMode { get; set; }

        // ============ 扩展 ============

        /// <summary>收集到的所有 IXuiavrgAttribute 列表</summary>
        public List<IXuiavrgAttribute> Attributes { get; set; } = new List<IXuiavrgAttribute>();

        // ============ ★ 核心变更 — 值访问器工厂 ============

        /// <summary>
        /// 值访问器工厂
        /// 在元数据提取阶段设置（持有 MemberInfo 或 Shader 属性信息）
        /// 在 BuildMDVM 阶段调用 CreateAccessor(targetInstance) 创建真正的读写通道
        /// </summary>
        public IAvrgValueAccessorFactory AccessorFactory { get; set; }
    }
}
