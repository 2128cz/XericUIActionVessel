using System;
using System.Collections.Generic;
using UnityEngine;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 类型-预制体映射配置 ScriptableObject
    /// 用于在 GenerateUI 阶段根据数据类型 + Tag 查找匹配的预制体
    /// </summary>
    [CreateAssetMenu(
        fileName = "Xeric UI avr Type-Prefab Mapping",
        menuName = "Xeric Library/UI Action Vessel/Reflection UI Gen/UI avr Type-Prefab Mapping",
        order = 15)]
    public class AvrgTypePrefabMappingConfig : ScriptableObject
    {
        /// <summary>
        /// Tag 预制体覆盖 — 同一类型在不同 Tag 下使用不同的预制体
        /// 例如 float + Slider → FloatSlider, float + Dropdown → FloatDropdown
        /// </summary>
        [Serializable]
        public class TagPrefabOverride
        {
            [Tooltip("功能标记")]
            public XuiavrgFieldTag tag;

            [Tooltip("该 Tag 对应的 UI 预制体")]
            public GameObject prefab;
        }

        /// <summary>
        /// 单个类型映射条目
        /// </summary>
        [Serializable]
        public class TypePrefabEntry
        {
            [Tooltip("C# 数据类型全名 (如 System.Single, System.String, System.Boolean)")]
            public string valueTypeName;

            [Tooltip("对应此类型的默认 UI 预制体")]
            public GameObject prefab;

            [Tooltip("按 Tag 覆盖预制体（同一类型在不同标记下使用不同 UI）")]
            public List<TagPrefabOverride> tagOverrides = new List<TagPrefabOverride>();

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
        /// 按数据类型 + Tag 查找匹配的预制体
        /// 优先匹配 tagOverrides，未匹配则按类型查找
        /// </summary>
        /// <param name="valueType">数据类型</param>
        /// <param name="tag">功能标记（None 时等同 FindPrefabByType）</param>
        public GameObject FindPrefabByTypeAndTag(Type valueType, XuiavrgFieldTag tag)
        {
            if (valueType == null) return defaultPrefab;
            if (tag == XuiavrgFieldTag.None)
                return FindPrefabByType(valueType);

            BuildCache();

            // 查找类型条目
            foreach (var entry in typePrefabEntries)
            {
                if (entry.cachedType == null && !string.IsNullOrEmpty(entry.valueTypeName))
                    entry.cachedType = Type.GetType(entry.valueTypeName);

                if (entry.cachedType == null) continue;

                // 类型匹配
                if (entry.cachedType == valueType || entry.cachedType.IsAssignableFrom(valueType))
                {
                    // 优先查 tagOverrides
                    foreach (var overrideEntry in entry.tagOverrides)
                    {
                        if (overrideEntry.tag == tag && overrideEntry.prefab != null)
                            return overrideEntry.prefab;
                    }

                    // 没有 Tag 覆盖则返回类型默认预制体
                    if (entry.prefab != null)
                        return entry.prefab;
                }
            }

            // 类型匹配 + Tag 匹配失败，尝试纯类型回退
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

            // 按类型匹配
            var typeMatch = FindPrefabByType(metaData.ValueType);
            if (typeMatch != null) return typeMatch;

            return defaultPrefab;
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
