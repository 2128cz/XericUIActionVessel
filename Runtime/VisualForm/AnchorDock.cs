using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace XericUI.VisualForm
{
    /// <summary>
    /// 锚点坞 - 挂载在Canvas上的主要布局组件管理器。
    /// 负责收集所有世界锚点，每帧将世界坐标转换到屏幕坐标，并管理中间窗口状态。
    /// </summary>
    [AddComponentMenu("Xeric UI Vessel/AnchorWindow/AnchorDock")]
    public class AnchorDock : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField, Tooltip("指定摄像机，不填则自动检测")]
        private Camera m_OverrideCamera;

        [SerializeField, Tooltip("坐标变化阈值，移动超过此值才会标记坐标脏")]
        private float m_PositionThreshold = 0.1f;

        [Space]
        [SerializeField, Tooltip("预注册的锚点列表（坞可直接标记要使用的窗口锚点，双方引用有一个可不填）")]
        private List<WorldAnchor> m_TargetAnchors = new List<WorldAnchor>();

        [SerializeField, Tooltip("预分配的中间类对象池容量")]
        private int m_StatePoolPreallocate = 16;

        #region 静态单例

        private static AnchorDock s_Main;

        /// <summary>
        /// 主要的静态单例锚点坞引用
        /// </summary>
        public static AnchorDock Main => s_Main;

        /// <summary>
        /// 待注册锚点列表 — 锚点在OnEnable时找不到坞，将自己暂存于此，等待坞后续处理
        /// </summary>
        internal static readonly HashSet<WorldAnchor> s_PendingAnchors = new HashSet<WorldAnchor>();

        #endregion

        #region 内部状态

        private Canvas _canvas;
        private Camera _renderCamera;
        private RectTransform _rectTransform;

        /// <summary>
        /// 锚点集合（HashSet用于O(1)去重和查找）
        /// </summary>
        private readonly HashSet<WorldAnchor> _anchorSet = new HashSet<WorldAnchor>();

        /// <summary>
        /// 锚点脏标记 - 有新增或移除时为true，延迟到下一帧处理
        /// </summary>
        private bool _anchorsDirty;

        /// <summary>
        /// 待新增锚点列表（脏更新期间收集）
        /// </summary>
        private readonly List<WorldAnchor> _pendingAdds = new List<WorldAnchor>();

        /// <summary>
        /// 待移除锚点列表（脏更新期间收集）
        /// </summary>
        private readonly List<WorldAnchor> _pendingRemoves = new List<WorldAnchor>();

        /// <summary>
        /// 活跃的窗口状态字典：锚点 -> 中间窗口状态类
        /// </summary>
        private readonly Dictionary<WorldAnchor, WindowState> _activeStates = new Dictionary<WorldAnchor, WindowState>();

        /// <summary>
        /// 中间类对象池
        /// </summary>
        private readonly Queue<WindowState> _statePool = new Queue<WindowState>();

        /// <summary>
        /// 子对象哈希集 - 用于O(1)快速检查对象是否在子项中
        /// </summary>
        private readonly HashSet<GameObject> _childrenHashSet = new HashSet<GameObject>();

        /// <summary>
        /// 子对象哈希集脏标记
        /// </summary>
        private bool _childrenSetDirty = true;

        /// <summary>
        /// 摄像机变换脏标记
        /// </summary>
        private bool _cameraDirty;

        /// <summary>
        /// 上帧摄像机世界坐标
        /// </summary>
        private Vector3 _lastCameraPosition;

        /// <summary>
        /// 上帧摄像机世界旋转
        /// </summary>
        private Quaternion _lastCameraRotation;

        #endregion

        #region 公开属性

        /// <summary>
        /// 当前使用的渲染摄像机
        /// </summary>
        public Camera RenderCamera => _renderCamera;

        /// <summary>
        /// Canvas组件引用
        /// </summary>
        public Canvas Canvas => _canvas;

        /// <summary>
        /// RectTransform引用
        /// </summary>
        public RectTransform RectTransform
        {
            get
            {
                if (_rectTransform == null)
                    _rectTransform = GetComponent<RectTransform>();
                return _rectTransform;
            }
        }

        /// <summary>
        /// 坐标变化阈值
        /// </summary>
        public float PositionThreshold => m_PositionThreshold;

        #endregion

        #region 生命周期

        protected virtual void Awake()
        {
            // 在Awake阶段注册单例
            s_Main = this;
        }

        protected virtual void OnEnable()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null)
                _canvas = GetComponent<Canvas>();

            _rectTransform = GetComponent<RectTransform>();

            // 预分配对象池
            for (int i = 0; i < m_StatePoolPreallocate; i++)
            {
                var state = new WindowState();
                _statePool.Enqueue(state);
            }

            // 注册预置的锚点
            foreach (var anchor in m_TargetAnchors)
            {
                if (anchor != null && !_anchorSet.Contains(anchor))
                {
                    anchor.TargetDock = this;
                    // 如果锚点已激活则直接添加
                    if (anchor.isActiveAndEnabled)
                        AddAnchor(anchor);
                }
            }

            // 初始化摄像机引用
            RefreshCameraReference();
            _lastCameraPosition = _renderCamera ? _renderCamera.transform.position : Vector3.zero;
            _lastCameraRotation = _renderCamera ? _renderCamera.transform.rotation : Quaternion.identity;

            // 标记需要重建子对象哈希集
            MarkChildrenSetDirty();
        }

        protected virtual void OnDisable()
        {
            // 清除静态单例
            if (s_Main == this)
                s_Main = null;

            // 清理所有中间类
            foreach (var state in _activeStates.Values)
            {
                state.OnEnd();
            }

            foreach (var state in _statePool)
            {
                state.OnEnd();
            }

            _activeStates.Clear();
            _statePool.Clear();
            _anchorSet.Clear();
            _pendingAdds.Clear();
            _pendingRemoves.Clear();
        }

        protected virtual void LateUpdate()
        {
            // 0. 处理待注册锚点（在OnEnable阶段未找到坞的锚点）
            ProcessPendingAnchors();

            // 1. 处理锚点脏更新（新增/移除窗口）
            ProcessDirtyAnchors();

            // 2. 检查摄像机变化
            CheckCameraDirty();

            // 3. 遍历所有中间类，检查坐标脏（比较上帧和本帧位置差异）
            foreach (var kvp in _activeStates)
            {
                var state = kvp.Value;
                if (state == null || !state.IsEnabled) continue;
                state.CheckPositionDirty(m_PositionThreshold);
            }

            // 4. 遍历所有中间类，根据脏标记更新坐标
            foreach (var kvp in _activeStates)
            {
                var state = kvp.Value;
                if (state == null || !state.IsEnabled) continue;

                bool needUpdate = _cameraDirty || state.CoordinateDirty;
                if (needUpdate)
                {
                    UpdateStatePosition(state);
                }
            }

            // 5. 复位脏标记
            _cameraDirty = false;
        }

        #endregion

        #region 锚点管理

        /// <summary>
        /// 添加世界锚点到管理列表
        /// </summary>
        /// <param name="anchor">要添加的锚点</param>
        public void AddAnchor(WorldAnchor anchor)
        {
            if (anchor == null || _anchorSet.Contains(anchor))
                return;

            _anchorSet.Add(anchor);
            _pendingAdds.Add(anchor);
            _anchorsDirty = true;
        }

        /// <summary>
        /// 从管理列表移除世界锚点
        /// </summary>
        /// <param name="anchor">要移除的锚点</param>
        public void RemoveAnchor(WorldAnchor anchor)
        {
            if (anchor == null || !_anchorSet.Contains(anchor))
                return;

            _anchorSet.Remove(anchor);
            _pendingRemoves.Add(anchor);
            _anchorsDirty = true;
        }

        /// <summary>
        /// 获取指定锚点对应的中间窗口状态类
        /// </summary>
        /// <param name="anchor">锚点引用</param>
        /// <returns>对应的WindowState，不存在则返回null</returns>
        public WindowState GetWindowState(WorldAnchor anchor)
        {
            if (anchor == null) return null;
            _activeStates.TryGetValue(anchor, out var state);
            return state;
        }

        /// <summary>
        /// 处理静态待注册锚点列表 — 这些锚点在OnEnable时未找到坞，现在坞已就绪，正式注册它们
        /// </summary>
        private void ProcessPendingAnchors()
        {
            if (s_PendingAnchors.Count == 0) return;

            foreach (var anchor in s_PendingAnchors)
            {
                if (anchor == null) continue;
                anchor.TargetDock = this;
                AddAnchor(anchor);
            }
            s_PendingAnchors.Clear();
        }

        /// <summary>
        /// 处理锚点脏更新：创建/销毁WindowState
        /// </summary>
        private void ProcessDirtyAnchors()
        {
            if (!_anchorsDirty) return;
            _anchorsDirty = false;

            // 处理移除
            for (int i = 0; i < _pendingRemoves.Count; i++)
            {
                var anchor = _pendingRemoves[i];
                if (_activeStates.TryGetValue(anchor, out var state))
                {
                    state.OnDisable();
                    state.OnEnd();
                    _statePool.Enqueue(state);
                    _activeStates.Remove(anchor);
                }
            }
            _pendingRemoves.Clear();

            // 确保子对象哈希集在创建WindowState前是最新的
            if (_childrenSetDirty)
                RebuildChildrenSet();

            // 处理新增
            for (int i = 0; i < _pendingAdds.Count; i++)
            {
                var anchor = _pendingAdds[i];
                if (_activeStates.ContainsKey(anchor)) continue;

                var state = GetPooledState();
                state.OnStart(anchor, this);
                _activeStates[anchor] = state;

                // 如果锚点当前处于激活状态，调用Enable
                if (anchor.isActiveAndEnabled)
                    state.OnEnable();
            }
            _pendingAdds.Clear();
        }

        #endregion

        #region 坐标更新

        /// <summary>
        /// 刷新摄像机引用：检查Canvas的renderMode，选择合适的摄像机
        /// </summary>
        public void RefreshCameraReference()
        {
            if (m_OverrideCamera != null)
            {
                _renderCamera = m_OverrideCamera;
                return;
            }

            if (_canvas == null)
            {
                _canvas = GetComponent<Canvas>();
                if (_canvas == null)
                    _canvas = GetComponentInParent<Canvas>();
            }

            if (_canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                _renderCamera = _canvas.worldCamera;
            }

            // ScreenSpaceOverlay 或找不到摄像机时使用MainCamera
            if (_renderCamera == null)
                _renderCamera = Camera.main;
        }

        /// <summary>
        /// 检查摄像机变换是否发生变化
        /// </summary>
        private void CheckCameraDirty()
        {
            if (_renderCamera == null)
            {
                RefreshCameraReference();
                return;
            }

            var camTransform = _renderCamera.transform;
            var currentPos = camTransform.position;
            var currentRot = camTransform.rotation;

            if (currentPos != _lastCameraPosition || currentRot != _lastCameraRotation)
            {
                _cameraDirty = true;
                _lastCameraPosition = currentPos;
                _lastCameraRotation = currentRot;
            }
        }

        /// <summary>
        /// 更新指定中间类的屏幕坐标
        /// </summary>
        private void UpdateStatePosition(WindowState state)
        {
            if (state.Anchor == null || _renderCamera == null) return;

            var target = state.GetTargetTransform();
            if (target == null) return;

            Vector3 worldPos = state.Anchor.CachedTransform.position;
            Vector2 localPoint = WorldToScreenPoint(worldPos, _renderCamera, RectTransform);

            // target是容器的子对象，设置localPosition
            if (target is RectTransform rectTarget)
            {
                rectTarget.localPosition = new Vector3(localPoint.x, localPoint.y, rectTarget.localPosition.z);
            }
            else
            {
                target.localPosition = new Vector3(localPoint.x, localPoint.y, target.localPosition.z);
            }

            state.CoordinateDirty = false;
        }

        #endregion

        #region 子对象哈希集

        /// <summary>
        /// 标记子对象哈希集需要重建
        /// </summary>
        public void MarkChildrenSetDirty()
        {
            _childrenSetDirty = true;
        }

        /// <summary>
        /// 重建子对象哈希集，用于O(1)快速查找
        /// </summary>
        public void RebuildChildrenSet()
        {
            _childrenHashSet.Clear();

            var t = transform;
            for (int i = 0; i < t.childCount; i++)
            {
                _childrenHashSet.Add(t.GetChild(i).gameObject);
            }

            _childrenSetDirty = false;
        }

        /// <summary>
        /// 检查指定对象是否在当前容器的子项中（O(1)哈希查找）
        /// </summary>
        /// <param name="obj">要检查的对象</param>
        /// <returns>是否存在于子项中</returns>
        public bool IsChildObject(GameObject obj)
        {
            if (obj == null) return false;

            if (_childrenSetDirty)
                RebuildChildrenSet();

            return _childrenHashSet.Contains(obj);
        }

        #endregion

        #region 对象池管理

        /// <summary>
        /// 从对象池获取一个WindowState实例
        /// </summary>
        private WindowState GetPooledState()
        {
            if (_statePool.Count > 0)
                return _statePool.Dequeue();

            return new WindowState();
        }

        #endregion

        #region 静态工具方法

        /// <summary>
        /// 将世界空间坐标转换到Canvas容器内的本地坐标
        /// </summary>
        /// <param name="worldPos">世界空间坐标</param>
        /// <param name="camera">使用的摄像机</param>
        /// <param name="container">目标Canvas容器的RectTransform</param>
        /// <returns>容器内的本地坐标</returns>
        public static Vector2 WorldToScreenPoint(Vector3 worldPos, Camera camera, RectTransform container)
        {
            if (camera == null || container == null)
                return Vector2.zero;

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, worldPos);

            if (container.TryGetComponent<Canvas>(out var canvas))
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay && camera != null)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        container, screenPoint, camera, out Vector2 localPoint);
                    return localPoint;
                }
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                container, screenPoint, null, out Vector2 overlayPoint);
            return overlayPoint;
        }

        /// <summary>
        /// 获取Canvas使用的渲染摄像机
        /// </summary>
        /// <param name="canvas">目标Canvas</param>
        /// <returns>渲染摄像机（如果Canvas是Overlay模式则返回MainCamera）</returns>
        public static Camera GetCanvasRenderCamera(Canvas canvas)
        {
            if (canvas == null) return Camera.main;

            if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null)
                return canvas.worldCamera;

            return Camera.main;
        }

        #endregion
    }
}
