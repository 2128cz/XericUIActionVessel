using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 专用 UI 特性抽象基类
    /// 每个 UI 组件类型（Dropdown/Slider/InputField/Button）都有自己的专用 Attribute，
    /// 继承此类即可自动设置 Tag 并携带 UI 相关配置。
    /// 
    /// UI 侧查询方式：metaData.Attributes.OfType<T>() 获取专用配置
    /// 
    /// 使用示例：
    /// [XuiavrgFieldAsDropdown("选项A", "选项B", "选项C")]
    /// public float myOption;
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public abstract class XuiavrgFieldAsAttribute : Attribute, IXuiavrgAttribute
    {
        /// <summary>子类指定自己的功能标记</summary>
        public abstract XuiavrgFieldTag TargetTag { get; }

        /// <summary>
        /// 绑定元数据：自动设置 Tag
        /// 子类重写此方法时应在开头调用 base.BindMetaData(metaData)
        /// </summary>
        public virtual void BindMetaData(AvrgMemberMetaData metaData)
        {
            metaData.Tag = TargetTag;
            // 专用特性自动被收集到 metaData.Attributes 中
            // UI 侧通过 metaData.Attributes.OfType<T>() 查询配置
        }
    }
}
