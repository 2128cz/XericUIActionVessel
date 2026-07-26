using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XericUI.Form
{
    #region 枚举与结构体

    /// <summary>范围坐标空间枚举</summary>
    public enum FormRangeSpace
    {
        RelativeOffset,
        CanvasLocal,
        AnchorToSelf,
        AnchorToCanvas
    }

    [System.Serializable]
    public struct RangeRect
    {
        [Tooltip("坐标空间")]
        public FormRangeSpace space;

        [Tooltip("范围定义。\nRelativeOffset: x,y=像素偏移; w,h=像素宽高(0=用参考尺寸)\nCanvasLocal: Canvas局部坐标\nAnchorToSelf: x,y=锚点0-1; w,h=尺寸比例0-1\nAnchorToCanvas: x,y=锚点0-1; w,h=尺寸比例0-1")]
        public Rect rect;
    }

    #endregion

    /// <summary>
    /// UI 窗体控制器 — 挂载到窗口根 GameObject 上，自身即为完整窗口。
    /// 支持全屏/还原、边缘吸附、自动隐藏、拖拽移动。
    /// 标题栏功能由独立的 UITitleBar 子组件提供，Controller 只提供公开 API。
    /// </summary>
    [AddComponentMenu("Xeric UI Vessel/Form/UI Form Controller")]
    [RequireComponent(typeof(RectTransform))]
    public class UIFormController : MonoBehaviour, IUIFormTarget, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        #region 序列化字段

        [Header("窗口基本设置")]
        [SerializeField, Tooltip("窗口标题，供外部读取")]
        private string m_WindowTitle = "Window";

        [Header("边缘吸附")]
        [SerializeField, Tooltip("是否启用边缘自动吸附")]
        private bool m_EnableAutoSnap = true;

        [SerializeField, Tooltip("吸附阈值（像素），窗口距边缘小于此值时自动贴边")]
        private float m_SnapThreshold = 8f;

        [Header("自动隐藏")]
        [SerializeField, Tooltip("是否启用自动隐藏（贴边后滑出屏幕）")]
        private bool m_EnableAutoHide;

        [SerializeField, Tooltip("自动隐藏的滑动方向")]
        private AutoHideDirection m_AutoHideDirection = AutoHideDirection.Left;

        [Header("调试")]
        [SerializeField, Tooltip("编辑器调试模式")]
        private bool m_DebugMode = true;

        [SerializeField, Tooltip("输出事件日志")]
        private bool m_DebugLogEvents;

        #endregion

        #region 公开属性

        public string WindowTitle => m_WindowTitle;

        /// <summary>是否处于全屏状态</summary>
        public bool IsMaximized => _windowState == WindowState.Maximized;

        /// <summary>是否处于边缘隐藏状态</summary>
        public bool IsEdgeHidden => _windowState == WindowState.EdgeHidden;

        /// <summary>当前窗口状态</summary>
        public WindowState CurrentState => _windowState;

        /// <summary>自动吸附是否开启</summary>
        public bool AutoSnapEnabled => m_EnableAutoSnap;

        /// <summary>自动隐藏是否开启</summary>
        public bool AutoHideEnabled => m_EnableAutoHide;

        /// <summary>自动隐藏方向</summary>
        public AutoHideDirection HideDirection => m_AutoHideDirection;

        /// <summary>编辑器调试模式</summary>
        public bool DebugMode => m_DebugMode;

        #endregion

        #region 内部状态

        private RectTransform _rectTransform;
        private Canvas _canvas;
        private RectTransform _canvasRt;

        private WindowState _windowState = WindowState.Normal;

        // 拖拽
        private Vector2 _dragOffset;

        // 全屏还原数据
        private Vector2 _savePos;
        private Vector2 _saveSize;
        private Vector2 _saveAnchorMin;
        private Vector2 _saveAnchorMax;

        // 边缘隐藏
        private Vector2 _edgeVisiblePos;
        private Vector2 _edgeHiddenPos;
        private Coroutine _autoHideCoroutine;

        // 悬停
        private bool _isHovering;

        #endregion

        #region 生命周期

        protected virtual void Awake()
        {
            _rectTransform = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();
            _canvasRt = _canvas != null ? (RectTransform)_canvas.transform : null;
        }

        protected virtual void OnEnable()
        {
            if (UIFormManager.Main != null)
                UIFormManager.Main.RegisterWindow(this);
        }

        protected virtual void OnDisable()
        {
            StopAutoHideMonitor();

            if (_windowState == WindowState.EdgeHidden && UIFormManager.Main != null)
                UIFormManager.Main.UnregisterEdgeHidden(this);

            if (UIFormManager.Main != null)
            {
                UIFormManager.Main.UnregisterWindow(this);
                UIFormManager.Main.UnregisterEdgeHidden(this);
            }

            UnsubscribeEditorUpdate();
            _isHovering = false;
        }

        #endregion

        #region IUIFormTarget 实现

        public bool IsActive => isActiveAndEnabled;

        public Rect GetEnterRect() => GetWindowScreenRect();
        public Rect GetExitRect() => GetWindowScreenRect();

        public void OnPointerEnter()
        {
            if (m_DebugLogEvents)
                Debug.Log($"[UIFormController] 鼠标进入窗口范围: {name}", this);
        }

        public void OnPointerExit()
        {
            if (m_DebugLogEvents)
                Debug.Log($"[UIFormController] 鼠标离开窗口范围: {name}", this);
        }

        #endregion

        #region === 公开 API（供 UITitleBar 及外部调用） ===

        /// <summary>全屏/还原切换</summary>
        public void ToggleMaximize()
        {
            if (IsMaximized)
                RestoreFromFullscreen();
            else
                MaximizeToFullscreen();
        }

        /// <summary>最小化窗口</summary>
        public void Minimize()
        {
            if (_windowState == WindowState.Minimized) return;
            if (UIFormManager.Main == null) return;

            UIFormManager.Main.MinimizeWindow(this);
        }

        /// <summary>关闭窗口（Destroy GameObject）</summary>
        public void Close()
        {
            if (UIFormManager.Main != null)
                UIFormManager.Main.CloseWindow(this);

            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }

        /// <summary>设置边缘吸附开关</summary>
        public void SetAutoSnapEnabled(bool enabled)
        {
            m_EnableAutoSnap = enabled;
        }

        /// <summary>设置自动隐藏开关</summary>
        public void SetAutoHideEnabled(bool enabled)
        {
            m_EnableAutoHide = enabled;

            if (!enabled && _windowState == WindowState.EdgeHidden)
            {
                // 关闭自动隐藏 → 还原窗口
                SlideInFromEdge();
            }
        }

        /// <summary>设置自动隐藏方向</summary>
        public void SetAutoHideDirection(AutoHideDirection direction)
        {
            m_AutoHideDirection = direction;
        }

        #endregion

        #region === 全屏/还原 ===

        private void MaximizeToFullscreen()
        {
            if (_windowState == WindowState.Maximized) return;

            _savePos = _rectTransform.anchoredPosition;
            _saveSize = _rectTransform.sizeDelta;
            _saveAnchorMin = _rectTransform.anchorMin;
            _saveAnchorMax = _rectTransform.anchorMax;

            _rectTransform.anchorMin = Vector2.zero;
            _rectTransform.anchorMax = Vector2.one;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;

            _windowState = WindowState.Maximized;

            if (m_DebugLogEvents)
                Debug.Log($"[UIFormController] 窗口全屏: {name}", this);
        }

        private void RestoreFromFullscreen()
        {
            if (_windowState != WindowState.Maximized) return;

            _rectTransform.anchorMin = _saveAnchorMin;
            _rectTransform.anchorMax = _saveAnchorMax;
            _rectTransform.anchoredPosition = _savePos;
            _rectTransform.sizeDelta = _saveSize;

            _windowState = WindowState.Normal;

            if (m_DebugLogEvents)
                Debug.Log($"[UIFormController] 窗口还原: {name}", this);
        }

        #endregion

        #region === 拖拽 ===

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_windowState == WindowState.EdgeHidden || _windowState == WindowState.Minimized)
                return;

            // 全屏拖拽 → 先还原
            if (IsMaximized)
            {
                RestoreFromFullscreen();
                // 将窗口移到鼠标附近
                _rectTransform.anchoredPosition = eventData.position;
            }

            _dragOffset = _rectTransform.anchoredPosition - eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_windowState == WindowState.EdgeHidden || _windowState == WindowState.Minimized)
                return;

            _rectTransform.anchoredPosition = eventData.position + _dragOffset;

            if (m_EnableAutoSnap)
                CheckEdgeSnap();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_windowState == WindowState.EdgeHidden || _windowState == WindowState.Minimized)
                return;

            // 拖拽结束 → 如果贴边且启用自动隐藏，则触发
            if (m_EnableAutoHide && IsSnappedToEdge())
            {
                AutoHideToEdge();
            }
        }

        #endregion

        #region === 边缘吸附 ===

        private void CheckEdgeSnap()
        {
            if (_canvasRt == null) return;

            Vector2 canvasSize = _canvasRt.rect.size;
            Vector2 pos = _rectTransform.anchoredPosition;
            Vector2 size = _rectTransform.rect.size;

            bool snapped = false;

            // 左
            if (Mathf.Abs(pos.x) < m_SnapThreshold)
            {
                pos.x = 0;
                snapped = true;
            }
            // 右
            else if (Mathf.Abs(pos.x + size.x - canvasSize.x) < m_SnapThreshold)
            {
                pos.x = canvasSize.x - size.x;
                snapped = true;
            }

            _rectTransform.anchoredPosition = pos;
        }

        /// <summary>检测窗口是否贴边</summary>
        private bool IsSnappedToEdge()
        {
            if (_canvasRt == null) return false;

            Vector2 canvasSize = _canvasRt.rect.size;
            Vector2 pos = _rectTransform.anchoredPosition;
            Vector2 size = _rectTransform.rect.size;

            return Mathf.Abs(pos.x) < 1f
                || Mathf.Abs(pos.x + size.x - canvasSize.x) < 1f;
        }

        /// <summary>获取最近的边缘方向</summary>
        public AutoHideDirection GetClosestEdge()
        {
            if (_canvasRt == null) return AutoHideDirection.Left;

            Vector2 canvasSize = _canvasRt.rect.size;
            Vector2 pos = _rectTransform.anchoredPosition;
            Vector2 size = _rectTransform.rect.size;

            float distLeft = Mathf.Abs(pos.x);
            float distRight = Mathf.Abs(pos.x + size.x - canvasSize.x);

            return distLeft < distRight ? AutoHideDirection.Left : AutoHideDirection.Right;
        }

        #endregion

        #region === 自动隐藏 ===

        private void AutoHideToEdge()
        {
            if (_windowState == WindowState.EdgeHidden) return;
            if (_canvasRt == null) return;

            Vector2 canvasSize = _canvasRt.rect.size;
            Vector2 pos = _rectTransform.anchoredPosition;
            Vector2 size = _rectTransform.rect.size;

            _edgeVisiblePos = pos;

            AutoHideDirection dir = m_AutoHideDirection;
            if (dir == AutoHideDirection.Left || dir == AutoHideDirection.Right)
                dir = GetClosestEdge();

            switch (dir)
            {
                case AutoHideDirection.Left:
                    _edgeHiddenPos = new Vector2(-size.x, pos.y);
                    break;
                case AutoHideDirection.Right:
                    _edgeHiddenPos = new Vector2(canvasSize.x, pos.y);
                    break;
                case AutoHideDirection.Down:
                    _edgeHiddenPos = new Vector2(pos.x, -size.y);
                    break;
                case AutoHideDirection.Up:
                    _edgeHiddenPos = new Vector2(pos.x, canvasSize.y);
                    break;
            }

            _rectTransform.anchoredPosition = _edgeHiddenPos;
            _windowState = WindowState.EdgeHidden;

            if (UIFormManager.Main != null)
                UIFormManager.Main.RegisterEdgeHidden(this, dir);

            if (m_DebugLogEvents)
                Debug.Log($"[UIFormController] 自动隐藏 (方向={dir}): {name}", this);
        }

        /// <summary>由 Manager 调用 — 从边缘滑出</summary>
        public void SlideInFromEdge()
        {
            if (_windowState != WindowState.EdgeHidden) return;

            StopAutoHideMonitor();
            _rectTransform.anchoredPosition = _edgeVisiblePos;
            _windowState = WindowState.Normal;

            if (UIFormManager.Main != null)
                UIFormManager.Main.UnregisterEdgeHidden(this);

            // 开始监控鼠标是否离开窗口
            _autoHideCoroutine = StartCoroutine(MonitorMouseExit());

            if (m_DebugLogEvents)
                Debug.Log($"[UIFormController] 从边缘滑出: {name}", this);
        }

        /// <summary>监控鼠标是否离开窗口范围，离开则自动隐藏</summary>
        private IEnumerator MonitorMouseExit()
        {
            while (_windowState == WindowState.Normal && m_EnableAutoHide)
            {
                yield return null;

                Rect screenRect = GetWindowScreenRect();
                if (!screenRect.Contains(Input.mousePosition))
                {
                    AutoHideToEdge();
                    yield break;
                }
            }
        }

        private void StopAutoHideMonitor()
        {
            if (_autoHideCoroutine != null)
            {
                StopCoroutine(_autoHideCoroutine);
                _autoHideCoroutine = null;
            }
        }

        #endregion

        #region === 窗口屏幕范围 ===

        /// <summary>获取窗口自身在屏幕空间中的 Rect</summary>
        public Rect GetWindowScreenRect()
        {
            if (_rectTransform == null || _canvas == null) return Rect.zero;

            Vector3[] corners = new Vector3[4];
            _rectTransform.GetWorldCorners(corners);

            Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.negativeInfinity;

            for (int i = 0; i < 4; i++)
            {
                Vector2 sp = cam != null
                    ? RectTransformUtility.WorldToScreenPoint(cam, corners[i])
                    : (Vector2)corners[i];

                min = Vector2.Min(min, sp);
                max = Vector2.Max(max, sp);
            }

            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        #endregion

        #region === 悬停检测（供 Manager 调用） ===

        public bool CheckHover(Vector2 screenPoint)
        {
            Rect windowRect = GetWindowScreenRect();

            if (!_isHovering)
            {
                if (windowRect.Contains(screenPoint))
                {
                    _isHovering = true;
                    OnPointerEnter();
                }
            }
            else
            {
                if (!windowRect.Contains(screenPoint))
                {
                    _isHovering = false;
                    OnPointerExit();
                }
            }

            return _isHovering;
        }

        public void SimulateEnter()
        {
            _isHovering = true;

            if (m_DebugLogEvents)
                Debug.Log($"[UIFormController] 模拟进入: {name}", this);
        }

        public void SimulateExit()
        {
            _isHovering = false;

            if (m_DebugLogEvents)
                Debug.Log($"[UIFormController] 模拟退出: {name}", this);
        }

        #endregion

        #region 编辑器调试

#if UNITY_EDITOR

        [System.NonSerialized] private bool _editorDebugSubscribed;
        [System.NonSerialized] private Vector2 _editorMouseScreenPos;

        private void SubscribeEditorUpdate()
        {
            if (_editorDebugSubscribed) return;
            UnityEditor.SceneView.duringSceneGui += OnSceneGUI;
            _editorDebugSubscribed = true;
        }

        private void UnsubscribeEditorUpdate()
        {
            if (!_editorDebugSubscribed) return;
            UnityEditor.SceneView.duringSceneGui -= OnSceneGUI;
            _editorDebugSubscribed = false;
        }

        protected virtual void OnValidate()
        {
            if (m_DebugMode)
                SubscribeEditorUpdate();
            else
                UnsubscribeEditorUpdate();
        }

        /// <summary>
        /// SceneView 重绘回调 — 在 GUI 上下文中获取鼠标屏幕坐标并执行悬停检测。
        /// 使用 GUIUtility.GUIToScreenPoint 将 SceneView 内的 GUI 坐标转为 Unity 屏幕坐标。
        /// </summary>
        private void OnSceneGUI(UnityEditor.SceneView sceneView)
        {
            if (this == null)
            {
                UnsubscribeEditorUpdate();
                return;
            }

            if (Application.isPlaying) return;
            if (!m_DebugMode) return;

            // Event.current.mousePosition 是相对于 SceneView 窗口的 GUI 坐标
            // GUIUtility.GUIToScreenPoint 将其转换为屏幕坐标（左下角为原点）
            Vector2 guiPos = Event.current.mousePosition;
            _editorMouseScreenPos = GUIUtility.GUIToScreenPoint(guiPos);

            CheckHover(_editorMouseScreenPos);
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (!m_DebugMode) return;
            if (_canvas == null) return;
            if (_canvasRt == null) return;

            Rect screenRect = GetWindowScreenRect();
            if (screenRect.width <= 0 || screenRect.height <= 0) return;

            // 悬停时高亮边框
            Color borderColor = _isHovering
                ? new Color(0f, 1f, 0f, 0.6f)
                : new Color(0f, 0.7f, 1f, 0.3f);
            Gizmos.color = borderColor;

            DrawScreenRectOnCanvas(screenRect, borderColor, _canvas, _canvasRt);

            // 边缘吸附阈值可视化
            if (m_EnableAutoSnap)
            {
                Gizmos.color = new Color(1f, 0.6f, 0f, 0.15f);
                DrawScreenRectOnCanvas(
                    new Rect(screenRect.x + m_SnapThreshold, screenRect.y,
                             screenRect.width - m_SnapThreshold * 2, screenRect.height),
                    Gizmos.color, _canvas, _canvasRt);
            }
        }

        private static void DrawScreenRectOnCanvas(Rect screenRect, Color color, Canvas canvas, RectTransform canvasRt)
        {
            if (screenRect.width <= 0 || screenRect.height <= 0) return;

            Gizmos.color = color;
            Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            Vector3 p0 = ScreenToWorldOnCanvas(new Vector3(screenRect.xMin, screenRect.yMin), uiCam, canvasRt);
            Vector3 p1 = ScreenToWorldOnCanvas(new Vector3(screenRect.xMax, screenRect.yMin), uiCam, canvasRt);
            Vector3 p2 = ScreenToWorldOnCanvas(new Vector3(screenRect.xMax, screenRect.yMax), uiCam, canvasRt);
            Vector3 p3 = ScreenToWorldOnCanvas(new Vector3(screenRect.xMin, screenRect.yMax), uiCam, canvasRt);

            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawLine(p2, p3);
            Gizmos.DrawLine(p3, p0);
        }

        private static Vector3 ScreenToWorldOnCanvas(Vector3 screenPoint, Camera uiCamera, RectTransform canvasRt)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRt, (Vector2)screenPoint, uiCamera, out Vector3 wp);
            return wp;
        }

#endif

        #endregion
    }
}
