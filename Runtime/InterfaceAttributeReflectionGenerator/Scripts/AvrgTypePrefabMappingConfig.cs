using System;
using System.Collections.Generic;
using UnityEngine;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 类型-预制体映射配置 ScriptableObject
    /// 用于在 GenerateUI 阶段根据数据类型查找匹配的预制体
    /// </summary>
    [CreateAssetMenu(
        fileName = "Xeric UI avr Type-Prefab Mapping",
        menuName = "Xeric Library/UI Action Vessel/Reflection UI Gen/UI avr Type-Prefab Mapping",
        order = 15)]
    public class AvrgTypePrefabMappingConfig : ScriptableObject
    {
        /// <summary>
        /// 单个类型映射条目
        /// </summary>
        [Serializable]
        public class TypePrefabEntry
        {
            [Tooltip("C# 数据类型全名 (如 System.Single, System.String, System.Boolean)")]
            public string valueTypeName;

            [Tooltip("对应的 UI 预制体")]
            public GameObject prefab;

            /// <summary>缓存的类型</summary>
            [NonSerialized] public Type cachedType;
        }

        /// <summary>类型映射列表</summary>
        [Tooltip("类型映射列表：数据类型 → UI 预制体")]
        public List<TypePrefabEntry> typePrefabEntries = new List<TypePrefabEntry>();

        /// <summary>默认预制体（无精确匹配时的回退）</summary>
        [Tooltip("默认预制体（没有类型匹配时的回退方案）")]
        public GameObject defaultPrefab;

        [NonSerialized]
        private Dictionary<Type, GameObject> _typeCache;

        private void BuildCache()
        {
            if (_typeCache != null) return;
            _typeCache = new Dictionary<Type, GameObject>();

            foreach (var entry in typePrefabEntries)
            {
                if (entry.prefab == null) continue;

                if (entry.cachedType == null && !string.IsNullOrEmpty(entry.valueTypeName))
                    entry.cachedType = Type.GetType(entry.valueTypeName);

                if (entry.cachedType != null && !_typeCache.ContainsKey(entry.cachedType))
                    _typeCache[entry.cachedType] = entry.prefab;
            }
        }

        /// <summary>
        /// 根据数据类型查找匹配的预制体
        /// </summary>
        public GameObject FindPrefabByType(Type valueType)
        {
            if (valueType == null) return defaultPrefab;

            BuildCache();

            if (_typeCache.TryGetValue(valueType, out var prefab))
                return prefab;

            // 精确匹配失败，尝试基类/接口匹配
            foreach (var kvp in _typeCache)
            {
                if (kvp.Key.IsAssignableFrom(valueType))
                    return kvp.Value;
            }

            return defaultPrefab;
        }

        /// <summary>
        /// 根据绑定的 Entry Bindings 查找匹配的预制体
        /// </summary>
        public GameObject FindPrefabByBindings(IReadOnlyList<AvrgMvvmBinding> bindings)
        {
            if (bindings == null || bindings.Count == 0) return defaultPrefab;

            var metaData = bindings[0].MetaData;
            if (metaData?.ValueType == null) return defaultPrefab;

            // 优先按 PrefabOverrideName 查找预制体
            if (!string.IsNullOrEmpty(metaData.PrefabOverrideName))
            {
                foreach (var entry in typePrefabEntries)
                {
                    if (entry.prefab != null && entry.prefab.name == metaData.PrefabOverrideName)
                        return entry.prefab;
                }
            }

            // 按类型匹配
            var typeMatch = FindPrefabByType(metaData.ValueType);
            if (typeMatch != null) return typeMatch;

            return defaultPrefab;
        }

        /// <summary>
        /// 按预制体名称查找
        /// </summary>
        public GameObject FindPrefabByName(string prefabName)
        {
            foreach (var entry in typePrefabEntries)
            {
                if (entry.prefab != null && entry.prefab.name == prefabName)
                    return entry.prefab;
            }
            return null;
        }

        /// <summary>
        /// 清空缓存
        /// </summary>
        public void ClearCache()
        {
            _typeCache = null;
        }
    }
}
