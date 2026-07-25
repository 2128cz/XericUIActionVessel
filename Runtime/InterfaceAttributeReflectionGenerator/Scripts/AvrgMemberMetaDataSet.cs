using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 元数据集
    /// 包含从对象/材质反射提取的完整元数据，以及用于生成 MVVM 绑定的 BuildMDVM() 方法
    /// </summary>
    public class AvrgMemberMetaDataSet
    {
        private List<AvrgMemberMetaData> _metaDataList = new List<AvrgMemberMetaData>();

        /// <summary>
        /// 只读访问元数据列表
        /// </summary>
        public IReadOnlyList<AvrgMemberMetaData> MetaDataList => _metaDataList;

        /// <summary>
        /// 添加元数据
        /// </summary>
        public void Add(AvrgMemberMetaData metaData)
        {
            _metaDataList.Add(metaData);
        }

        /// <summary>
        /// 处理编组合并 — 将 GroupPath 相同的元数据合并
        /// 非编组元数据保持为独立 Entry
        /// </summary>
        internal void MergeGroupEntries()
        {
            var grouped = _metaDataList
                .Where(m => !string.IsNullOrEmpty(m.GroupPath))
                .GroupBy(m => m.GroupPath)
                .ToList();

            var nonGrouped = _metaDataList
                .Where(m => string.IsNullOrEmpty(m.GroupPath))
                .ToList();

            var merged = new List<AvrgMemberMetaData>();

            foreach (var gEntry in grouped)
            {
                var first = gEntry.First();
                foreach (var member in gEntry)
                {
                    if (member.SubMetaDataList?.Count > 0)
                    {
                        if (first.SubMetaDataList == null)
                            first.SubMetaDataList = new List<AvrgMemberMetaData>();
                        first.SubMetaDataList.AddRange(member.SubMetaDataList);
                    }
                }
                merged.Add(first);
            }

            merged.AddRange(nonGrouped);
            _metaDataList = merged;
        }

        /// <summary>
        /// 按 FieldOrder 排序
        /// </summary>
        internal void SortByFieldOrder()
        {
            _metaDataList.Sort((a, b) =>
            {
                int orderCompare = a.FieldOrder.CompareTo(b.FieldOrder);
                if (orderCompare != 0) return orderCompare;
                return string.Compare(a.MemberName, b.MemberName, StringComparison.Ordinal);
            });
        }

        /// <summary>
        /// 为元数据生成对应的 MVVM 值绑定
        /// 通过 AccessorFactory 创建 IAvrgValueAccessor 完成数据读写
        /// </summary>
        /// <param name="businessTarget">目标实例（C# 对象、Material 等）</param>
        /// <param name="resolverConfig">可选的冲突解决配置</param>
        /// <returns>绑定集合</returns>
        public AvrgMvvmBindingCollection BuildMDVM(
            object businessTarget = null,
            AvrgMvvmResolverConfig resolverConfig = null)
        {
            var bindingCollection = new AvrgMvvmBindingCollection();
            bindingCollection.SourceMetaDataSet = this;
            var scheduler = new AvrgMvvmScheduler(resolverConfig);

            foreach (var metaData in _metaDataList)
            {
                if (metaData.AccessorFactory == null) continue;
                if (businessTarget == null)
                {
                    Debug.LogWarning(
                        $"[Xuiavrg] 元数据 '{metaData.MemberName}' 缺少 targetInstance，跳过绑定");
                    continue;
                }

                var accessor = metaData.AccessorFactory.CreateAccessor(businessTarget);
                if (accessor == null) continue;

                // 创建主绑定
                var binding = new AvrgMvvmBinding
                {
                    MetaData = metaData,
                    ReadFunc = () => accessor.GetValue(),
                    Accessor = accessor,
                };

                var capturedBinding = binding;

                if (accessor.IsReadOnly)
                {
                    binding.WriteAction = null;
                }
                else
                {
                    binding.WriteAction = (val) =>
                    {
                        accessor.SetValue(val);
                        scheduler.MarkDataDirty(capturedBinding);
                    };
                }

                // 拆分属性子绑定
                if (metaData.SubMetaDataList?.Count > 0)
                {
                    binding.SubBindings = new List<AvrgMvvmBinding>();

                    foreach (var subMetaData in metaData.SubMetaDataList)
                    {
                        string subPath = subMetaData.PropertyPath;
                        if (string.IsNullOrEmpty(subPath)) continue;

                        var subBinding = new AvrgMvvmBinding
                        {
                            MetaData = subMetaData,
                            ReadFunc = () => ReadSubProperty(accessor.GetValue(), subPath),
                            WriteAction = (val) =>
                            {
                                var parentVal = accessor.GetValue();
                                var modifiedParent = WriteSubProperty(parentVal, subPath, val);
                                accessor.SetValue(modifiedParent);
                                scheduler.MarkDataDirty(capturedBinding);
                            },
                        };

                        binding.SubBindings.Add(subBinding);
                    }
                }

                // 注册到调度器
                scheduler.Register(binding);
                bindingCollection.Add(binding);
            }

            bindingCollection.AttachScheduler(scheduler);
            return bindingCollection;
        }

        // ============ 子属性访问辅助方法 ============

        private static object ReadSubProperty(object parentValue, string propertyPath)
        {
            if (parentValue == null || string.IsNullOrEmpty(propertyPath))
                return null;

            if (propertyPath.Contains('/'))
            {
                Debug.LogWarning($"[Xuiavrg] 暂不支持多层嵌套路径 '{propertyPath}'");
                return null;
            }

            var type = parentValue.GetType();
            var field = type.GetRuntimeField(propertyPath);
            if (field != null) return field.GetValue(parentValue);

            var prop = type.GetRuntimeProperty(propertyPath);
            if (prop != null) return prop.GetValue(parentValue);

            Debug.LogWarning($"[Xuiavrg] 在类型 '{type.Name}' 上找不到子属性 '{propertyPath}'");
            return null;
        }

        private static object WriteSubProperty(object parentValue, string propertyPath, object value)
        {
            if (parentValue == null || string.IsNullOrEmpty(propertyPath))
                return parentValue;

            if (propertyPath.Contains('/'))
            {
                Debug.LogWarning($"[Xuiavrg] 暂不支持多层嵌套路径 '{propertyPath}'");
                return parentValue;
            }

            var type = parentValue.GetType();
            var field = type.GetRuntimeField(propertyPath);
            if (field != null)
            {
                field.SetValue(parentValue, value);
                return parentValue;
            }

            var prop = type.GetRuntimeProperty(propertyPath);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(parentValue, value);
                return parentValue;
            }

            Debug.LogWarning($"[Xuiavrg] 在类型 '{type.Name}' 上找不到可写的子属性 '{propertyPath}'");
            return parentValue;
        }

        // ============ 验证方法 ============

        /// <summary>
        /// 对当前元数据集执行属性检查验证
        /// </summary>
        public AvrgValidationResultSet Validate(AvrgValueRangeConfig valueRangeConfig = null)
        {
            var resultSet = new AvrgValidationResultSet();

            foreach (var metaData in _metaDataList)
            {
                if (metaData.Hide)
                    continue;

                if (!metaData.Required && !metaData.ValidateReasonable
                    && !metaData.ValidateWarning && !metaData.ValidateInvalid)
                {
                    resultSet.Results.Add(new AvrgMemberValidationResult
                    {
                        MetaData = metaData,
                        Result = AvrgMemberCheckResult.Skipped,
                        CurrentValueString = metaData.DefaultValue?.ToString(),
                        Message = "未标记属性检查",
                    });
                    continue;
                }

                var value = metaData.DefaultValue;
                var checkResult = CheckSingleMember(metaData, value, valueRangeConfig);
                resultSet.Results.Add(checkResult);
            }

            return resultSet;
        }

        private AvrgMemberValidationResult CheckSingleMember(
            AvrgMemberMetaData metaData,
            object value,
            AvrgValueRangeConfig rangeConfig)
        {
            var result = new AvrgMemberValidationResult
            {
                MetaData = metaData,
                CurrentValueString = value?.ToString() ?? "(null)",
            };

            AvrgValueRangeEntry rangeEntry = null;
            string checkKey = !string.IsNullOrEmpty(metaData.PropertyCheckKey)
                ? metaData.PropertyCheckKey
                : metaData.ValueType?.FullName;

            if (rangeConfig != null && !string.IsNullOrEmpty(checkKey))
            {
                rangeEntry = rangeConfig.FindEntry(checkKey);
            }

            // 优先级 1: 空值检查 (Required)
            if (metaData.Required)
            {
                bool isEmpty = false;

                if (value == null)
                {
                    isEmpty = true;
                }
                else if (value is string str)
                {
                    isEmpty = string.IsNullOrEmpty(str);
                }
                else if (rangeEntry != null && !rangeEntry.skipEmptyCheck)
                {
                    isEmpty = rangeEntry.IsEmpty(value);
                }

                if (isEmpty)
                {
                    result.Result = AvrgMemberCheckResult.Empty;
                    result.Message = $"必填字段 '{metaData.MemberName}' 为空";
                    return result;
                }
            }

            if (rangeEntry == null)
            {
                result.Result = AvrgMemberCheckResult.Valid;
                result.Message = "通过（无范围配置）";
                return result;
            }

            // 优先级 2: 合理值检查
            if (metaData.ValidateReasonable && rangeEntry.IsReasonable(value))
            {
                result.Result = AvrgMemberCheckResult.Valid;
                result.Message = "值在合理范围内";
                return result;
            }

            // 优先级 3: 无效值检查
            if (metaData.ValidateInvalid && rangeEntry.IsInvalid(value))
            {
                result.Result = AvrgMemberCheckResult.Invalid;
                result.Message = $"值 {value} 在无效范围内";
                return result;
            }

            // 优先级 4: 警告值检查
            if (metaData.ValidateWarning && rangeEntry.IsWarning(value))
            {
                result.Result = AvrgMemberCheckResult.Warning;
                result.Message = $"值 {value} 在警告范围内";
                return result;
            }

            result.Result = AvrgMemberCheckResult.Valid;
            result.Message = "通过";
            return result;
        }
    }
}
