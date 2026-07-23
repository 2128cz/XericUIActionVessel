using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 元数据集
    /// 包含从对象反射提取的完整元数据，以及用于生成 MVVM 绑定的 BuildMDVM() 方法
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
            // 提取编组和非编组
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
                // 编组内第一个成员作为主元数据
                var first = gEntry.First();
                // 合成的 metaData 保持 GroupPath 和主 Tag
                merged.Add(first);
            }

            // 非编组直接追加
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
        /// 为元数据生成对应的 MVVM 值绑定模板
        /// </summary>
        /// <param name="businessTarget">可选的业务目标对象（如果元数据中没有绑定目标，则在此传入）</param>
        /// <param name="resolverConfig">可选的冲突解决配置</param>
        /// <returns>绑定集合</returns>
        public AvrgMvvmBindingCollection BuildMDVM(
            object businessTarget = null,
            AvrgMvvmResolverConfig resolverConfig = null)
        {
            var bindingCollection = new AvrgMvvmBindingCollection();
            var scheduler = new AvrgMvvmScheduler(resolverConfig);

            foreach (var metaData in _metaDataList)
            {
                // 确定目标实例
                var target = metaData.TargetInstance ?? businessTarget;
                if (target == null)
                    continue;

                // 创建绑定
                var binding = new AvrgMvvmBinding
                {
                    MetaData = metaData,
                    ReadFunc = () => metaData.GetValue(),
                };

                var capturedBinding = binding;
                binding.WriteAction = (val) =>
                {
                    metaData.SetValue(val);
                    scheduler.MarkDataDirty(capturedBinding);
                };

                // 注册到调度器
                scheduler.Register(binding);
                bindingCollection.Add(binding);
            }

            bindingCollection.AttachScheduler(scheduler);
            return bindingCollection;
        }

        // ============ 验证方法 ============

        /// <summary>
        /// 对当前元数据集执行属性检查验证
        /// </summary>
        /// <param name="valueRangeConfig">值范围配置（可选，不传入则不执行范围检查）</param>
        /// <returns>验证结果集合</returns>
        public AvrgValidationResultSet Validate(AvrgValueRangeConfig valueRangeConfig = null)
        {
            var resultSet = new AvrgValidationResultSet();

            foreach (var metaData in _metaDataList)
            {
                if (metaData.Hide)
                    continue;

                // 只有标记了 PropertyCheck 的才验证
                if (!metaData.Required && !metaData.ValidateReasonable
                    && !metaData.ValidateWarning && !metaData.ValidateInvalid)
                {
                    resultSet.Results.Add(new AvrgMemberValidationResult
                    {
                        MetaData = metaData,
                        Result = AvrgMemberCheckResult.Skipped,
                        CurrentValueString = metaData.GetValue()?.ToString(),
                        Message = "未标记属性检查",
                    });
                    continue;
                }

                var value = metaData.GetValue();
                var checkResult = CheckSingleMember(metaData, value, valueRangeConfig);
                resultSet.Results.Add(checkResult);
            }

            return resultSet;
        }

        /// <summary>
        /// 验证单个成员
        /// </summary>
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

            // 根据 CheckKey 查找值范围规则
            AvrgValueRangeEntry rangeEntry = null;
            string checkKey = !string.IsNullOrEmpty(metaData.PropertyCheckKey)
                ? metaData.PropertyCheckKey
                : metaData.ValueType?.FullName;

            if (rangeConfig != null && !string.IsNullOrEmpty(checkKey))
            {
                rangeEntry = rangeConfig.FindEntry(checkKey);
            }

            // ——— 优先级 1: 空值检查 (Required) ———
            if (metaData.Required)
            {
                bool isEmpty = false;

                // 引用类型 null
                if (value == null)
                {
                    isEmpty = true;
                }
                // 字符串 null/空
                else if (value is string str)
                {
                    isEmpty = string.IsNullOrEmpty(str);
                }
                // 使用 ValueRangeEntry 的 IsEmpty 判断
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

            // 如果没有范围配置，无法继续检查
            if (rangeEntry == null)
            {
                result.Result = AvrgMemberCheckResult.Valid;
                result.Message = "通过（无范围配置）";
                return result;
            }

            // ——— 优先级 2: 合理值检查（最高优先级，命中即通过） ———
            if (metaData.ValidateReasonable && rangeEntry.IsReasonable(value))
            {
                result.Result = AvrgMemberCheckResult.Valid;
                result.Message = "值在合理范围内";
                return result;
            }

            // ——— 优先级 3: 无效值检查 ———
            if (metaData.ValidateInvalid && rangeEntry.IsInvalid(value))
            {
                result.Result = AvrgMemberCheckResult.Invalid;
                result.Message = $"值 {value} 在无效范围内";
                return result;
            }

            // ——— 优先级 4: 警告值检查 ———
            if (metaData.ValidateWarning && rangeEntry.IsWarning(value))
            {
                result.Result = AvrgMemberCheckResult.Warning;
                result.Message = $"值 {value} 在警告范围内";
                return result;
            }

            // ——— 最终: 通过 ———
            result.Result = AvrgMemberCheckResult.Valid;
            result.Message = "通过";
            return result;
        }
    }
}
