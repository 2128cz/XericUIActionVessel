using UnityEngine;

namespace XericUI.VisualForm
{
    /// <summary>
    /// 中间窗口状态类 - 管理锚点窗口和锚点坞之间的中间产物。
    /// 负责窗口实例的生命周期管理、坐标脏标记和屏幕定位。
    /// 使用对象池管理，避免频繁创建销毁。
    /// 
    /// 生命周期：
    ///   OnStart  - 从对象池取出时调用（初始化/复用）
    ///   OnEnable - 锚点激活时调用（脏更新触发）
    ///   OnDisable- 锚点隐藏时调用（脏更新触发）
    ///   OnEnd    - 回收到对象池时调用（清理/回收）
    /// </summary>
    public class WindowState
    {
        /// <summary>
        /// 关联的世界锚点
        /// </summary>
        public WorldAnchor Anchor { get; private set; }

        /// <summary>
        /// 关联的锚点坞
        /// </summary>
        public AnchorDock Dock { get; private set; }

        /// <summary>
        /// 原始窗口对象（来自AnchorWindow的引用）
        /// </summary>
        private GameObject _windowObject;

        /// <summary>
        /// 实例化的窗口对象（如果是预制体则为此实例，否则为_windowObject本身）
        /// </summary>
        private GameObject _instance;

        /// <summary>
        /// 是否为预制体
        /// </summary>
        private bool _isPrefab;

        /// <summary>
        /// 是否为预制体（公开属性）
        /// </summary>
        public bool IsPrefab => _isPrefab;

        /// <summary>
        /// 窗口实例对象（预制体实例化后的对象，或子项中已有的对象）
        /// </summary>
        public GameObject WindowInstance => _instance;

        /// <summary>
        /// 上帧锚点世界坐标
        /// </summary>
        public Vector3 LastWorldPosition => _lastPosition;

        /// <summary>
        /// 当前是否处于激活状态
        /// </summary>
        public bool IsEnabled { get; private set; }

        /// <summary>
        /// 坐标脏标记 - 当锚点世界坐标发生变化时标记
        /// </summary>
        public bool CoordinateDirty { get; set; }

        /// <summary>
        /// 上帧世界坐标（用于比较变化量）
        /// </summary>
        private Vector3 _lastPosition;

        #region 生命周期

        /// <summary>
        /// 从对象池取出时的初始化（Start事件）
        /// </summary>
        /// <param name="anchor">关联的锚点</param>
        /// <param name="dock">关联的锚点坞</param>
        public void OnStart(WorldAnchor anchor, AnchorDock dock)
        {
            ResetState();

            Anchor = anchor;
            Dock = dock;

            // 解析窗口实例（原型对象或预制体实例化）
            ResolveWindowInstance();

            // 初始化位置记录
            if (anchor != null)
                _lastPosition = anchor.CachedTransform.position;
        }

        /// <summary>
        /// 解析窗口实例：自动判断窗口对象是原型（已在子项中）还是预制体，并完成实例获取或创建。
        /// 如果锚点是AnchorWindow且携带窗口对象，则根据是否在坞子项中决定处理方向：
        ///   - 在子项中 → 直接使用该对象作为实例（原型模式）
        ///   - 不在子项中 → 从WindowPool获取或实例化预制体
        /// 如果锚点不是AnchorWindow或无窗口对象，则不创建实例。
        /// </summary>
        public void ResolveWindowInstance()
        {
            if (Dock == null || Anchor == null) return;

            var anchorWindow = Anchor as AnchorWindow;
            if (anchorWindow == null || anchorWindow.WindowObject == null)
                return;

            _windowObject = anchorWindow.WindowObject;

            // 通过锚点坞的哈希检查窗口对象是否在子项中
            _isPrefab = !Dock.IsChildObject(_windowObject);

            if (_isPrefab)
            {
                // 预制体：从对象池获取或创建实例，挂到坞下
                _instance = WindowPool.Get(_windowObject, Dock.transform);
            }
            else
            {
                // 原型：已在子项中的实例对象，直接引用
                _instance = _windowObject;
            }

            // 同步标记到AnchorWindow
            anchorWindow.IsPrefab = _isPrefab;
        }

        /// <summary>
        /// 锚点激活时的处理（Enable事件，由脏更新触发）
        /// </summary>
        public void OnEnable()
        {
            IsEnabled = true;

            // 如果实例尚不存在（如纯WorldAnchor没有窗口对象），尝试解析
            if (_instance == null)
                ResolveWindowInstance();

            if (_instance != null)
                _instance.SetActive(true);

            // 激活时强制标记坐标脏，确保下一帧更新位置
            CoordinateDirty = true;
        }

        /// <summary>
        /// 锚点隐藏时的处理（Disable事件，由脏更新触发）
        /// </summary>
        public void OnDisable()
        {
            IsEnabled = false;

            if (_instance != null)
                _instance.SetActive(false);
        }

        /// <summary>
        /// 回收到对象池时的清理（End事件）
        /// </summary>
        public void OnEnd()
        {
            // 回收预制体实例到对象池
            if (_isPrefab && _instance != null)
            {
                WindowPool.Release(_windowObject, _instance);
            }

            ResetState();
        }

        #endregion

        #region 状态管理

        /// <summary>
        /// 复位所有状态（用于对象池复用）
        /// </summary>
        private void ResetState()
        {
            Anchor = null;
            Dock = null;
            _windowObject = null;
            _instance = null;
            _isPrefab = false;
            IsEnabled = false;
            CoordinateDirty = false;
            _lastPosition = Vector3.zero;
        }

        /// <summary>
        /// 检查锚点坐标是否发生变化，超过阈值则标记脏
        /// 由锚点坞在每帧调用
        /// </summary>
        /// <param name="threshold">变化阈值</param>
        public void CheckPositionDirty(float threshold)
        {
            if (Anchor == null) return;

            var currentPos = Anchor.CachedTransform.position;
            if (Vector3.Distance(_lastPosition, currentPos) > threshold)
            {
                CoordinateDirty = true;
                _lastPosition = currentPos;
            }
        }

        /// <summary>
        /// 获取当前需要定位的Transform目标（实例化的窗口对象或锚点自身）
        /// </summary>
        /// <returns>目标Transform</returns>
        public Transform GetTargetTransform()
        {
            if (_instance != null)
                return _instance.transform;
            if (Anchor != null)
                return Anchor.CachedTransform;
            return null;
        }

        #endregion
    }
}
