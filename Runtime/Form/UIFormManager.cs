using System.Collections.Generic;
using UnityEngine;

namespace XericUI.Form
{
    /// <summary>
    /// UI 窗体管理器 — 窗口管理中心。挂载在 Canvas 下作为所有窗口的父容器。
    /// 通过 Entry 驱动管理窗口生命周期、层级排序、委托事件发布。
    /// 支持任务栏和边缘触发隐藏窗口。
    /// </summary>
    [AddComponentMenu("Xeric UI Vessel/Form/UI Form Manager")]
    [DefaultExecutionOrder(32000)]
    public class UIFormManager : MonoBehaviour
    {
        [Header("边缘触发")]
        [SerializeField, Tooltip("边缘触发区域大小")]
        private Vector2 m_EdgeTriggerSize = new Vector2(80, 60);

        [Header("调试")]
        [SerializeField, Tooltip("输出事件日志")]
        private bool m_EnableDebugLog;

        #region === 委托事件 ===

        /// <summary>窗口创建</summary>
        public event WindowEntryEventHandler OnWindowCreated;

        /// <summary>窗口销毁</summary>
        public event WindowEntryEventHandler OnWindowDestroyed;

        /// <summary>窗口状态变更</summary>
        public event WindowEntryEventHandler OnWindowStateChanged;

        /// <summary>窗口获得焦点（置顶）</summary>
        public event WindowEntryEventHandler OnWindowFocused;

        #endregion

        #region === 内部状态 ===

        private readonly Dictionary<string, WindowEntry> _entries = new Dictionary<string, WindowEntry>();
        private readonly List<WindowEntry> _orderedEntries = new List<WindowEntry>();
        private string _focusedEntryId;

        // 边缘隐藏（按方向分组）
        private readonly Dictionary<AutoHideDirection, List<WindowEntry>> _edgeHidden = new()
        {
            { AutoHideDirection.Left, new List<WindowEntry>() },
            { AutoHideDirection.Right, new List<WindowEntry>() },
            { AutoHideDirection.Up, new List<WindowEntry>() },
            { AutoHideDirection.Down, new List<WindowEntry>() },
        };
        private AutoHideDirection? _lastEdgeTrigger;

        #endregion

        #region === 生命周期 ===

        protected virtual void Update()
        {
            CheckEdgeTriggers();
        }

        #endregion

        #region === 程序化窗口创建 / 销毁 ===

        /// <summary>程序化创建一个窗口实例并返回其 Entry</summary>
        public WindowEntry CreateWindow(RectTransform prefab, string title = null, Sprite icon = null)
        {
            if (prefab == null)
            {
                Debug.LogError("[UIFormManager] CreateWindow: prefab 为空", this);
                return null;
            }

            var instance = Instantiate(prefab, transform);
            instance.name = title ?? prefab.name;

            var controller = instance.GetComponent<UIFormController>();
            var entry = new WindowEntry
            {
                Title = title ?? prefab.name,
                Icon = icon,
                Prefab = prefab,
                Controller = controller,
                State = WindowState.Normal,
            };

            if (controller != null)
                controller.Entry = entry;

            _entries[entry.Id] = entry;
            _orderedEntries.Add(entry);

            MoveToFront(entry.Id);

            NotifyCreated(entry);

            if (m_EnableDebugLog)
                Debug.Log($"[UIFormManager] 创建窗口: {entry.Title} (ID={entry.Id})");

            return entry;
        }

        /// <summary>销毁窗口及其所有子窗口</summary>
        public void DestroyWindow(string entryId)
        {
            if (!_entries.TryGetValue(entryId, out var entry)) return;

            // 级联关闭子窗口
            CascadeCloseChildren(entry);

            UnregisterEdgeHiddenForEntry(entry);

            _orderedEntries.Remove(entry);
            _entries.Remove(entryId);

            if (_focusedEntryId == entryId)
                _focusedEntryId = null;

            var controller = entry.Controller;
            entry.Controller = null;

            NotifyDestroyed(entry);

            if (controller != null && Application.isPlaying)
                Destroy(controller.gameObject);
        }

        private void CascadeCloseChildren(WindowEntry entry)
        {
            for (int i = entry.ChildEntries.Count - 1; i >= 0; i--)
            {
                var child = entry.ChildEntries[i];
                DestroyWindow(child.Id);
            }
        }

        #endregion

        #region === 显示控制 ===

        /// <summary>显示窗口并置顶</summary>
        public void ShowWindow(string entryId)
        {
            if (!TryGetEntry(entryId, out var entry)) return;
            if (entry.Controller == null) return;

            entry.Controller.gameObject.SetActive(true);
            MoveToFront(entryId);
        }

        /// <summary>隐藏窗口</summary>
        public void HideWindow(string entryId)
        {
            if (!TryGetEntry(entryId, out var entry)) return;
            if (entry.Controller == null) return;

            entry.Controller.gameObject.SetActive(false);
        }

        /// <summary>切换窗口显示/隐藏</summary>
        public void ToggleWindow(string entryId)
        {
            if (!TryGetEntry(entryId, out var entry)) return;
            if (entry.Controller == null) return;

            if (entry.State == WindowState.Minimized)
            {
                RestoreWindow(entryId);
            }
            else if (entry.Controller.gameObject.activeSelf)
            {
                MinimizeWindow(entryId);
            }
            else
            {
                ShowWindow(entryId);
            }
        }

        /// <summary>将窗口置顶</summary>
        public void MoveToFront(string entryId)
        {
            if (!TryGetEntry(entryId, out var entry)) return;
            if (entry.Controller == null) return;

            entry.Controller.transform.SetAsLastSibling();

            _orderedEntries.Remove(entry);
            _orderedEntries.Add(entry);

            _focusedEntryId = entryId;

            NotifyFocused(entry);
        }

        #endregion

        #region === 最小化 / 恢复 ===

        /// <summary>最小化窗口</summary>
        public void MinimizeWindow(string entryId)
        {
            if (!TryGetEntry(entryId, out var entry)) return;
            if (entry.Controller == null) return;
            if (entry.State == WindowState.Minimized) return;

            entry.Controller.gameObject.SetActive(false);
            entry.State = WindowState.Minimized;

            NotifyStateChanged(entry);
        }

        /// <summary>恢复已最小化的窗口</summary>
        public void RestoreWindow(string entryId)
        {
            if (!TryGetEntry(entryId, out var entry)) return;
            if (entry.Controller == null) return;
            if (entry.State != WindowState.Minimized) return;

            ShowWindow(entryId);
            entry.State = WindowState.Normal;

            NotifyStateChanged(entry);
        }

        public void ToggleMaximize(string entryId)
        {
            if (!TryGetEntry(entryId, out var entry)) return;
            if (entry.Controller == null) return;

            entry.Controller.ToggleMaximize();
            NotifyStateChanged(entry);
        }

        #endregion

        #region === 查询 ===

        public WindowEntry GetEntry(string id)
        {
            _entries.TryGetValue(id, out var entry);
            return entry;
        }

        public IReadOnlyList<WindowEntry> GetAllEntries() => _orderedEntries;

        public WindowEntry GetFocusedEntry()
        {
            if (string.IsNullOrEmpty(_focusedEntryId)) return null;
            return GetEntry(_focusedEntryId);
        }

        public int EntryCount => _entries.Count;

        #endregion

        #region === 边缘隐藏 ===

        public void RegisterEdgeHidden(WindowEntry entry, AutoHideDirection direction)
        {
            if (entry == null) return;
            var list = _edgeHidden[direction];
            if (!list.Contains(entry))
                list.Add(entry);
        }

        public void UnregisterEdgeHidden(WindowEntry entry)
        {
            if (entry == null) return;
            foreach (var list in _edgeHidden.Values)
                list.Remove(entry);
        }

        private void UnregisterEdgeHiddenForEntry(WindowEntry entry)
        {
            UnregisterEdgeHidden(entry);
        }

        private void CheckEdgeTriggers()
        {
            Vector2 mousePos = Input.mousePosition;
            float sw = Screen.width;
            float sh = Screen.height;

            bool leftTrigger = mousePos.x < m_EdgeTriggerSize.x;
            bool rightTrigger = mousePos.x > (sw - m_EdgeTriggerSize.x);
            bool bottomTrigger = mousePos.y < m_EdgeTriggerSize.y;
            bool topTrigger = mousePos.y > (sh - m_EdgeTriggerSize.y);

            AutoHideDirection? activeTrigger = null;
            if (leftTrigger) activeTrigger = AutoHideDirection.Left;
            else if (rightTrigger) activeTrigger = AutoHideDirection.Right;
            else if (bottomTrigger) activeTrigger = AutoHideDirection.Down;
            else if (topTrigger) activeTrigger = AutoHideDirection.Up;

            if (activeTrigger.HasValue && activeTrigger != _lastEdgeTrigger)
            {
                ShowEdgeWindows(activeTrigger.Value);
            }

            if (!activeTrigger.HasValue)
                _lastEdgeTrigger = null;
        }

        public void ShowEdgeWindows(AutoHideDirection direction)
        {
            _lastEdgeTrigger = direction;
            var list = _edgeHidden[direction];

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var entry = list[i];
                if (entry?.Controller == null)
                {
                    list.RemoveAt(i);
                    continue;
                }
                entry.Controller.SlideInFromEdge();
            }
        }

        #endregion

        #region === 内部通知 ===

        private void NotifyCreated(WindowEntry entry) => OnWindowCreated?.Invoke(entry);
        private void NotifyDestroyed(WindowEntry entry) => OnWindowDestroyed?.Invoke(entry);
        private void NotifyStateChanged(WindowEntry entry) => OnWindowStateChanged?.Invoke(entry);
        private void NotifyFocused(WindowEntry entry) => OnWindowFocused?.Invoke(entry);

        #endregion

        #region === 工具 ===

        private bool TryGetEntry(string id, out WindowEntry entry)
        {
            if (string.IsNullOrEmpty(id))
            {
                entry = null;
                return false;
            }
            return _entries.TryGetValue(id, out entry);
        }

        #endregion
    }
}
