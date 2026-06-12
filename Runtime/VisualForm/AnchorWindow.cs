using System.Collections.Generic;
using UnityEngine;

namespace XericUI.VisualForm
{
    /// <summary>
    /// 锚点窗口 - WorldAnchor的子类，持有一组UI窗口条目的引用。
    /// 每个条目包含窗口对象、屏幕偏移量和激活状态，支持一个锚点同时生成多个定位窗口。
    /// 窗口对象可以是实际的场景对象，也可以是预制体。
    /// </summary>
    [AddComponentMenu("Xeric UI Vessel/AnchorWindow/AnchorWindow")]
    public class AnchorWindow : WorldAnchor
    {
        [Header("窗口条目")]
        [SerializeField, Tooltip("窗口对象集合，每个条目可独立配置偏移和启用状态")]
        private List<WindowEntry> m_WindowEntries = new List<WindowEntry>();

        /// <summary>
        /// 窗口条目列表
        /// </summary>
        public List<WindowEntry> WindowEntries => m_WindowEntries;

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
        /// 获取第一个有效窗口对象的Transform（兼容旧API）
        /// </summary>
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
    }
}
