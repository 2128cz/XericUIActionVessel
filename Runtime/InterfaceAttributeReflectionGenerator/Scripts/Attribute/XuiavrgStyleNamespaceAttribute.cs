using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 样式命名空间特性 — 为元数据指定 SuperStyleSheet 查询时的命名空间。
    /// 
    /// 可标记在类上（作为该类型所有字段的默认命名空间），
    /// 也可标记在字段/属性上（覆盖单个字段的命名空间）。
    /// 
    /// 类级标记先应用（设置默认值），字段级标记后应用（可覆盖），
    /// 因此字段级 [XuiavrgStyleNamespace] 优先于类级。
    /// 
    /// 默认命名空间为 "default"。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class XuiavrgStyleNamespaceAttribute : Attribute, IXuiavrgAttribute
    {
        public readonly string StyleNamespace;

        /// <param name="styleNamespace">SuperStyleSheet 命名空间名称（如 "my-theme"）</param>
        public XuiavrgStyleNamespaceAttribute(string styleNamespace)
        {
            StyleNamespace = styleNamespace;
        }

        public void BindMetaData(AvrgMemberMetaData metaData)
        {
            if (metaData != null)
                metaData.StyleNamespace = StyleNamespace;
        }
    }
}
