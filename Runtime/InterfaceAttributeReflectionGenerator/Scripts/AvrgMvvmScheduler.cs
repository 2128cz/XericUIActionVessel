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
    /// - 仅 UI 脏   → 写入数据源
    /// - 双方都脏 → 走配置的冲突解决策略
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
                // 双方都脏 — 走冲突解决
                ResolveConflict(binding);
            }
            else if (dataDirty)
            {
                // 仅数据侧更新 → 通知 UI
                var value = binding.DataCachedValue ?? binding.ReadFromDataSource();
                binding.CommitToDataSource(value);
                binding.NotifyUIUpdate(value);
            }
            else if (uiDirty)
            {
                // 仅 UI 侧更新 → 写入数据源
                binding.CommitToDataSource(binding.UICachedValue);
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
                        binding.CommitToDataSource(resolvedValue);
                        binding.NotifyUIUpdate(resolvedValue);
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
                binding.CommitToDataSource(configResolvedValue);
                binding.NotifyUIUpdate(configResolvedValue);
                return;
            }

            // 3. 按默认策略解决
            switch (DefaultStrategy)
            {
                case ConflictResolutionStrategy.PreferUI:
                    binding.CommitToDataSource(binding.UICachedValue);
                    binding.NotifyUIUpdate(binding.UICachedValue);
                    break;

                case ConflictResolutionStrategy.PreferData:
                    var dataValue = binding.DataCachedValue ?? binding.ReadFromDataSource();
                    binding.CommitToDataSource(dataValue);
                    binding.NotifyUIUpdate(dataValue);
                    break;

                case ConflictResolutionStrategy.CompareValues:
                    // 比较UI值和数据值
                    object uiVal = binding.UICachedValue;
                    object dataVal = binding.DataCachedValue ?? binding.ReadFromDataSource();

                    if (Equals(uiVal, dataVal))
                    {
                        // 值相同，忽略冲突
                        binding.NotifyUIUpdate(dataVal);
                        break;
                    }

                    // 值不同，回退到 PreferUI
                    binding.CommitToDataSource(binding.UICachedValue);
                    binding.NotifyUIUpdate(binding.UICachedValue);
                    Debug.LogWarning($"[AvrgMvvmScheduler] 值冲突(CompareValues回退PreferUI): {binding.MetaData?.MemberName}");
                    break;

                default:
                    binding.CommitToDataSource(binding.UICachedValue);
                    binding.NotifyUIUpdate(binding.UICachedValue);
                    break;
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
