using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 节点下生成反射属性管理器 — MVM (Metadata-Driven View Model)
    /// 负责：构建 Entry → 创建组容器（树状嵌套）→ 生成 UI → 绑定数据
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
        /// 编组叶节点字典 — 完整 GroupPath → 对应层级的 XuiAvrgMDGroup
        /// </summary>
        private Dictionary<string, XuiAvrgMDGroup> _leafGroupMap = new Dictionary<string, XuiAvrgMDGroup>();

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

        // ============ 四阶段 GenerateUI ============

        /// <summary>
        /// 生成 UI — 四阶段流程
        /// Phase A: 按 GroupPath 分组
        /// Phase B: 构建路径树，创建嵌套的 XuiAvrgMDGroup 容器
        /// Phase C: 在叶节点容器内生成 UI 组件并绑定
        /// Phase D: 处理非编组 Entry 直接放进根目录
        /// </summary>
        public void GenerateUI()
        {
            if (CurrentEntries == null || CurrentEntries.Count == 0)
            {
                Debug.LogWarning("[Xuiavrg] 没有可生成的 Entry，请先调用 BuildFromMetaDataSet");
                return;
            }

            // Phase A: 按完整 GroupPath 分组
            var groupedEntries = new Dictionary<string, List<IFieldEntry>>();
            var ungroupedEntries = new List<IFieldEntry>();

            foreach (var entry in CurrentEntries)
            {
                string groupPath = GetGroupPathFromEntry(entry);

                if (!string.IsNullOrEmpty(groupPath))
                {
                    if (!groupedEntries.ContainsKey(groupPath))
                        groupedEntries[groupPath] = new List<IFieldEntry>();
                    groupedEntries[groupPath].Add(entry);
                }
                else
                {
                    ungroupedEntries.Add(entry);
                }
            }

            // Phase B: 构建树状嵌套组容器
            _leafGroupMap.Clear();
            BuildGroupTree(groupedEntries.Keys);

            // Phase C: 在叶节点容器内生成 UI
            foreach (var kvp in groupedEntries)
            {
                string groupPath = kvp.Key;
                var entries = kvp.Value;

                if (!_leafGroupMap.TryGetValue(groupPath, out var leafGroup)) continue;

                foreach (var entry in entries)
                {
                    GenerateSingleUI(entry, leafGroup.contentRoot);
                }
            }

            // Phase D: 非编组 Entry 直接放进根
            foreach (var entry in ungroupedEntries)
            {
                GenerateSingleUI(entry, transform);
            }
        }

        // ============ 树状编组容器构建 ============

        private void BuildGroupTree(IEnumerable<string> allGroupPaths)
        {
            var rootNode = new GroupTreeNode(null);

            foreach (var fullPath in allGroupPaths)
            {
                if (string.IsNullOrEmpty(fullPath)) continue;

                var segments = fullPath.Split('/');
                var current = rootNode;

                for (int i = 0; i < segments.Length; i++)
                {
                    string seg = segments[i];
                    if (!current.Children.ContainsKey(seg))
                        current.Children[seg] = new GroupTreeNode(seg);
                    current = current.Children[seg];
                }
            }

            CreateGroupGameObjects(rootNode, null, transform, "");
        }

        private void CreateGroupGameObjects(
            GroupTreeNode node,
            XuiAvrgMDGroup parentGroup,
            Transform parentTransform,
            string accumulatedPath)
        {
            foreach (var kvp in node.Children)
            {
                string segName = kvp.Key;
                var childNode = kvp.Value;

                string childFullPath = string.IsNullOrEmpty(accumulatedPath)
                    ? segName
                    : accumulatedPath + "/" + segName;

                var groupGo = new GameObject($"Group [{childFullPath}]", typeof(RectTransform));
                groupGo.transform.SetParent(parentTransform, false);

                var rect = groupGo.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 1);
                rect.sizeDelta = new Vector2(0, 0);

                var group = groupGo.AddComponent<XuiAvrgMDGroup>();
                group.Initialize(childFullPath, 0, parentGroup);

                if (childNode.Children.Count == 0)
                {
                    _leafGroupMap[childFullPath] = group;
                }

                CreateGroupGameObjects(childNode, group, group.contentRoot, childFullPath);
            }
        }

        private class GroupTreeNode
        {
            public string SegmentName { get; }
            public Dictionary<string, GroupTreeNode> Children { get; }

            public GroupTreeNode(string name)
            {
                SegmentName = name;
                Children = new Dictionary<string, GroupTreeNode>();
            }
        }

        // ============ 生成单个 UI ============

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

            if (uiInstance.GetComponentInChildren<XUIAvrgMDValue>(true) == null)
            {
                Debug.LogError(
                    $"[Xuiavrg] 预制体 '{prefab.name}' 不符合生成要求：缺少 XUIAvrgMDValue 组件。请确保预制体根节点或其子节点包含 XUIAvrgMDValue 脚本。");
                PrefabPoolRelease(prefab, uiInstance);
                return;
            }

            entry.BindUIComponent(uiInstance, null);
            entry.OnEntryBind();
        }

        private string GetGroupPathFromEntry(IFieldEntry entry)
        {
            if (entry.Bindings != null && entry.Bindings.Count > 0)
            {
                var metaData = entry.Bindings[0].MetaData;
                if (metaData != null && !string.IsNullOrEmpty(metaData.GroupPath))
                    return metaData.GroupPath;
            }

            return null;
        }

        // ============ 预制体解析 (Tag + 类型匹配) ============

        /// <summary>
        /// 预制体解析优先级：
        /// 1. Tag != None → 按 Tag + 类型匹配（无匹配时报错）
        /// 2. Tag == None → 按类型匹配
        /// 3. defaultPrefab 回退
        /// PrefabOverrideName 已移除，由 Tag + 类型匹配替代
        /// </summary>
        private GameObject ResolvePrefab(IFieldEntry entry)
        {
            if (typePrefabMappingConfig == null)
            {
                Debug.LogError("[Xuiavrg] 未配置 typePrefabMappingConfig");
                return null;
            }

            var bindings = entry.Bindings;
            var valueType = bindings?.Count > 0 ? bindings[0].MetaData?.ValueType : null;

            // 优先级 1: Tag != None → Tag + 类型匹配
            if (entry.Tag != XuiavrgFieldTag.None && valueType != null)
            {
                var taggedPrefab = typePrefabMappingConfig.FindPrefabByTypeAndTag(valueType, entry.Tag);
                if (taggedPrefab != null)
                    return taggedPrefab;

                Debug.LogError(
                    $"[Xuiavrg] 指定 Tag '{entry.Tag}' 的预制体不存在 " +
                    $"(Entry: '{entry.EntryName}', 类型: {valueType.Name})，跳过 UI 生成");
                return null;
            }

            // 优先级 2: Tag == None → 按类型匹配
            if (valueType != null)
            {
                var typeMatch = typePrefabMappingConfig.FindPrefabByType(valueType);
                if (typeMatch != null)
                    return typeMatch;
            }

            // 优先级 3: defaultPrefab 回退
            if (typePrefabMappingConfig.defaultPrefab != null)
            {
                Debug.LogWarning(
                    $"[Xuiavrg] 类型 '{valueType?.Name ?? "未知"}' 无精确匹配，使用默认预制体 '{typePrefabMappingConfig.defaultPrefab.name}'");
                return typePrefabMappingConfig.defaultPrefab;
            }

            Debug.LogError(
                $"[Xuiavrg] 类型 '{valueType?.Name ?? "未知"}' 无匹配预制体，且未配置 defaultPrefab");
            return null;
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
            foreach (var kvp in _leafGroupMap)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }

            _leafGroupMap.Clear();
        }

        private void OnDestroy()
        {
            ClearAll();
        }
    }

    #endregion
}