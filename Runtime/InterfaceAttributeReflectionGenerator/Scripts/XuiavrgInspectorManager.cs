using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 节点下生成反射属性管理器
    /// </summary>
    /// <remarks>
    /// 管理器需要挂载在 Unity 节点中，管理器中指定的属性承载节点将用于生成属性内容。
    ///
    /// 新的生命周期流程：
    /// 1. BuildEntries(object payload) — 传入数据对象，构建 Entry 列表
    /// 2. OnPostBuildEntries(entries) — 虚方法，可重写添加额外 Entry
    /// 3. 自动按 FieldOrder 排序
    /// 4. GenerateUI() — 根据 Entry 列表生成 UI 实例并绑定
    ///
    /// 对象池说明：
    /// - Entry 通过 XuiFieldTempEntry.Pool 静态池管理
    /// - UI 预制体通过内部 PropertyGroupPools 字典管理，以 PropertyGroupScriptableObject 为键
    /// </remarks>
    public class XuiavrgInspectorManager : MonoBehaviour
    {
        #region 对象池

        /// <summary>
        /// UI 预制体对象池，以 PropertyGroupScriptableObject 为键
        /// </summary>
        private Dictionary<XuiavrgPropertyGroupScriptableObject, ObjectPool<GameObject>> _propertyGroupPools
            = new Dictionary<XuiavrgPropertyGroupScriptableObject, ObjectPool<GameObject>>();

        /// <summary>
        /// 从指定 PropertyGroup 的预制体池获取 UI 实例
        /// </summary>
        public GameObject PropertyGroupPoolGet(XuiavrgPropertyGroupScriptableObject group)
        {
            if (group == null) return null;

            if (!_propertyGroupPools.TryGetValue(group, out var pool))
            {
                var firstPrefab = group.FirstOrDefault()?.prefab;
                pool = new ObjectPool<GameObject>(
                    createFunc: () =>
                        firstPrefab != null
                            ? Object.Instantiate(firstPrefab, transform)
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
                _propertyGroupPools[group] = pool;
            }

            return pool.Get();
        }

        /// <summary>
        /// 将 UI 实例归还到指定 PropertyGroup 的预制体池
        /// </summary>
        public void PropertyGroupPoolRelease(XuiavrgPropertyGroupScriptableObject group, GameObject instance)
        {
            if (group == null || instance == null) return;
            if (_propertyGroupPools.TryGetValue(group, out var pool))
            {
                pool.Release(instance);
            }
            else
            {
                if (instance) Destroy(instance);
            }
        }

        /// <summary>
        /// 清空所有预制体对象池
        /// </summary>
        public void ClearAllPropertyGroupPools()
        {
            foreach (var pool in _propertyGroupPools.Values)
            {
                pool.Clear();
            }
            _propertyGroupPools.Clear();
        }

        #endregion

        #region 字段

        /// <summary>
        /// 当前活跃的 Entry 列表
        /// </summary>
        public List<IFieldEntry> CurrentEntries { get; private set; } = new List<IFieldEntry>();

        /// <summary>
        /// 当前传入的数据负载
        /// </summary>
        public object CurrentPayload { get; private set; }

        #endregion

        #region 核心生命周期

        /// <summary>
        /// 传入对象实例，构建 Entry 列表
        /// </summary>
        /// <param name="payload">数据端对象</param>
        public void BuildEntries(object payload)
        {
            // 清空之前的 Entry
            ClearCurrentEntries();
            CurrentPayload = payload;

            if (payload == null)
            {
                Debug.LogError("[Xuiavrg] 未指定有效负载，无法构建 Entry");
                return;
            }

            var type = payload.GetType();
            var memberInfos = type.GetRuntimeMemberInfo().ToList();

            // 用于合并编组 Entry 的临时字典
            var groupEntries = new Dictionary<string, XuiFieldTempEntry>();

            foreach (var memberInfo in memberInfos)
            {
                // 检查隐藏标记
                var hideAttr = memberInfo.GetCustomAttribute<XuiavrgHideAttribute>();
                if (hideAttr != null && hideAttr.Hide)
                    continue;

                // 检查编组标记
                var groupAttr = memberInfo.GetCustomAttribute<XuiavrgFieldEntryGroupAttribute>();

                if (groupAttr != null)
                {
                    // 编组字段：合并到同名 Entry
                    if (!groupEntries.TryGetValue(groupAttr.GroupPath, out var groupEntry))
                    {
                        groupEntry = XuiFieldTempEntry.Get();
                        groupEntry.Initialize(groupAttr.GroupPath);
                        groupEntries[groupAttr.GroupPath] = groupEntry;
                        CurrentEntries.Add(groupEntry);
                    }

                    groupEntry.RegisterBinding(
                        memberInfo.Name, memberInfo, payload,
                        groupAttr.Tag, groupAttr.TagIndex);
                }
                else
                {
                    // 普通字段：创建独立 Entry
                    var entry = XuiFieldTempEntry.Get();
                    entry.Initialize(memberInfo.Name);
                    entry.RegisterBinding(memberInfo.Name, memberInfo, payload);

                    // 检查预制体覆盖
                    var overrideAttr = memberInfo.GetCustomAttribute<XuiavrgFieldPrefabOverrideAttribute>();
                    if (overrideAttr != null)
                    {
                        entry.PrefabOverrideName = overrideAttr.PrefabGroupName;
                    }

                    CurrentEntries.Add(entry);
                }

                // 应用排序标记
                var orderAttr = memberInfo.GetCustomAttribute<XuiavrgFieldOrderAttribute>();
                if (orderAttr != null)
                {
                    // 找到该成员对应的 Entry（可能是编组 Entry）
                    if (groupAttr != null && groupEntries.TryGetValue(groupAttr.GroupPath, out var gEntry))
                    {
                        if (gEntry.FieldOrder == 0 || orderAttr.Order < gEntry.FieldOrder)
                            gEntry.FieldOrder = orderAttr.Order;
                    }
                    else
                    {
                        // 普通字段的排序应用到其 Entry
                        var normalEntry = CurrentEntries.LastOrDefault();
                        if (normalEntry != null)
                            normalEntry.FieldOrder = orderAttr.Order;
                    }
                }

                // 应用旧的 IXuiavrgAttribute（兼容）
                ApplyLegacyAttributes(memberInfo, payload, groupAttr, groupEntries);
            }

            // 调用后期回调
            OnPostBuildEntries(CurrentEntries);

            // 排序
            SortEntries();
        }

        /// <summary>
        /// 应用旧的 IXuiavrgAttribute 体系中的属性（兼容保留）
        /// </summary>
        private void ApplyLegacyAttributes(
            MemberInfo memberInfo, object payload,
            XuiavrgFieldEntryGroupAttribute groupAttr,
            Dictionary<string, XuiFieldTempEntry> groupEntries)
        {
            // 处理旧的 XuiavrgAttribute（设置 labelName, inspectorOrder 等）
            var oldAttr = memberInfo.GetCustomAttribute<XuiavrgAttribute>();
            if (oldAttr != null)
            {
                // 创建临时的 InspectorProperty 来接收绑定
                var tempProp = new XuiavrgInspectorProperty();
                tempProp.RegsiterDataValue(memberInfo, payload);
                oldAttr.BindProperty(tempProp);

                // 将相关属性同步到 Entry
                IFieldEntry targetEntry;
                if (groupAttr != null && groupEntries.TryGetValue(groupAttr.GroupPath, out var gEntry))
                    targetEntry = gEntry;
                else
                    targetEntry = CurrentEntries.LastOrDefault(e => !e.IsGroupEntry || e == null) ?? CurrentEntries.LastOrDefault();

                if (targetEntry != null)
                {
                    if (tempProp.inspectorOrder != 0 && targetEntry.FieldOrder == 0)
                        targetEntry.FieldOrder = tempProp.inspectorOrder;

                    targetEntry.PrefabOverrideName ??= tempProp.uiPropertyName;
                }
            }

            // 处理其他旧的验证 Attribute
            foreach (var attr in memberInfo.GetCustomAttributes().OfType<IXuiavrgAttribute>())
            {
                if (attr is XuiavrgAttribute) continue; // 已处理
                if (attr is XuiavrgHideAttribute) continue; // 已处理
                if (attr is XuiavrgFieldEntryGroupAttribute) continue; // 已处理
                if (attr is XuiavrgFieldOrderAttribute) continue; // 已处理
                if (attr is XuiavrgFieldPrefabOverrideAttribute) continue; // 已处理

                // 其他旧 Attribute（Required, StringCheck, NumberCheck 等）
                var tempProp = new XuiavrgInspectorProperty();
                tempProp.RegsiterDataValue(memberInfo, payload);
                attr.BindProperty(tempProp);
                // 这些验证信息目前由 InspectorProperty 保留，后续可迁移到 Entry
            }
        }

        /// <summary>
        /// 生成后期回调（可重写）
        /// 在此方法中可以手动添加额外的 Entry（如 Preview 结构、不在数据对象中的内容）
        /// </summary>
        /// <param name="entries">当前 Entry 列表</param>
        protected virtual void OnPostBuildEntries(List<IFieldEntry> entries)
        {
            // 默认无操作，子类可重写
        }

        /// <summary>
        /// 对 Entry 列表按 FieldOrder 排序
        /// </summary>
        private void SortEntries()
        {
            CurrentEntries.Sort((a, b) =>
            {
                int orderCompare = a.FieldOrder.CompareTo(b.FieldOrder);
                if (orderCompare != 0) return orderCompare;
                return string.Compare(a.EntryName, b.EntryName, StringComparison.Ordinal);
            });
        }

        /// <summary>
        /// 根据 Entry 列表生成 UI
        /// </summary>
        public void GenerateUI()
        {
            if (CurrentEntries == null || CurrentEntries.Count == 0)
            {
                Debug.LogWarning("[Xuiavrg] 没有可生成的 Entry，请先调用 BuildEntries");
                return;
            }

            foreach (var entry in CurrentEntries)
            {
                if (entry is not XuiFieldTempEntry tempEntry) continue;

                // 查找匹配的 PropertyGroup
                var group = ResolvePropertyGroup(entry);
                if (group == null)
                {
                    Debug.LogWarning($"[Xuiavrg] 未找到 Entry '{entry.EntryName}' 的匹配预制体组，跳过");
                    continue;
                }

                entry.BoundPrefabGroup = group;

                // 从对象池获取 UI 实例
                var uiInstance = PropertyGroupPoolGet(group);
                if (uiInstance == null) continue;

                // 设置父节点和命名
                uiInstance.name = entry.EntryName;
                uiInstance.transform.SetParent(transform, false);

                // 绑定 UI 组件（首次绑定不传缓存，由 Entry 内部收集）
                entry.BindUIComponent(uiInstance, null);

                // 绑定事件
                entry.OnEntryBind();
            }
        }

        /// <summary>
        /// 解析 Entry 对应的 PropertyGroup
        /// 优先使用 PrefabOverrideName，否则按 Tag 匹配
        /// </summary>
        private XuiavrgPropertyGroupScriptableObject ResolvePropertyGroup(IFieldEntry entry)
        {
            // 1. 优先检查 PrefabOverrideName
            if (!string.IsNullOrEmpty(entry.PrefabOverrideName))
            {
                var overrideGroup = XuiavrgPropertyGroupScriptableObject.FindGroupByName(entry.PrefabOverrideName);
                if (overrideGroup != null) return overrideGroup;
            }

            // 2. 按 Tag 查找匹配的组
            if (entry.Tag != XuiavrgFieldTag.None)
            {
                var tags = new List<XuiavrgFieldTag> { entry.Tag };
                // 收集子 Entry 的 Tag（如果是编组 Entry）
                if (entry.IsGroupEntry)
                {
                    foreach (var child in entry.Children)
                    {
                        if (child.Tag != XuiavrgFieldTag.None && !tags.Contains(child.Tag))
                            tags.Add(child.Tag);
                    }
                }

                var matchedGroup = XuiavrgPropertyGroupScriptableObject.FindGroupByTags(tags.ToArray());
                if (matchedGroup != null) return matchedGroup;
            }

            // 3. 回退到通用匹配
            return XuiavrgPropertyGroupScriptableObject.FindGroupByName("Default");
        }

        /// <summary>
        /// 清空当前界面和 Entry 列表
        /// </summary>
        public void ClearAll()
        {
            ClearCurrentEntries();
            ClearAllPropertyGroupPools();
            CurrentPayload = null;
        }

        /// <summary>
        /// 清空当前 Entry 列表（归还到池中）
        /// </summary>
        private void ClearCurrentEntries()
        {
            foreach (var entry in CurrentEntries)
            {
                // 释放 UI 实例
                if (entry.UIInstance != null && entry.BoundPrefabGroup != null)
                {
                    PropertyGroupPoolRelease(entry.BoundPrefabGroup, entry.UIInstance);
                }

                // 归还 Entry 到池
                if (entry is XuiFieldTempEntry tempEntry)
                {
                    XuiFieldTempEntry.Release(tempEntry);
                }
            }
            CurrentEntries.Clear();
        }

        #endregion

        #region 生命周期

        private void OnDestroy()
        {
            ClearAll();
        }

        #endregion

        #region 兼容旧 API

        /// <summary>
        /// [已弃用] 请使用 BuildEntries + GenerateUI 替代
        /// </summary>
        [Obsolete("Use BuildEntries + GenerateUI instead", false)]
        public void SchemaHydration(object payload)
        {
            BuildEntries(payload);
            GenerateUI();
        }

        #endregion
    }
}
