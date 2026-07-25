using System;
using System.Collections.Generic;
using UnityEngine;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// MVVM 调度器 — 管理脏标记、触发 Flush 更新、检测并解决冲突
    /// 
    /// 冲突解决策略：
    /// - 仅数据脏 → 通知 UI 更新
    /// - 仅 UI 脏   → 写入数据源（写入前校验，Invalid 驳回）
    /// - 双方都脏 → 走配置的冲突解决策略
    /// 
    /// 验证拦截：在 CommitToDataSource 前通过 Binding.ValidateValue() 校验。
    /// Invalid 值被驳回不写入，仅通知 UI 进入无效状态。
    /// </summary>
    public class AvrgMvvmScheduler
    {
        /// <summary>用户可注册的外部冲突解决事件</summary>
        public event Func<AvrgMvvmBinding, ConflictResolutionStrategy, object> OnConflictDetected;

        /// <summary>当前配置的冲突解决策略</summary>
        public ConflictResolutionStrategy DefaultStrategy { get; set; } = ConflictResolutionStrategy.PreferUI;

        private List<AvrgMvvmBinding> _bindings = new List<AvrgMvvmBinding>();
        private HashSet<AvrgMvvmBinding> _dirtySet = new HashSet<AvrgMvvmBinding>();
        private AvrgMvvmResolverConfig _resolverConfig;

        public AvrgMvvmScheduler(AvrgMvvmResolverConfig resolverConfig = null)
        {
            _resolverConfig = resolverConfig;
        }

        /// <summary>
        /// 注册绑定到调度器
        /// </summary>
        public void Register(AvrgMvvmBinding binding)
        {
            if (!_bindings.Contains(binding))
                _bindings.Add(binding);
        }

        /// <summary>
        /// UI 侧写入时调用 — 标记脏
        /// </summary>
        public void MarkUIDirty(AvrgMvvmBinding binding)
        {
            binding.IsUIDirty = true;
            _dirtySet.Add(binding);
        }

        /// <summary>
        /// 数据侧写入时调用 — 标记脏
        /// </summary>
        public void MarkDataDirty(AvrgMvvmBinding binding)
        {
            binding.IsDataDirty = true;
            _dirtySet.Add(binding);
        }

        /// <summary>
        /// 处理所有脏绑定
        /// </summary>
        public void Flush()
        {
            if (_dirtySet.Count == 0) return;

            foreach (var binding in _dirtySet)
            {
                ResolveBinding(binding);
                binding.ClearDirty();
            }

            _dirtySet.Clear();
        }

        private void ResolveBinding(AvrgMvvmBinding binding)
        {
            bool uiDirty = binding.IsUIDirty;
            bool dataDirty = binding.IsDataDirty;

            if (uiDirty && dataDirty)
            {
                // 双方都脏 — 走冲突解决（内部含验证）
                ResolveConflict(binding);
            }
            else if (dataDirty)
            {
                // 仅数据侧更新 → 验证后通知 UI
                var value = binding.DataCachedValue ?? binding.ReadFromDataSource();
                var state = binding.ValidateValue(value);
                binding.SetValidationState(state);

                binding.CommitToDataSource(value);
                binding.NotifyUIUpdate(value, state);
            }
            else if (uiDirty)
            {
                // 仅 UI 侧更新 → 验证后写入数据源
                var value = binding.UICachedValue;
                var state = binding.ValidateValue(value);

                if (state == ValidationState.Invalid)
                {
                    // ★ 无效值：驳回不写入，仅通知 UI 进入无效态
                    binding.SetValidationState(state);
                    binding.NotifyUIUpdate(value, state);
                    return;
                }

                binding.SetValidationState(state);
                binding.CommitToDataSource(value);
                // UI 是发起方，不再通知自身（但验证状态变更需通知）
            }
        }

        private void ResolveConflict(AvrgMvvmBinding binding)
        {
            // 1. 检查外部事件处理
            if (OnConflictDetected != null)
            {
                try
                {
                    var resolvedValue = OnConflictDetected.Invoke(binding, DefaultStrategy);
                    if (resolvedValue != null)
                    {
                        var state = binding.ValidateValue(resolvedValue);
                        binding.SetValidationState(state);
                        binding.CommitToDataSource(resolvedValue);
                        binding.NotifyUIUpdate(resolvedValue, state);
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AvrgMvvmScheduler] 外部冲突解决事件异常: {e.Message}");
                }
            }

            // 2. 检查 ResolverConfig 中的自定义策略
            if (_resolverConfig != null && _resolverConfig.TryResolve(binding, out object configResolvedValue))
            {
                var state = binding.ValidateValue(configResolvedValue);
                binding.SetValidationState(state);
                binding.CommitToDataSource(configResolvedValue);
                binding.NotifyUIUpdate(configResolvedValue, state);
                return;
            }

            // 3. 按默认策略解决
            switch (DefaultStrategy)
            {
                case ConflictResolutionStrategy.PreferUI:
                    {
                        var val = binding.UICachedValue;
                        var state = binding.ValidateValue(val);
                        if (state == ValidationState.Invalid)
                        {
                            binding.SetValidationState(state);
                            binding.NotifyUIUpdate(val, state);
                            return;
                        }
                        binding.SetValidationState(state);
                        binding.CommitToDataSource(val);
                        binding.NotifyUIUpdate(val, state);
                        break;
                    }

                case ConflictResolutionStrategy.PreferData:
                    {
                        var dataValue = binding.DataCachedValue ?? binding.ReadFromDataSource();
                        var state = binding.ValidateValue(dataValue);
                        binding.SetValidationState(state);
                        binding.CommitToDataSource(dataValue);
                        binding.NotifyUIUpdate(dataValue, state);
                        break;
                    }

                case ConflictResolutionStrategy.CompareValues:
                    {
                        object uiVal = binding.UICachedValue;
                        object dataVal = binding.DataCachedValue ?? binding.ReadFromDataSource();

                        if (Equals(uiVal, dataVal))
                        {
                            // 值相同，忽略冲突
                            var state2 = binding.ValidateValue(dataVal);
                            binding.SetValidationState(state2);
                            binding.NotifyUIUpdate(dataVal, state2);
                            break;
                        }

                        // 值不同，回退到 PreferUI
                        var val = binding.UICachedValue;
                        var state = binding.ValidateValue(val);
                        if (state == ValidationState.Invalid)
                        {
                            binding.SetValidationState(state);
                            binding.NotifyUIUpdate(val, state);
                            return;
                        }
                        binding.SetValidationState(state);
                        binding.CommitToDataSource(val);
                        binding.NotifyUIUpdate(val, state);
                        Debug.LogWarning($"[AvrgMvvmScheduler] 值冲突(CompareValues回退PreferUI): {binding.MetaData?.MemberName}");
                        break;
                    }

                default:
                    {
                        var val = binding.UICachedValue;
                        var state = binding.ValidateValue(val);
                        binding.SetValidationState(state);
                        binding.CommitToDataSource(val);
                        binding.NotifyUIUpdate(val, state);
                        break;
                    }
            }
        }

        /// <summary>
        /// 清空所有注册
        /// </summary>
        public void Clear()
        {
            _bindings.Clear();
            _dirtySet.Clear();
        }

        /// <summary>
        /// 手动将绑定的 UI 值标记为脏
        /// </summary>
        public void MarkUI(AvrgMvvmBinding binding, object value)
        {
            binding.WriteFromUI(value);
            MarkUIDirty(binding);
        }

        /// <summary>
        /// 手动将绑定的数据值写入并标记脏
        /// </summary>
        public void MarkData(AvrgMvvmBinding binding, object value)
        {
            binding.WriteFromData(value);
            MarkDataDirty(binding);
        }
    }

    /// <summary>
    /// 冲突解决策略枚举
    /// </summary>
    public enum ConflictResolutionStrategy
    {
        /// <summary>优先使用 UI 侧的值</summary>
        PreferUI,

        /// <summary>优先使用数据源侧的值</summary>
        PreferData,

        /// <summary>比较两者，相同则忽略，不同则优先 UI</summary>
        CompareValues,
    }
}
