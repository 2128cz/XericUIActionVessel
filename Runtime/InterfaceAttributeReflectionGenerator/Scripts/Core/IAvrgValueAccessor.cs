using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 预设选项合并模式
    /// 用于 Dropdown/ToggleGroup 的预设选项与已有选项（如 enum 名）的合并策略
    /// </summary>
    public enum PresetOptionsMergeMode
    {
        /// <summary>完全覆盖现有选项</summary>
        Replace,

        /// <summary>保留两者交集</summary>
        Intersection,

        /// <summary>保留两者并集（去重合并）</summary>
        Union,

        /// <summary>从现有选项中移除预设选项（差集）</summary>
        Difference,
    }

    /// <summary>
    /// 值访问器 — 统一的数据读写通道
    /// 解耦"数据的物理存储方式"和"业务层的读写需求"
    /// 
    /// C# 反射对象和 Material Shader 属性通过各自实现此接口，
    /// 向 MVVM 绑定层暴露完全相同的数据交互约定。
    /// </summary>
    public interface IAvrgValueAccessor
    {
        /// <summary>值的 C# 类型</summary>
        Type ValueType { get; }

        /// <summary>是否只读（如 Shader Keywords 不可运行时修改）</summary>
        bool IsReadOnly { get; }

        /// <summary>从数据源读取当前值</summary>
        object GetValue();

        /// <summary>将值写入数据源（IsReadOnly 时忽略）</summary>
        void SetValue(object value);

        /// <summary>将值格式化为 UI 显示字符串</summary>
        string FormatDisplay(object value);
    }

    /// <summary>
    /// 值访问器工厂 — 在"元数据模板"阶段创建，延迟到"绑定实例"时生产 Accessor
    /// 
    /// 设计意图：
    /// 1. 元数据提取阶段：只描述"如何访问数据"，不绑定具体实例
    /// 2. BuildMDVM 阶段：传入实例，通过 Factory 生产 Accessor
    /// 3. 同一套元数据模板可复用于不同实例
    /// </summary>
    public interface IAvrgValueAccessorFactory
    {
        /// <summary>为给定目标实例创建值访问器</summary>
        /// <param name="targetInstance">目标实例（C# 对象、Material 等）</param>
        /// <returns>值访问器实例，targetInstance 类型不匹配时返回 null</returns>
        IAvrgValueAccessor CreateAccessor(object targetInstance);
    }
}
