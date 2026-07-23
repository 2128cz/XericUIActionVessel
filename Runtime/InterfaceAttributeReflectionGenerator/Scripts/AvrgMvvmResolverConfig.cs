using System;
using System.Collections.Generic;
using UnityEngine;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 可配置的冲突解决器 ScriptableObject
    /// 预配置各种冲突场景下的决策方法，以及可注册的自定义解析器列表
    /// </summary>
    [CreateAssetMenu(
        fileName = "Xeric UI avr Resolver Config",
        menuName = "Xeric Library/UI Action Vessel/Reflection UI Gen/UI avr Resolver Config",
        order = 20)]
    public class AvrgMvvmResolverConfig : ScriptableObject, IAvrgMvvmConflictResolver
    {
        /// <summary>全局默认冲突解决策略</summary>
        [Tooltip("全局默认冲突解决策略")]
        public ConflictResolutionStrategy defaultStrategy = ConflictResolutionStrategy.PreferUI;

        /// <summary>自定义解析器列表（按序尝试）</summary>
        [Tooltip("自定义解析器类型全名列表（按序尝试，需实现 IAvrgMvvmConflictResolver）")]
        public List<string> customResolverTypeNames = new List<string>();

        // 运行时已实例化的解析器缓存
        [NonSerialized]
        private List<IAvrgMvvmConflictResolver> _resolverInstances;

        /// <summary>
        /// 获取或实例化自定义解析器
        /// </summary>
        private List<IAvrgMvvmConflictResolver> GetResolverInstances()
        {
            if (_resolverInstances != null)
                return _resolverInstances;

            _resolverInstances = new List<IAvrgMvvmConflictResolver>();

            foreach (var typeName in customResolverTypeNames)
            {
                try
                {
                    var type = Type.GetType(typeName);
                    if (type != null && typeof(IAvrgMvvmConflictResolver).IsAssignableFrom(type))
                    {
                        var instance = Activator.CreateInstance(type) as IAvrgMvvmConflictResolver;
                        if (instance != null)
                            _resolverInstances.Add(instance);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AvrgMvvmResolverConfig] 无法实例化解析器 {typeName}: {e.Message}");
                }
            }

            return _resolverInstances;
        }

        /// <summary>
        /// 尝试按配置解决冲突
        /// </summary>
        public bool TryResolve(AvrgMvvmBinding binding, out object resolvedValue)
        {
            // 按序尝试自定义解析器
            var resolvers = GetResolverInstances();
            foreach (var resolver in resolvers)
            {
                if (resolver.TryResolve(binding, defaultStrategy, out resolvedValue))
                    return true;
            }

            resolvedValue = null;
            return false;
        }

        /// <summary>
        /// IAvrgMvvmConflictResolver 接口实现
        /// </summary>
        bool IAvrgMvvmConflictResolver.TryResolve(
            AvrgMvvmBinding binding,
            ConflictResolutionStrategy defaultStrategy,
            out object resolvedValue)
        {
            return TryResolve(binding, out resolvedValue);
        }
    }
}
