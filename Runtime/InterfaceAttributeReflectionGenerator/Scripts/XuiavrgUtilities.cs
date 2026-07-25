using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

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

        // ============ C# 对象: 元数据提取（纯元数据，不绑定实例）============

        /// <summary>
        /// 从类型提取 AvrgMemberMetaData 元数据集（不含实例绑定）
        /// 用于构建元数据模板，可复用于不同实例
        /// </summary>
        /// <param name="type">待提取的类型</param>
        /// <returns>纯元数据集（无实例引用，由 AccessorFactory 持有反射信息）</returns>
        public static AvrgMemberMetaDataSet ExtractAvrgMemberMetaData(this Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            var metaDataSet = new AvrgMemberMetaDataSet();

            foreach (var memberInfo in type.GetRuntimeMemberInfo())
            {
                // 检查隐藏标记
                var hideAttr = memberInfo.GetCustomAttribute<XuiavrgHideAttribute>();
                if (hideAttr != null && hideAttr.Hide)
                    continue;

                // 识别值类型
                Type valueType = memberInfo switch
                {
                    PropertyInfo pi => pi.PropertyType,
                    FieldInfo fi => fi.FieldType,
                    _ => typeof(object)
                };

                // 创建元数据 — 不持有 MemberInfo 和 TargetInstance
                var metaData = new AvrgMemberMetaData
                {
                    MemberName = memberInfo.Name,
                    ValueType = valueType,
                    AccessorFactory = new ReflectionAccessorFactory(memberInfo),
                    PropertyCheckKey = valueType?.FullName,
                };

                // 收集并执行所有 IXuiavrgAttribute
                var attributes = memberInfo.GetCustomAttributes()
                    .OfType<IXuiavrgAttribute>()
                    .ToList();

                metaData.Attributes = attributes;

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
        /// 从对象实例提取元数据并一步生成绑定（便捷方法）
        /// 内部调用 Type.ExtractAvrgMemberMetaData() + BuildMDVM()
        /// </summary>
        public static AvrgMvvmBindingCollection BuildBindingsFromObject(
            this object source,
            AvrgMvvmResolverConfig resolverConfig = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var metaDataSet = source.GetType().ExtractAvrgMemberMetaData();
            return metaDataSet.BuildMDVM(source, resolverConfig);
        }

        // ============ Material: 元数据提取 ============

        /// <summary>
        /// 从 Material 的 Shader 属性中提取元数据集
        /// </summary>
        /// <param name="material">目标 Material</param>
        /// <returns>纯元数据集（AccessorFactory 持有 MaterialPropertyAccessorFactory）</returns>
        public static AvrgMemberMetaDataSet ExtractFromMaterial(this Material material)
        {
            if (material == null)
                throw new ArgumentNullException(nameof(material));

            var shader = material.shader;
            if (shader == null)
            {
                Debug.LogError("[Xuiavrg] Material 没有关联的 Shader");
                return null;
            }

            var metaDataSet = new AvrgMemberMetaDataSet();
            int propCount = shader.GetPropertyCount();

            for (int i = 0; i < propCount; i++)
            {
                var propName = shader.GetPropertyName(i);
                var shaderType = shader.GetPropertyType(i);
                var desc = shader.GetPropertyDescription(i);
                var flags = shader.GetPropertyFlags(i);

                // 跳过隐藏属性
                if ((flags & UnityEngine.Rendering.ShaderPropertyFlags.HideInInspector) != 0)
                    continue;

                // ShaderPropertyType → C# Type + Tag
                (Type valueType, XuiavrgFieldTag tag) = MapShaderPropertyType(shaderType, propName);

                var metaData = new AvrgMemberMetaData
                {
                    MemberName = propName,
                    ValueType = valueType,
                    LabelName = !string.IsNullOrEmpty(desc) ? desc : FormatPropertyName(propName),
                    Tag = tag,
                    AccessorFactory = new MaterialPropertyAccessorFactory(propName, shaderType, valueType),
                };

                // 读取当前值作为 DefaultValue
                metaData.DefaultValue = ReadMaterialProperty(material, propName, shaderType);

                // Range 属性：提取范围限制
                if (shaderType == ShaderPropertyType.Range)
                {
                    var limits = shader.GetPropertyRangeLimits(i);
                    metaData.RangeMin = limits.x;
                    metaData.RangeMax = limits.y;
                }

                metaDataSet.Add(metaData);
            }

            // ===== Shader Keywords 提取（只读）=====
            try
            {
                var keywordSpace = shader.keywordSpace;
                foreach (var kw in keywordSpace.keywords)
                {
                    bool isEnabled = material.IsKeywordEnabled(kw.name);
                    var kwMetaData = new AvrgMemberMetaData
                    {
                        MemberName = $"kwd_{kw.name}",
                        ValueType = typeof(bool),
                        LabelName = $"[Keyword] {kw.name}",
                        Tag = XuiavrgFieldTag.Toggle,
                        DefaultValue = isEnabled,
                        AccessorFactory = new MaterialPropertyAccessorFactory(
                            kw.name,
                            ShaderPropertyType.Float, // Keywords 不是标准属性，用 Float 占位
                            typeof(bool),
                            isReadOnly: true),
                    };
                    metaDataSet.Add(kwMetaData);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Xuiavrg] 提取 Shader Keywords 失败: {e.Message}");
            }

            metaDataSet.SortByFieldOrder();
            return metaDataSet;
        }

        private static (Type valueType, XuiavrgFieldTag tag) MapShaderPropertyType(
            ShaderPropertyType shaderType, string propName)
        {
            return shaderType switch
            {
                ShaderPropertyType.Float => (typeof(float), XuiavrgFieldTag.Input),
                ShaderPropertyType.Range => (typeof(float), XuiavrgFieldTag.Slider),
                ShaderPropertyType.Int => (typeof(int), XuiavrgFieldTag.Input),
                ShaderPropertyType.Color => (typeof(Color), XuiavrgFieldTag.Color),
                ShaderPropertyType.Vector => (typeof(Vector4), XuiavrgFieldTag.Vector),
                ShaderPropertyType.Texture => (typeof(Texture), XuiavrgFieldTag.Texture),
                _ => (typeof(object), XuiavrgFieldTag.None),
            };
        }

        private static object ReadMaterialProperty(Material material, string propName,
            ShaderPropertyType shaderType)
        {
            return shaderType switch
            {
                ShaderPropertyType.Float or ShaderPropertyType.Range => material.GetFloat(propName),
                ShaderPropertyType.Int => material.GetInt(propName),
                ShaderPropertyType.Color => material.GetColor(propName),
                ShaderPropertyType.Vector => material.GetVector(propName),
                ShaderPropertyType.Texture => material.GetTexture(propName),
                _ => null
            };
        }

        /// <summary>
        /// 属性名格式化（_MainTex → MainTex, _ShadowColor → Shadow Color）
        /// </summary>
        private static string FormatPropertyName(string propName)
        {
            if (string.IsNullOrEmpty(propName)) return propName;
            return propName.TrimStart('_').Replace('_', ' ');
        }
    }
}
