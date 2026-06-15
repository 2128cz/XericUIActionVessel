using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XericLibrary.Runtime.MacroLibrary;

namespace XericUI.VisualForm
{
    /// <summary>
    /// 锚点坞 - 挂载在Canvas上的主要布局组件管理器。
    /// 负责收集所有世界锚点，每帧将世界坐标转换到屏幕坐标，并管理中间窗口状态。
    /// </summary>
    [AddComponentMenu("Xeric UI Vessel/AnchorWindow/AnchorDock")]
    [DefaultExecutionOrder(31000)]
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

        [Header("安全区")]
        [SerializeField, Tooltip("安全区矩形（Canvas本地空间），窗口超出此区域视为超出安全区")]
        private Rect m_SafeZone = new Rect(.1f, .05f, .8f, .8f);

        [SerializeField, Tooltip("安全区检测模式")]
        private SafeZoneMode m_SafeZoneMode = SafeZoneMode.Rect;

        [SerializeField, Tooltip("安全区坐标单位")]
        private SafeZoneUnit m_SafeZoneUnit = SafeZoneUnit.Ratio;

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
        private Camera _worldCamera;
        private Camera _uiCamera;
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
        /// 上帧世界摄像机坐标
        /// </summary>
        private Vector3 _lastWorldCameraPosition;

        /// <summary>
        /// 上帧世界摄像机旋转
        /// </summary>
        private Quaternion _lastWorldCameraRotation;

        /// <summary>
        /// 上帧UI摄像机坐标
        /// </summary>
        private Vector3 _lastUICameraPosition;

        /// <summary>
        /// 上帧UI摄像机旋转
        /// </summary>
        private Quaternion _lastUICameraRotation;

        #endregion

        #region 公开属性

        /// <summary>
        /// 世界摄像机（用于 World→Screen 转换，通常是 MainCamera）
        /// </summary>
        public Camera WorldCamera => _worldCamera;

        /// <summary>
        /// UI摄像机（用于 Screen→Canvas 转换，Overlay时为null，Camera时为canvas.worldCamera）
        /// </summary>
        public Camera UICamera => _uiCamera;

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
            _lastWorldCameraPosition = _worldCamera ? _worldCamera.transform.position : Vector3.zero;
            _lastWorldCameraRotation = _worldCamera ? _worldCamera.transform.rotation : Quaternion.identity;
            _lastUICameraPosition = _uiCamera ? _uiCamera.transform.position : Vector3.zero;
            _lastUICameraRotation = _uiCamera ? _uiCamera.transform.rotation : Quaternion.identity;

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

            // 4. 遍历所有中间类，根据脏标记更新坐标（跳过无启用条目的状态）
            foreach (var kvp in _activeStates)
            {
                var state = kvp.Value;
                if (state == null || !state.IsEnabled) continue;
                if (!state.HasAnyEnabled) continue;

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
        /// 刷新摄像机引用：分离世界摄像机（观察3D场景）和UI摄像机（渲染Canvas）
        /// </summary>
        public void RefreshCameraReference()
        {
            // 世界摄像机：用于 World→Screen 转换
            _worldCamera = Camera.main;

            // UI摄像机：用于 Screen→Canvas 转换
            if (m_OverrideCamera != null)
            {
                _uiCamera = m_OverrideCamera;
            }
            else
            {
                if (_canvas == null)
                {
                    _canvas = GetComponent<Canvas>();
                    if (_canvas == null)
                        _canvas = GetComponentInParent<Canvas>();
                }

                _uiCamera = (_canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceCamera)
                    ? _canvas.worldCamera : null;
            }
        }

        /// <summary>
        /// 检查世界摄像机和UI摄像机是否发生变换，任意一个变化即标记脏
        /// </summary>
        private void CheckCameraDirty()
        {
            if (_worldCamera == null)
            {
                RefreshCameraReference();
                return;
            }

            bool dirty = false;

            // 检查世界摄像机变换
            var wcTransform = _worldCamera.transform;
            Vector3 wcPos = wcTransform.position;
            Quaternion wcRot = wcTransform.rotation;
            if (wcPos != _lastWorldCameraPosition || wcRot != _lastWorldCameraRotation)
            {
                dirty = true;
                _lastWorldCameraPosition = wcPos;
                _lastWorldCameraRotation = wcRot;
            }

            // 检查UI摄像机变换
            if (_uiCamera != null && _uiCamera != _worldCamera)
            {
                var uiTransform = _uiCamera.transform;
                Vector3 uiPos = uiTransform.position;
                Quaternion uiRot = uiTransform.rotation;
                if (uiPos != _lastUICameraPosition || uiRot != _lastUICameraRotation)
                {
                    dirty = true;
                    _lastUICameraPosition = uiPos;
                    _lastUICameraRotation = uiRot;
                }
            }

            _cameraDirty = dirty;
        }

        /// <summary>
        /// 更新指定中间类中所有窗口条目的屏幕坐标。
        /// 流程：计算基础坐标 → 检测溢出 → 钳制需要安全区约束的条目 → 逐个设置位置。
        /// </summary>
        private void UpdateStatePosition(WindowState state)
        {
            if (state.Anchor == null || _worldCamera == null || _canvas == null) return;
            if (state.Entries.Count == 0) return;

            Vector3 worldPos = state.Anchor.CachedTransform.position;

            if (!TryWorldToScreenPoint(worldPos, _worldCamera, _uiCamera, _canvas, RectTransform, out Vector2 basePoint))
                return;

            Rect canvasLocalRect = ((RectTransform)_canvas.transform).rect;
            bool hasClamp = state.HasAnyClamp;
            var anchorWindow = state.Anchor as IAnchorWindowEntry;

            // 将安全区从当前单位转为像素坐标
            Rect pixelSafeZone = m_SafeZoneUnit == SafeZoneUnit.Ratio
                ? RatioSafeZoneToPixel(m_SafeZone, canvasLocalRect)
                : m_SafeZone;

            // 计算基础坐标的溢出状态
            OverflowState baseOverflow = CheckOverflow(basePoint, pixelSafeZone, m_SafeZoneMode, canvasLocalRect);
            state.Overflow = baseOverflow;

            // 为基础坐标计算钳制位置（仅在需要钳制的条目上使用）
            Vector2 clampedBase = hasClamp ? ClampToSafeZone(basePoint, pixelSafeZone, m_SafeZoneMode) : basePoint;

            for (int i = 0; i < state.Entries.Count; i++)
            {
                var entry = state.Entries[i];
                if (entry.Instance == null) continue;
                if (!entry.Enabled) continue;

                Vector2 rawPoint = basePoint + entry.ScreenOffset;
                Vector2 finalPoint;

                if (entry.ClampToSafeZone && baseOverflow != OverflowState.InRange)
                {
                    finalPoint = clampedBase + entry.ScreenOffset;
                }
                else
                {
                    finalPoint = rawPoint;
                }

                // 检测逐条目溢出状态变化，触发生命周期事件
                OverflowState entryOverflow = CheckOverflow(finalPoint, pixelSafeZone, m_SafeZoneMode, canvasLocalRect);
                if (entryOverflow != entry.CurrentOverflow)
                {
                    entry.CurrentOverflow = entryOverflow;
                    anchorWindow?.OnWindowLevelChange(i, entryOverflow);
                }

                var target = entry.Instance.transform;
                if (target is RectTransform rectTarget)
                    rectTarget.anchoredPosition = finalPoint;
                else
                    target.localPosition = new Vector3(finalPoint.x, finalPoint.y, target.localPosition.z);
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
        /// 尝试将世界空间坐标转换到目标RectTransform容器内的本地坐标。
        /// 世界摄像机负责 World→Screen，UI摄像机负责 Screen→Canvas 本地。
        /// </summary>
        /// <param name="worldPos">世界坐标</param>
        /// <param name="worldCamera">世界摄像机（用于 WorldToScreenPoint，通常为MainCamera）</param>
        /// <param name="uiCamera">UI摄像机（用于 ScreenPointToLocalPointInRectangle，Overlay时为null）</param>
        /// <param name="canvas">所属Canvas</param>
        /// <param name="container">目标容器的RectTransform</param>
        /// <param name="localPoint">输出的本地坐标</param>
        /// <returns>坐标是否有效</returns>
        public static bool TryWorldToScreenPoint(Vector3 worldPos, Camera worldCamera, Camera uiCamera,
            Canvas canvas, RectTransform container, out Vector2 localPoint)
        {
            localPoint = Vector2.zero;

            if (worldCamera == null || container == null || canvas == null)
                return false;

            // 1. 世界坐标 → 屏幕像素（使用世界摄像机）
            Vector3 screenPos = worldCamera.WorldToScreenPoint(worldPos);

            if (screenPos.IsNaN() || screenPos.IsInfinity() || screenPos.z < 0f)
                return false;

            // 2. Canvas RectTransform参数
            RectTransform canvasRect = (RectTransform)canvas.transform;
            Vector2 canvasSize = canvasRect.rect.size;
            Vector2 canvasPivot = canvasRect.pivot;

            // 3. 屏幕像素 → Canvas本地坐标
            Vector2 canvasLocal;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                // Overlay：屏幕即Canvas
                canvasLocal = new Vector2(
                    screenPos.x - canvasSize.x * canvasPivot.x,
                    screenPos.y - canvasSize.y * canvasPivot.y
                );
            }
            else
            {
                // Camera模式：用UI摄像机做 Screen→Canvas 转换
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, (Vector2)screenPos, uiCamera, out canvasLocal);
            }

            if (canvasLocal.IsNaN() || canvasLocal.IsInfinity())
                return false;

            // 4. 如果container不是Canvas自身，从Canvas空间变换到container本地空间
            if (container != canvasRect)
            {
                Vector3 worldInCanvas = canvasRect.TransformPoint(new Vector3(canvasLocal.x, canvasLocal.y, 0f));
                Vector3 localInContainer = container.InverseTransformPoint(worldInCanvas);
                localPoint = new Vector2(localInContainer.x, localInContainer.y);
            }
            else
            {
                localPoint = canvasLocal;
            }

            return true;
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

        /// <summary>
        /// 检查本地坐标是否超出安全区。
        /// 矩形模式：检查点是否在Rect范围内。
        /// 椭圆模式：检查点到中心的归一化距离是否超过各轴半径。
        /// </summary>
        /// <param name="localPoint">Canvas本地坐标</param>
        /// <param name="safeZone">安全区矩形</param>
        /// <param name="mode">检测模式</param>
        /// <param name="canvasLocalRect">Canvas本地空间矩形（用于判断是否超出屏幕）</param>
        /// <returns>溢出状态（Flags可叠加）</returns>
        public static OverflowState CheckOverflow(Vector2 localPoint, Rect safeZone, SafeZoneMode mode, Rect canvasLocalRect)
        {
            OverflowState result = OverflowState.InRange;

            // 检查是否超出安全区
            bool outOfSafeZone;
            if (mode == SafeZoneMode.Ellipse)
            {
                // 椭圆：(px/rx)² + (py/ry)² > 1
                Vector2 center = safeZone.center;
                float rx = safeZone.width * 0.5f;
                float ry = safeZone.height * 0.5f;
                if (rx <= 0f || ry <= 0f)
                {
                    outOfSafeZone = false;
                }
                else
                {
                    float nx = (localPoint.x - center.x) / rx;
                    float ny = (localPoint.y - center.y) / ry;
                    outOfSafeZone = (nx * nx + ny * ny) > 1f;
                }
            }
            else
            {
                outOfSafeZone = !safeZone.Contains(localPoint);
            }

            if (outOfSafeZone)
                result |= OverflowState.OutOfSafeZone;

            // 检查是否超出屏幕
            if (!canvasLocalRect.Contains(localPoint))
                result |= OverflowState.OutOfScreen;

            return result;
        }

        /// <summary>
        /// 将本地坐标钳制到安全区边界内。
        /// 矩形模式：clamp x到[minX, maxX]，y到[minY, maxY]。
        /// 椭圆模式：沿中心方向投影到椭圆边界上。
        /// </summary>
        /// <param name="localPoint">Canvas本地坐标</param>
        /// <param name="safeZone">安全区矩形</param>
        /// <param name="mode">检测模式</param>
        /// <returns>钳制后的坐标</returns>
        public static Vector2 ClampToSafeZone(Vector2 localPoint, Rect safeZone, SafeZoneMode mode)
        {
            if (mode == SafeZoneMode.Ellipse)
            {
                Vector2 center = safeZone.center;
                float rx = safeZone.width * 0.5f;
                float ry = safeZone.height * 0.5f;

                if (rx <= 0f || ry <= 0f)
                    return center;

                Vector2 delta = localPoint - center;
                float nx = delta.x / rx;
                float ny = delta.y / ry;
                float dist = Mathf.Sqrt(nx * nx + ny * ny);

                if (dist <= 1f)
                    return localPoint; // 已在椭圆内

                // 投影到椭圆边界
                return center + delta / dist;
            }
            else
            {
                // 矩形钳制
                return new Vector2(
                    Mathf.Clamp(localPoint.x, safeZone.xMin, safeZone.xMax),
                    Mathf.Clamp(localPoint.y, safeZone.yMin, safeZone.yMax)
                );
            }
        }

        /// <summary>
        /// 将比例安全区（0-1）转换为像素安全区。
        /// </summary>
        /// <param name="ratioSafeZone">比例安全区（x,y,width,height均为0-1）</param>
        /// <param name="canvasLocalRect">Canvas本地空间矩形</param>
        /// <returns>像素安全区</returns>
        public static Rect RatioSafeZoneToPixel(Rect ratioSafeZone, Rect canvasLocalRect)
        {
            float w = canvasLocalRect.width;
            float h = canvasLocalRect.height;
            return new Rect(
                canvasLocalRect.x + ratioSafeZone.x * w,
                canvasLocalRect.y + ratioSafeZone.y * h,
                ratioSafeZone.width * w,
                ratioSafeZone.height * h
            );
        }

        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Canvas canvas = _canvas;
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>();
            if (canvas == null) return;

            Rect canvasRect = ((RectTransform)canvas.transform).rect;
            Rect pixelSafeZone = m_SafeZoneUnit == SafeZoneUnit.Ratio
                ? RatioSafeZoneToPixel(m_SafeZone, canvasRect)
                : m_SafeZone;

            Vector2 center = pixelSafeZone.center;
            Vector3 size = new Vector3(pixelSafeZone.width, pixelSafeZone.height, 0.001f);

            // 填充 — 橘黄色半透明
            Gizmos.color = new Color(1f, 0.65f, 0.15f, 0.25f);
            Gizmos.matrix = canvas.transform.localToWorldMatrix;
            Gizmos.DrawCube(center, size);

            // 边框 — 橘红色
            Gizmos.color = new Color(1f, 0.35f, 0.05f, 0.9f);
            Gizmos.DrawWireCube(center, size);

            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
