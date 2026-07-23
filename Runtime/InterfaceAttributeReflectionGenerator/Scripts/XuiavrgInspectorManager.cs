using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 节点下生成反射属性管理器 — Metadata-Driven View Model (MDVM 元数据驱动的视图数据模型)
    /// 负责：构建 Entry → 创建组容器 → 生成 UI → 绑定数据
    /// </summary>
    public class XuiavrgInspectorManager : MonoBehaviour
    {
        #region 对象池

        private Dictionary<GameObject, ObjectPool<GameObject>> _prefabPools
            = new Dictionary<GameObject, ObjectPool<GameObject>>();

        public GameObject PrefabPoolGet(GameObject prefab)
        {
            if (prefab == null) return null;

            if (!_prefabPools.TryGetValue(prefab, out var pool))
            {
                pool = new ObjectPool<GameObject>(
                    createFunc: () =>
                        prefab != null
                            ? Object.Instantiate(prefab, transform)
                            : new GameObject("PropertyItem", typeof(RectTransform)),
                    actionOnGet: go => go.SetActive(true),
                    actionOnRelease: go => go.SetActive(false),
                    actionOnDestroy: go =>
                    {
                        if (go) Destroy(go);
                    },
                    collectionCheck: false,
                    defaultCapacity: 4,
                    maxSize: 64
                );
                _prefabPools[prefab] = pool;
            }

            return pool.Get();
        }

        public void PrefabPoolRelease(GameObject prefab, GameObject instance)
        {
            if (prefab == null || instance == null) return;
            if (_prefabPools.TryGetValue(prefab, out var pool))
            {
                pool.Release(instance);
            }
            else
            {
                if (instance) Destroy(instance);
            }
        }

        public void ClearAllPrefabPools()
        {
            foreach (var pool in _prefabPools.Values)
            {
                pool.Clear();
            }
            _prefabPools.Clear();
        }

        #endregion

        #region 字段

        public List<IFieldEntry> CurrentEntries { get; private set; } = new List<IFieldEntry>();
        public AvrgMvvmBindingCollection CurrentBindingCollection { get; private set; }
        public AvrgMemberMetaDataSet CurrentMetaDataSet { get; private set; }

        /// <summary>
        /// 当前活跃的组容器字典
        /// </summary>
        private Dictionary<string, XuiAvrgMDGroup> _activeGroups = new Dictionary<string, XuiAvrgMDGroup>();

        [Tooltip("类型-预制体映射配置 ScriptableObject")]
        public AvrgTypePrefabMappingConfig typePrefabMappingConfig;

        #endregion

        #region 核心生命周期

        public void BuildFromMetaDataSet(AvrgMemberMetaDataSet metaDataSet,
            AvrgMvvmBindingCollection bindingCollection = null)
        {
            ClearCurrentEntries();
            CurrentMetaDataSet = metaDataSet;
            CurrentBindingCollection = bindingCollection;

            if (metaDataSet == null)
            {
                Debug.LogError("[Xuiavrg] 未指定元数据集，无法构建 Entry");
                return;
            }

            foreach (var metaData in metaDataSet.MetaDataList)
            {
                if (metaData.Hide)
                    continue;

                var entry = XuiFieldTempEntry.Get();
                entry.Initialize(
                    string.IsNullOrEmpty(metaData.GroupPath) ? metaData.MemberName : metaData.GroupPath,
                    metaData.Tag,
                    metaData.TagIndex);

                if (!string.IsNullOrEmpty(metaData.PrefabOverrideName))
                    entry.PrefabOverrideName = metaData.PrefabOverrideName;

                if (bindingCollection != null)
                {
                    var matchedBindings = bindingCollection.FindByMemberName(metaData.MemberName);
                    if (matchedBindings != null)
                    {
                        entry.AddBinding(matchedBindings, null);
                    }
                    else
                    {
                        foreach (var binding in bindingCollection)
                        {
                            entry.AddBinding(binding, null);
                            break;
                        }
                    }
                }

                entry.FieldOrder = metaData.FieldOrder;
                CurrentEntries.Add(entry);
            }

            OnPostBuildEntries(CurrentEntries);
            SortEntries();
        }

        public void SetBindingCollection(AvrgMvvmBindingCollection bindingCollection)
        {
            CurrentBindingCollection = bindingCollection;

            if (CurrentEntries != null && bindingCollection != null)
            {
                foreach (var entry in CurrentEntries)
                {
                    if (entry is XuiFieldTempEntry tempEntry)
                    {
                        var matched = bindingCollection.FindByMemberName(entry.EntryName);
                        if (matched != null)
                            tempEntry.AddBinding(matched, null);
                    }
                }
            }
        }

        protected virtual void OnPostBuildEntries(List<IFieldEntry> entries)
        {
        }

        private void SortEntries()
        {
            CurrentEntries.Sort((a, b) =>
            {
                int orderCompare = a.FieldOrder.CompareTo(b.FieldOrder);
                if (orderCompare != 0) return orderCompare;
                return string.Compare(a.EntryName, b.EntryName, StringComparison.Ordinal);
            });
        }

        // ============ 三阶段 GenerateUI ============

        /// <summary>
        /// 生成 UI — 三阶段流程
        /// Phase A: 按 GroupPath 分组
        /// Phase B: 创建 XuiAvrgMDGroup 组容器
        /// Phase C: 在组容器内生成 UI 组件并绑定
        /// </summary>
        public void GenerateUI()
        {
            if (CurrentEntries == null || CurrentEntries.Count == 0)
            {
                Debug.LogWarning("[Xuiavrg] 没有可生成的 Entry，请先调用 BuildFromMetaDataSet");
                return;
            }

            // Phase A: 收集并分组
            var groupedEntries = new Dictionary<string, List<IFieldEntry>>();
            var ungroupedEntries = new List<IFieldEntry>();

            foreach (var entry in CurrentEntries)
            {
                string groupKey = !string.IsNullOrEmpty(entry.EntryName)
                    ? GetGroupPathFromEntry(entry)
                    : null;

                if (!string.IsNullOrEmpty(groupKey))
                {
                    if (!groupedEntries.ContainsKey(groupKey))
                        groupedEntries[groupKey] = new List<IFieldEntry>();
                    groupedEntries[groupKey].Add(entry);
                }
                else
                {
                    ungroupedEntries.Add(entry);
                }
            }

            // Phase B: 创建组容器
            _activeGroups.Clear();
            foreach (var kvp in groupedEntries)
            {
                string groupPath = kvp.Key;
                var entries = kvp.Value;

                // 检查是否已创建（相同 GroupPath 只创建一个容器）
                if (_activeGroups.ContainsKey(groupPath)) continue;

                var groupGo = new GameObject($"Group [{groupPath}]", typeof(RectTransform));
                groupGo.transform.SetParent(transform, false);

                var rect = groupGo.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 1);
                rect.sizeDelta = new Vector2(0, 0);

                var group = groupGo.AddComponent<XuiAvrgMDGroup>();
                int firstOrder = entries.Count > 0 ? entries[0].FieldOrder : 0;
                group.Initialize(groupPath, firstOrder);

                _activeGroups[groupPath] = group;
            }

            // Phase C: 生成 UI 组件
            foreach (var kvp in groupedEntries)
            {
                string groupPath = kvp.Key;
                var entries = kvp.Value;

                if (!_activeGroups.TryGetValue(groupPath, out var group)) continue;

                foreach (var entry in entries)
                {
                    GenerateSingleUI(entry, group.contentRoot);
                }
            }

            foreach (var entry in ungroupedEntries)
            {
                GenerateSingleUI(entry, transform);
            }
        }

        /// <summary>
        /// 生成单个 Entry 的 UI
        /// </summary>
        private void GenerateSingleUI(IFieldEntry entry, Transform parent)
        {
            if (entry is not XuiFieldTempEntry tempEntry) return;

            var prefab = ResolvePrefab(entry);
            if (prefab == null)
            {
                Debug.LogWarning($"[Xuiavrg] 未找到 Entry '{entry.EntryName}' 的匹配预制体，跳过");
                return;
            }

            entry.BoundPrefab = prefab;
            var uiInstance = PrefabPoolGet(prefab);

            if (uiInstance == null) return;

            uiInstance.name = entry.EntryName;
            uiInstance.transform.SetParent(parent, false);

            // 检查 XUIAvrgMDValue 是否存在
            if (uiInstance.GetComponentInChildren<XUIAvrgMDValue>(true) == null)
            {
                Debug.LogError($"[Xuiavrg] 预制体 '{prefab.name}' 不符合生成要求：缺少 XUIAvrgMDValue 组件。请确保预制体根节点或其子节点包含 XUIAvrgMDValue 脚本。");
                PrefabPoolRelease(prefab, uiInstance);
                return;
            }

            entry.BindUIComponent(uiInstance, null);
            entry.OnEntryBind();
        }

        /// <summary>
        /// 从 Entry 提取 GroupPath（通过 EntryName 判断）
        /// </summary>
        private string GetGroupPathFromEntry(IFieldEntry entry)
        {
            // 通过 Bindings 中的元数据找到 GroupPath
            if (entry.Bindings != null && entry.Bindings.Count > 0)
            {
                var metaData = entry.Bindings[0].MetaData;
                if (metaData != null && !string.IsNullOrEmpty(metaData.GroupPath))
                    return metaData.GroupPath;
            }
            return null;
        }

        // ============ 预制体解析 ============

        private GameObject ResolvePrefab(IFieldEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.PrefabOverrideName))
            {
                if (typePrefabMappingConfig != null)
                {
                    var namedPrefab = typePrefabMappingConfig.FindPrefabByName(entry.PrefabOverrideName);
                    if (namedPrefab != null) return namedPrefab;
                }
            }

            if (typePrefabMappingConfig != null)
            {
                var typeMatch = typePrefabMappingConfig.FindPrefabByBindings(entry.Bindings);
                if (typeMatch != null) return typeMatch;
            }

            return typePrefabMappingConfig != null ? typePrefabMappingConfig.defaultPrefab : null;
        }

        // ============ 清理 ============

        public void ClearAll()
        {
            ClearCurrentEntries();
            ClearAllPrefabPools();
            ClearGroups();
            CurrentMetaDataSet = null;
            CurrentBindingCollection = null;
        }

        private void ClearCurrentEntries()
        {
            foreach (var entry in CurrentEntries)
            {
                if (entry.UIInstance != null && entry.BoundPrefab != null)
                {
                    PrefabPoolRelease(entry.BoundPrefab, entry.UIInstance);
                }

                if (entry is XuiFieldTempEntry tempEntry)
                {
                    XuiFieldTempEntry.Release(tempEntry);
                }
            }
            CurrentEntries.Clear();
        }

        private void ClearGroups()
        {
            foreach (var kvp in _activeGroups)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            _activeGroups.Clear();
        }

        private void OnDestroy()
        {
            ClearAll();
        }

        // ============ 兼容旧 API ============

        [Obsolete("Use BuildFromMetaDataSet + GenerateUI instead", false)]
        public void SchemaHydration(object payload)
        {
            var metaDataSet = XuiavrgUtilities.ExtractAvrgMemberMetaData(payload);
            var bindingCollection = metaDataSet.BuildMDVM(payload);
            BuildFromMetaDataSet(metaDataSet, bindingCollection);
            GenerateUI();
        }
        
        #endregion

    }
}
