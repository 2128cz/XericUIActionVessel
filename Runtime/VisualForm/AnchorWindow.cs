using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XericUI.Core;

namespace XericUI.VisualForm
{
    /// <summary>
    /// 锚点窗口
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.AnchorWindow + "/AnchorWindow")]
    public class AnchorWindowByDefaultEntry : AnchorWindow<WindowEntry> { }
    
    /// <summary>
    /// 锚点窗口 - WorldAnchor的子类，持有一组UI窗口条目的引用。
    /// 每个条目包含窗口对象、屏幕偏移量和激活状态，支持一个锚点同时生成多个定位窗口。
    /// 窗口对象可以是实际的场景对象，也可以是预制体。
    /// </summary>
    public abstract class AnchorWindow<T> : WorldAnchor, IAnchorWindowEntry<T>
        where T : WindowEntry
    {
        [Header("窗口条目")]
        [SerializeField, Tooltip("窗口对象集合，每个条目可独立配置偏移和启用状态")]
        private List<T> m_WindowEntries = new List<T>();

        public IList<T> WindowEntries => m_WindowEntries;

        public IEnumerable<WindowEntry> DefaultTypeWindowEntries => m_WindowEntries;

        public int DefaultTypeWindowEntriesCount => m_WindowEntries.Count;

        /// <summary>
        /// 当前生效的窗口条目数（过滤掉WindowObject为null的条目）
        /// </summary>
        public int ValidEntryCount
        {
            get
            {
                int count = 0;
                foreach (var entry in m_WindowEntries)
                    if (entry != null && entry.WindowObject != null)
                        count++;
                return count;
            }
        }

        /// <summary>
        /// 获取第一个有效窗口对象的Transform
        /// </summary>
        [Obsolete("旧api")]
        public Transform GetWindowTransform()
        {
            foreach (var entry in m_WindowEntries)
            {
                if (entry != null && entry.WindowObject != null)
                    return entry.WindowObject.transform;
            }
            return transform;
        }

        #region 工具方法

        /// <summary>
        /// 设置指定索引条目的启用状态。写入后自动检查所有条目，
        /// 若全部禁用则同步标记到 WindowState（坞将跳过该窗口的位置更新）。
        /// </summary>
        /// <param name="index">条目索引</param>
        /// <param name="enabled">是否启用</param>
        public void SetEntryEnabled(int index, bool enabled)
        {
            if (index < 0 || index >= m_WindowEntries.Count) return;
            SetEntryEnabled(m_WindowEntries[index], enabled);
        }

        /// <summary>
        /// 直接通过条目引用设置启用状态。无需索引查找。
        /// </summary>
        /// <param name="entry">条目引用</param>
        /// <param name="enabled">是否启用</param>
        public void SetEntryEnabled(T entry, bool enabled)
        {
            if (entry == null) return;
            entry.SetEnabledInternal(enabled);
            var state = GetWindowState();
            state?.RefreshFlags();
            state?.ApplyEntryEnabled(entry, enabled);
        }

        public void SetAllEntryEnabled(bool enabled)
        {
            for (int i = 0; i < m_WindowEntries.Count; i++)
                m_WindowEntries[i].SetEnabledInternal(enabled);
            GetWindowState()?.RefreshFlags();
        }
        
        /// <summary>
        /// 设置指定索引条目的钳制到安全区标记。写入后自动同步 WindowState 的 HasAnyClamp。
        /// </summary>
        /// <param name="index">条目索引</param>
        /// <param name="clamp">是否钳制</param>
        public void SetEntryClampToSafeZone(int index, bool clamp)
        {
            if (index < 0 || index >= m_WindowEntries.Count) return;
            SetEntryClampToSafeZone(m_WindowEntries[index], clamp);
        }

        /// <summary>
        /// 直接通过条目引用设置安全区钳制标记。无需索引查找。
        /// </summary>
        /// <param name="entry">条目引用</param>
        /// <param name="clamp">是否钳制</param>
        public void SetEntryClampToSafeZone(T entry, bool clamp)
        {
            if (entry == null) return;
            entry.SetClampToSafeZoneInternal(clamp);
            GetWindowState()?.RefreshFlags();
        }

        /// <summary>
        /// 设置指定索引条目的碰撞参与标记。写入后自动同步 WindowState 的 HasAnyCollisionEntry。
        /// 锚点坞在启用碰撞计算时会对标记为参与碰撞的条目进行屏幕空间碰撞分离。
        /// </summary>
        /// <param name="index">条目索引</param>
        /// <param name="enable">是否参与碰撞</param>
        public void SetEntryEnableCollision(int index, bool enable)
        {
            if (index < 0 || index >= m_WindowEntries.Count) return;
            SetEntryEnableCollision(m_WindowEntries[index], enable);
        }

        /// <summary>
        /// 直接通过条目引用设置碰撞参与标记。无需索引查找。
        /// </summary>
        /// <param name="entry">条目引用</param>
        /// <param name="enable">是否参与碰撞</param>
        public void SetEntryEnableCollision(T entry, bool enable)
        {
            if (entry == null) return;
            entry.SetEnableCollisionInternal(enable);
            GetWindowState()?.RefreshFlags();
        }

        /// <summary>
        /// 设置指定索引条目的排序顺序。Order 越小越在下层，越大越在上层。
        /// 自动通知 WindowState 重新排列实例的层级顺序。
        /// </summary>
        /// <param name="index">条目索引</param>
        /// <param name="order">排序值</param>
        public void SetEntryOrder(int index, int order)
        {
            if (index < 0 || index >= m_WindowEntries.Count) return;
            SetEntryOrder(m_WindowEntries[index], order);
        }

        /// <summary>
        /// 直接通过条目引用设置排序顺序。无需索引查找。
        /// 自动通知 WindowState 重新排列实例的层级顺序。
        /// </summary>
        /// <param name="entry">条目引用</param>
        /// <param name="order">排序值</param>
        public void SetEntryOrder(T entry, int order)
        {
            if (entry == null) return;
            entry.SetOrderInternal(order);
            GetWindowState()?.ApplyEntryOrder();
        }

        /// <summary>
        /// 从所属锚点坞获取自己的中间窗口状态类
        /// </summary>
        public WindowState GetWindowState()
        {
            if (TargetDock == null) return null;
            return TargetDock.GetWindowState(this);
        }

        /// <summary>
        /// 获取第一个有效窗口的实例对象
        /// </summary>
        public GameObject GetWindowInstance()
        {
            var state = GetWindowState();
            if (state == null || state.Entries == null || state.Entries.Count == 0)
                return null;
            return state.Entries[0].Instance;
        }

        /// <summary>
        /// 获取所有窗口实例对象
        /// </summary>
        public List<GameObject> GetAllWindowInstances()
        {
            var result = new List<GameObject>();
            var state = GetWindowState();
            if (state == null || state.Entries == null) return result;

            foreach (var e in state.Entries)
                if (e.Instance != null)
                    result.Add(e.Instance);
            return result;
        }

        /// <summary>
        /// 获取第一个窗口当前的屏幕坐标（容器内本地坐标）
        /// </summary>
        public Vector2 GetScreenPosition()
        {
            var instance = GetWindowInstance();
            if (instance == null) return Vector2.zero;

            var rt = instance.transform as RectTransform;
            if (rt != null) return rt.localPosition;
            return instance.transform.localPosition;
        }

        /// <summary>
        /// 获取锚点当前的世界坐标
        /// </summary>
        public Vector3 GetWorldPosition()
        {
            var state = GetWindowState();
            if (state != null) return state.LastWorldPosition;
            return CachedTransform.position;
        }

        /// <summary>
        /// 检查窗口实例是否处于活动状态
        /// </summary>
        public bool IsWindowActive()
        {
            var state = GetWindowState();
            return state != null && state.IsEnabled;
        }

        #endregion

        #region 生命周期事件

        /// <summary>
        /// 窗口层级变更事件 — 当某个条目的溢出状态发生变化时触发。
        /// 继承类可重写此方法，根据溢出状态切换不同窗口显示（如溢出后切换为指向示意器）。
        /// </summary>
        /// <param name="entryIndex">发生变化的条目索引</param>
        /// <param name="state">新的溢出状态</param>
        public virtual void OnWindowLevelChange(int entryIndex, OverflowState state) { }

        #endregion
    }
}
