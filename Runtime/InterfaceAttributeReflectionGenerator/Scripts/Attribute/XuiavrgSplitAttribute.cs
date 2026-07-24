using System;
using System.Collections.Generic;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 拆分属性标注
    /// 将一个复合类型（如 Vector2）拆分为多个子属性，分别绑定到不同的 UI 元素
    /// 
    /// 使用示例：
    /// [XuiavrgFieldEntryGroup("speed")]
    /// [XuiavrgSplit("x", "最小")]
    /// [XuiavrgSplit("y", "最大")]
    /// public Vector2 speedRange = new Vector2(0, 100);
    /// 
    /// 上述代码创建一个 "speed" 编组的 Entry，内部包含两个子绑定：
    /// speedRange.x 绑定到 UI 的"最小"输入，speedRange.y 绑定到"最大"输入
    /// 子绑定通过 XUIAvrgMDValue.BindingSlots 与预制体上的 UI 组件匹配
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public class XuiavrgSplitAttribute : Attribute, IXuiavrgAttribute
    {
        /// <summary>子属性路径（如 "x"、"y"），访问父值上的子字段/属性</summary>
        public readonly string PropertyPath;

        /// <summary>可选的显示标签</summary>
        public readonly string DisplayLabel;

        /// <summary>
        /// 创建拆分属性标记
        /// </summary>
        /// <param name="propertyPath">子属性路径（如 Vector2 的 "x" 或 "y"）</param>
        /// <param name="displayLabel">可选的显示标签（为空时自动推导）</param>
        public XuiavrgSplitAttribute(string propertyPath, string displayLabel = null)
        {
            PropertyPath = propertyPath;
            DisplayLabel = displayLabel;
        }

        public void BindMetaData(AvrgMemberMetaData metaData)
        {
            // 创建子元数据
            var subMetaData = new AvrgMemberMetaData
            {
                MemberName = $"{metaData.MemberName}.{PropertyPath}",
                PropertyPath = PropertyPath,
                LabelName = DisplayLabel ?? PropertyPath,
                TargetInstance = null, // 由运行时闭包解析
                Hide = metaData.Hide,
                Required = metaData.Required,
                FieldOrder = metaData.FieldOrder,
            };

            // 通过反射解析子属性的值类型
            if (metaData.ValueType != null)
            {
                var subType = ResolveSubPropertyType(metaData.ValueType, PropertyPath);
                if (subType != null)
                    subMetaData.ValueType = subType;
            }

            // 添加到父元数据的子元数据列表
            if (metaData.SubMetaDataList == null)
                metaData.SubMetaDataList = new List<AvrgMemberMetaData>();

            metaData.SubMetaDataList.Add(subMetaData);
        }

        /// <summary>
        /// 通过反射解析子属性的类型
        /// </summary>
        private static Type ResolveSubPropertyType(Type parentType, string propertyPath)
        {
            if (parentType == null || string.IsNullOrEmpty(propertyPath))
                return null;

            // 仅支持单层路径
            if (propertyPath.Contains('/'))
                return null;

            var field = parentType.GetField(propertyPath);
            if (field != null) return field.FieldType;

            var prop = parentType.GetProperty(propertyPath);
            if (prop != null) return prop.PropertyType;

            return null;
        }
    }
}
