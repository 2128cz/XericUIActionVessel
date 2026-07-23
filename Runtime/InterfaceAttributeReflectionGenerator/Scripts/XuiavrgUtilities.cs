using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace XericUI.ReflectionUIGenerator
{
    public static class XuiavrgUtilities
    {
        /// <summary>
        /// 获取所有运行时成员（字段和属性）
        /// </summary>
        public static IEnumerable<MemberInfo> GetRuntimeMemberInfo(this Type type)
        {
            foreach (var field in type.GetRuntimeFields())
                yield return field;
            foreach (var property in type.GetRuntimeProperties())
                yield return property;
        }

        /// <summary>
        /// 从任意对象提取 AvrgMemberMetaData 元数据集
        /// 反射识别成员、识别 IXuiavrgAttribute 标记、执行标记绑定的完整流程
        /// </summary>
        /// <param name="source">待提取的数据对象</param>
        /// <returns>提取完成的元数据集</returns>
        public static AvrgMemberMetaDataSet ExtractAvrgMemberMetaData(this object source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var metaDataSet = new AvrgMemberMetaDataSet();
            var type = source.GetType();

            foreach (var memberInfo in type.GetRuntimeMemberInfo())
            {
                // 检查隐藏标记
                var hideAttr = memberInfo.GetCustomAttribute<XuiavrgHideAttribute>();
                if (hideAttr != null && hideAttr.Hide)
                    continue;

                // 创建元数据
                var metaData = new AvrgMemberMetaData
                {
                    MemberName = memberInfo.Name,
                    MemberInfo = memberInfo,
                    TargetInstance = source,
                };

                // 识别值类型和 IsProperty
                switch (memberInfo)
                {
                    case PropertyInfo pi:
                        metaData.IsProperty = true;
                        metaData.ValueType = pi.PropertyType;
                        break;
                    case FieldInfo fi:
                        metaData.IsProperty = false;
                        metaData.ValueType = fi.FieldType;
                        break;
                }

                // 提取字段默认值（类初始值）
                // Unity 序列化字段可以通过反射读取实例上的当前值作为"默认"参考
                // 但更准确的在类声明阶段的初始值需要通过运行时观察或 Fallback 判断
                // 此处策略：如果 TargetInstance 非 null，直接 ReadValue 作为 DefaultValue
                if (source != null && memberInfo is FieldInfo fi2)
                {
                    metaData.DefaultValue = fi2.GetValue(source);
                }
                else if (source != null && memberInfo is PropertyInfo pi2 && pi2.CanRead)
                {
                    metaData.DefaultValue = pi2.GetValue(source);
                }

                // 设置 PropertyCheckKey 默认值（用类型全称）
                if (metaData.ValueType != null)
                    metaData.PropertyCheckKey = metaData.ValueType.FullName;

                // 收集所有 IXuiavrgAttribute
                var attributes = memberInfo.GetCustomAttributes()
                    .OfType<IXuiavrgAttribute>()
                    .ToList();

                metaData.Attributes = attributes;

                // 按序执行 BindMetaData
                foreach (var attr in attributes)
                {
                    attr.BindMetaData(metaData);
                }

                metaDataSet.Add(metaData);
            }

            // 处理编组合并
            metaDataSet.MergeGroupEntries();

            // 排序
            metaDataSet.SortByFieldOrder();

            return metaDataSet;
        }

        /// <summary>
        /// 从任意对象提取 AvrgMemberMetaData 元数据集的泛型版本
        /// </summary>
        public static AvrgMemberMetaDataSet ExtractAvrgMemberMetaData<T>(this T source)
        {
            return ExtractAvrgMemberMetaData((object)source);
        }
    }
}
