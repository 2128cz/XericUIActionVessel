using System.Collections.Generic;
using UnityEngine;

namespace XericUI.Form
{
    /// <summary>
    /// UI 窗体管理器 — 窗口管理中心。
    /// 挂载在 Canvas 所在 GameObject 上（或通过代码自动创建），
    /// 管理所有窗口的注册、最小化恢复、边缘触发隐藏窗口的显隐调度。
    /// </summary>
    [AddComponentMenu("Xeric UI Vessel/Form/UI Form Manager")]
    [DefaultExecutionOrder(32000)]
    public class UIFormManager : MonoBehaviour
    {
        [Header("边缘触发")]
        [SerializeField, Tooltip("边缘触发区域大小（x=宽度方向，y=高度方向），鼠标进入此区域时触发对应方向隐藏窗口的显示")]
        private Vector2 m_EdgeTriggerSize = new Vector2(80, 60);

        [Header("调试")]
        [SerializeField, Tooltip("输出事件日志")]
        private bool m_EnableDebugLog;

        #region 静态单例

        private static UIFormManager s_Main;

        /// <summary>全局单例引用</summary>
        public static UIFormManager Main => s_Main;

        #endregion

        #region 内部状态

        // 所有窗口
        private readonly List<UIFormController> _windows = new List<UIFormController>();

        // 最小化窗口数据
        private struct MinimizedData
        {
            public Vector2 anchoredPosition;
            public Vector2 sizeDelta;
            public Vector2 anchorMin;
            public Vector2 anchorMax;
        }
        private readonly Dictionary<UIFormController, MinimizedData> _minimized = new();

        // 边缘隐藏窗口（按方向分组）
        private readonly Dictionary<AutoHideDirection, List<UIFormController>> _edgeHidden = new()
        {
            { AutoHideDirection.Left, new List<UIFormController>() },
            { AutoHideDirection.Right, new List<UIFormController>() },
            { AutoHideDirection.Up, new List<UIFormController>() },
            { AutoHideDirection.Down, new List<UIFormController>() },
        };

        // 边缘触发状态跟踪（避免重复触发）
        private AutoHideDirection? _lastEdgeTrigger;

        #endregion

        #region 生命周期

        protected virtual void Awake()
        {
            if (s_Main != null && s_Main != this)
            {
                Debug.LogWarning($"[UIFormManager] 已存在单例，销毁重复实例: {name}", this);
                Destroy(this);
                return;
            }

            s_Main = this;
        }

        protected virtual void OnEnable()
        {
            if (s_Main == null) s_Main = this;
        }

        protected virtual void OnDisable()
        {
            if (s_Main == this) s_Main = null;
        }

        protected virtual void OnDestroy()
        {
            if (s_Main == this) s_Main = null;
            _windows.Clear();
            _minimized.Clear();
            foreach (var list in _edgeHidden.Values) list.Clear();
        }

        protected virtual void Update()
        {
            CheckEdgeTriggers();
        }

        #endregion

        #region === 窗口注册 / 注销 ===

        public void RegisterWindow(UIFormController window)
        {
            if (window == null || _windows.Contains(window)) return;
            _windows.Add(window);

            if (m_EnableDebugLog)
                Debug.Log($"[UIFormManager] 注册窗口: {window.name}");
        }

        public void UnregisterWindow(UIFormController window)
        {
            if (window == null) return;
            _windows.Remove(window);
            UnregisterEdgeHidden(window);
            _minimized.Remove(window);

            if (m_EnableDebugLog)
                Debug.Log($"[UIFormManager] 注销窗口: {window.name}");
        }

        #endregion

        #region === 边缘隐藏窗口管理 ===

        /// <summary>注册边缘隐藏窗口</summary>
        public void RegisterEdgeHidden(UIFormController window, AutoHideDirection direction)
        {
            if (window == null) return;

            var list = _edgeHidden[direction];
            if (!list.Contains(window))
                list.Add(window);

            if (m_EnableDebugLog)
                Debug.Log($"[UIFormManager] 边缘隐藏注册: {window.name} (方向={direction})");
        }

        /// <summary>注销边缘隐藏窗口</summary>
        public void UnregisterEdgeHidden(UIFormController window)
        {
            if (window == null) return;

            foreach (var list in _edgeHidden.Values)
                list.Remove(window);
        }

        /// <summary>获取指定方向的隐藏窗口列表</summary>
        public IReadOnlyList<UIFormController> GetEdgeWindows(AutoHideDirection direction)
        {
            return _edgeHidden[direction];
        }

        #endregion

        #region === 边缘触发检测 ===

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

            // 触发进入 → 显示该方向窗口
            if (activeTrigger.HasValue && activeTrigger != _lastEdgeTrigger)
            {
                ShowEdgeWindows(activeTrigger.Value);
            }

            // 鼠标离开所有边缘触发区
            if (!activeTrigger.HasValue)
            {
                _lastEdgeTrigger = null;
            }
        }

        /// <summary>显示指定方向的所有边缘隐藏窗口</summary>
        public void ShowEdgeWindows(AutoHideDirection direction)
        {
            _lastEdgeTrigger = direction;

            var list = _edgeHidden[direction];
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var window = list[i];
                if (window == null)
                {
                    list.RemoveAt(i);
                    continue;
                }

                window.SlideInFromEdge();
            }

            if (m_EnableDebugLog && list.Count > 0)
                Debug.Log($"[UIFormManager] 边缘触发: 方向={direction}, 显示 {list.Count} 个窗口");
        }

        #endregion

        #region === 最小化 / 恢复 ===

        internal void MinimizeWindow(UIFormController window)
        {
            if (window == null || _minimized.ContainsKey(window)) return;

            var rt = window.transform as RectTransform;
            if (rt == null) return;

            _minimized[window] = new MinimizedData
            {
                anchoredPosition = rt.anchoredPosition,
                sizeDelta = rt.sizeDelta,
                anchorMin = rt.anchorMin,
                anchorMax = rt.anchorMax,
            };

            window.gameObject.SetActive(false);

            if (m_EnableDebugLog)
                Debug.Log($"[UIFormManager] 最小化窗口: {window.name}");
        }

        /// <summary>恢复已最小化的窗口</summary>
        public void RestoreWindow(UIFormController window)
        {
            if (window == null || !_minimized.TryGetValue(window, out var data)) return;

            var rt = window.transform as RectTransform;
            if (rt == null) return;

            rt.anchorMin = data.anchorMin;
            rt.anchorMax = data.anchorMax;
            rt.anchoredPosition = data.anchoredPosition;
            rt.sizeDelta = data.sizeDelta;

            window.gameObject.SetActive(true);
            _minimized.Remove(window);

            if (m_EnableDebugLog)
                Debug.Log($"[UIFormManager] 恢复窗口: {window.name}");
        }

        /// <summary>获取所有已最小化的窗口列表</summary>
        public IReadOnlyList<UIFormController> GetMinimizedWindows()
        {
            return new List<UIFormController>(_minimized.Keys);
        }

        #endregion

        #region === 关闭 ===

        internal void CloseWindow(UIFormController window)
        {
            if (window == null) return;

            _windows.Remove(window);
            UnregisterEdgeHidden(window);
            _minimized.Remove(window);

            if (m_EnableDebugLog)
                Debug.Log($"[UIFormManager] 关闭窗口: {window.name}");
        }

        #endregion

        #region === 查询 ===

        /// <summary>已注册窗口数量</summary>
        public int WindowCount => _windows.Count;

        /// <summary>获取所有已注册窗口</summary>
        public IReadOnlyList<UIFormController> GetAllWindows() => _windows;

        #endregion
    }
}
