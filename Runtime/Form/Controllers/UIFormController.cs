using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XericUI.Form
{
    #region 枚举与结构体

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

        [Tooltip("范围定义")]
        public Rect rect;
    }

    #endregion

    /// <summary>
    /// UI 窗体控制器 — 挂载到窗口根 GameObject 上，自身即为完整窗口。
    /// 支持全屏/还原、边缘吸附、自动隐藏、拖拽移动、置顶。
    /// 通过 WindowEntry 与 Manager 通信。
    /// </summary>
    [AddComponentMenu("Xeric UI Vessel/Form/UI Form Controller")]
    [RequireComponent(typeof(RectTransform))]
    public class UIFormController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
    {
        #region 序列化字段

        [Header("窗口基本设置")]
        [SerializeField, Tooltip("窗口标题")]
        private string m_WindowTitle = "Window";

        [Header("边缘吸附")]
        [SerializeField, Tooltip("启用边缘自动吸附")]
        private bool m_EnableAutoSnap = true;

        [SerializeField, Tooltip("吸附阈值（像素）")]
        private float m_SnapThreshold = 8f;

        [Header("自动隐藏")]
        [SerializeField, Tooltip("启用自动隐藏")]
        private bool m_EnableAutoHide;

        [SerializeField, Tooltip("自动隐藏滑动方向")]
        private AutoHideDirection m_AutoHideDirection = AutoHideDirection.Left;

        [Header("调试")]
        [SerializeField, Tooltip("编辑器调试模式")]
        private bool m_DebugMode = true;

        [SerializeField, Tooltip("输出事件日志")]
        private bool m_DebugLogEvents;

        #endregion

        #region 公开属性

        public string WindowTitle => m_WindowTitle;
        public bool IsMaximized => _windowState == WindowState.Maximized;
        public bool IsEdgeHidden => _windowState == WindowState.EdgeHidden;
        public WindowState CurrentState => _windowState;
        public bool AutoSnapEnabled => m_EnableAutoSnap;
        public bool AutoHideEnabled => m_EnableAutoHide;
        public AutoHideDirection HideDirection => m_AutoHideDirection;
        public bool DebugMode => m_DebugMode;

        /// <summary>窗口数据 Entry（由 Manager 在 CreateWindow 时注入）</summary>
        public WindowEntry Entry
        {
            get => _entry;
            set
            {
                _entry = value;
                if (_entry != null)
                    m_WindowTitle = _entry.Title;
            }
        }

        /// <summary>父级 Manager 引用</summary>
        public UIFormManager Manager => _manager;

        #endregion

        #region 内部状态

        private RectTransform _rectTransform;
        private Canvas _canvas;
        private RectTransform _canvasRt;
        private UIFormManager _manager;
        private WindowEntry _entry;

        private WindowState _windowState = WindowState.Normal;

        // 拖拽
        private Vector2 _dragOffset;

        // 全屏还原
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
            _manager = GetComponentInParent<UIFormManager>();
            _canvas = GetComponentInParent<Canvas>();
            _canvasRt = _canvas != null ? (RectTransform)_canvas.transform : null;
        }

        protected virtual void OnEnable()
        {
        }

        protected virtual void OnDisable()
        {
            StopAutoHideMonitor();

            if (_windowState == WindowState.EdgeHidden && _manager != null)
                _manager.UnregisterEdgeHidden(_entry);

#if UNITY_EDITOR
            UnsubscribeEditorUpdate();
#endif
            _isHovering = false;
        }

        #endregion

        #region === 公开 API ===

        public void ToggleMaximize()
        {
            if (IsMaximized)
                RestoreFromFullscreen();
            else
                MaximizeToFullscreen();

            if (_entry != null)
                _entry.State = _windowState;
        }

        public void Minimize()
        {
            if (_windowState == WindowState.Minimized) return;
            if (_manager == null || _entry == null) return;
            _manager.MinimizeWindow(_entry.Id);
        }

        public void Close()
        {
            if (_manager != null && _entry != null)
                _manager.DestroyWindow(_entry.Id);
            else if (Application.isPlaying)
                Destroy(gameObject);
        }

        public void SetAutoSnapEnabled(bool enabled) => m_EnableAutoSnap = enabled;
        public void SetAutoHideEnabled(bool enabled)
        {
            m_EnableAutoHide = enabled;
            if (!enabled && _windowState == WindowState.EdgeHidden)
                SlideInFromEdge();
        }

        public void SetAutoHideDirection(AutoHideDirection direction) => m_AutoHideDirection = direction;

        /// <summary>置顶窗口</summary>
        public void MoveToFront()
        {
            transform.SetAsLastSibling();
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
        }

        private void RestoreFromFullscreen()
        {
            if (_windowState != WindowState.Maximized) return;

            _rectTransform.anchorMin = _saveAnchorMin;
            _rectTransform.anchorMax = _saveAnchorMax;
            _rectTransform.anchoredPosition = _savePos;
            _rectTransform.sizeDelta = _saveSize;

            _windowState = WindowState.Normal;
        }

        #endregion

        #region === 拖拽 & 点击聚焦 ===

        public void OnPointerDown(PointerEventData eventData)
        {
            // 点击窗口任意位置 → 置顶
            transform.SetAsLastSibling();
            if (_manager != null && _entry != null)
                _manager.MoveToFront(_entry.Id);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_windowState == WindowState.EdgeHidden || _windowState == WindowState.Minimized) return;

            if (IsMaximized)
            {
                RestoreFromFullscreen();
                _rectTransform.anchoredPosition = eventData.position;
            }

            _dragOffset = _rectTransform.anchoredPosition - eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_windowState == WindowState.EdgeHidden || _windowState == WindowState.Minimized) return;

            _rectTransform.anchoredPosition = eventData.position + _dragOffset;

            if (m_EnableAutoSnap)
                CheckEdgeSnap();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_windowState == WindowState.EdgeHidden || _windowState == WindowState.Minimized) return;

            if (m_EnableAutoHide && IsSnappedToEdge())
                AutoHideToEdge();
        }

        #endregion

        #region === 边缘吸附 ===

        private void CheckEdgeSnap()
        {
            if (_canvasRt == null) return;
            Vector2 canvasSize = _canvasRt.rect.size;
            Vector2 pos = _rectTransform.anchoredPosition;
            Vector2 size = _rectTransform.rect.size;

            if (Mathf.Abs(pos.x) < m_SnapThreshold)
                pos.x = 0;
            else if (Mathf.Abs(pos.x + size.x - canvasSize.x) < m_SnapThreshold)
                pos.x = canvasSize.x - size.x;

            _rectTransform.anchoredPosition = pos;
        }

        private bool IsSnappedToEdge()
        {
            if (_canvasRt == null) return false;
            Vector2 canvasSize = _canvasRt.rect.size;
            Vector2 pos = _rectTransform.anchoredPosition;
            Vector2 size = _rectTransform.rect.size;
            return Mathf.Abs(pos.x) < 1f || Mathf.Abs(pos.x + size.x - canvasSize.x) < 1f;
        }

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
            if (_windowState == WindowState.EdgeHidden || _canvasRt == null) return;

            Vector2 canvasSize = _canvasRt.rect.size;
            Vector2 pos = _rectTransform.anchoredPosition;
            Vector2 size = _rectTransform.rect.size;

            _edgeVisiblePos = pos;

            AutoHideDirection dir = m_AutoHideDirection;
            if (dir == AutoHideDirection.Left || dir == AutoHideDirection.Right)
                dir = GetClosestEdge();

            _edgeHiddenPos = dir switch
            {
                AutoHideDirection.Left => new Vector2(-size.x, pos.y),
                AutoHideDirection.Right => new Vector2(canvasSize.x, pos.y),
                AutoHideDirection.Down => new Vector2(pos.x, -size.y),
                AutoHideDirection.Up => new Vector2(pos.x, canvasSize.y),
                _ => pos
            };

            _rectTransform.anchoredPosition = _edgeHiddenPos;
            _windowState = WindowState.EdgeHidden;

            if (_manager != null && _entry != null)
                _manager.RegisterEdgeHidden(_entry, dir);
        }

        public void SlideInFromEdge()
        {
            if (_windowState != WindowState.EdgeHidden) return;
            StopAutoHideMonitor();
            _rectTransform.anchoredPosition = _edgeVisiblePos;
            _windowState = WindowState.Normal;

            if (_manager != null)
                _manager.UnregisterEdgeHidden(_entry);

            _autoHideCoroutine = StartCoroutine(MonitorMouseExit());
        }

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

        public Rect GetWindowScreenRect()
        {
            if (_rectTransform == null || _canvas == null) return Rect.zero;
            Vector3[] corners = new Vector3[4];
            _rectTransform.GetWorldCorners(corners);
            Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
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
            if (m_DebugMode) SubscribeEditorUpdate();
            else UnsubscribeEditorUpdate();
        }

        private void OnSceneGUI(UnityEditor.SceneView sceneView)
        {
            if (this == null) { UnsubscribeEditorUpdate(); return; }
            if (Application.isPlaying || !m_DebugMode) return;

            Vector2 guiPos = Event.current.mousePosition;
            _editorMouseScreenPos = GUIUtility.GUIToScreenPoint(guiPos);

            Rect rect = GetWindowScreenRect();
            _isHovering = rect.Contains(_editorMouseScreenPos);
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (!m_DebugMode || _canvas == null || _canvasRt == null) return;
            Rect screenRect = GetWindowScreenRect();
            if (screenRect.width <= 0 || screenRect.height <= 0) return;

            Color borderColor = _isHovering
                ? new Color(0f, 1f, 0f, 0.6f)
                : new Color(0f, 0.7f, 1f, 0.3f);

            DrawScreenRectOnCanvas(screenRect, borderColor, _canvas, _canvasRt);
        }

        private static void DrawScreenRectOnCanvas(Rect screenRect, Color color, Canvas canvas, RectTransform canvasRt)
        {
            if (screenRect.width <= 0 || screenRect.height <= 0) return;
            Gizmos.color = color;
            Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector3 p0 = Stw(new Vector3(screenRect.xMin, screenRect.yMin), uiCam, canvasRt);
            Vector3 p1 = Stw(new Vector3(screenRect.xMax, screenRect.yMin), uiCam, canvasRt);
            Vector3 p2 = Stw(new Vector3(screenRect.xMax, screenRect.yMax), uiCam, canvasRt);
            Vector3 p3 = Stw(new Vector3(screenRect.xMin, screenRect.yMax), uiCam, canvasRt);
            Gizmos.DrawLine(p0, p1); Gizmos.DrawLine(p1, p2);
            Gizmos.DrawLine(p2, p3); Gizmos.DrawLine(p3, p0);
        }

        private static Vector3 Stw(Vector3 sp, Camera cam, RectTransform rt)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(rt, (Vector2)sp, cam, out Vector3 wp);
            return wp;
        }
#endif

        #endregion
    }
}
