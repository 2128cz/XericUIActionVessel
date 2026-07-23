using System;
using System.Collections.Generic;
using UnityEngine;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 值范围配置 ScriptableObject
    /// 定义不同类型的空/无效/警告/合理值范围判断依据
    /// </summary>
    [CreateAssetMenu(
        fileName = "Xeric UI avr Value Range Config",
        menuName = "Xeric Library/UI Action Vessel/Reflection UI Gen/UI avr Value Range Config",
        order = 25)]
    public class AvrgValueRangeConfig : ScriptableObject
    {
        /// <summary>值范围规则列表</summary>
        [Tooltip("按类型定义的取值范围规则")]
        public List<AvrgValueRangeEntry> entries = new List<AvrgValueRangeEntry>();

        [NonSerialized]
        private Dictionary<string, AvrgValueRangeEntry> _entryCache;

        private void BuildCache()
        {
            if (_entryCache != null) return;
            _entryCache = new Dictionary<string, AvrgValueRangeEntry>();
            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.key) && !_entryCache.ContainsKey(entry.key))
                    _entryCache[entry.key] = entry;
            }
        }

        /// <summary>
        /// 根据键查找值范围规则
        /// </summary>
        /// <param name="key">类型标识（通常为 Type.FullName）</param>
        /// <returns>匹配的规则，未找到则返回 null</returns>
        public AvrgValueRangeEntry FindEntry(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            BuildCache();
            _entryCache.TryGetValue(key, out var entry);
            return entry;
        }

        /// <summary>
        /// 根据 Type 查找值范围规则
        /// </summary>
        public AvrgValueRangeEntry FindEntryByType(Type type)
        {
            if (type == null) return null;
            return FindEntry(type.FullName);
        }

        /// <summary>
        /// 清空缓存（用于编辑器热重载后重新构建）
        /// </summary>
        public void ClearCache()
        {
            _entryCache = null;
        }
    }
}
