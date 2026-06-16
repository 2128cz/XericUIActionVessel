using System.Collections.Generic;
using UnityEngine;

namespace XericUI.VisualForm
{
    /// <summary>
    /// 中间窗口状态类 - 管理锚点窗口和锚点坞之间的中间产物。
    /// 负责所有窗口条目的实例生命周期管理、坐标脏标记和屏幕定位。
    /// 使用对象池管理，避免频繁创建销毁。
    /// 
    /// 生命周期：
    ///   OnStart  - 从对象池取出时调用（初始化/复用，解析所有窗口条目）
    ///   OnEnable - 锚点激活时调用（脏更新触发，激活所有启用条目）
    ///   OnDisable- 锚点隐藏时调用（脏更新触发，隐藏所有条目）
    ///   OnEnd    - 回收到对象池时调用（清理/回收所有条目实例）
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
        /// 窗口条目列表（来自AnchorWindow的WindowEntries的运行时副本）
        /// 每个条目维护自己的实例、偏移量和激活状态
        /// </summary>
        public List<WindowEntry> Entries { get; private set; } = new List<WindowEntry>();

        /// <summary>
        /// 当前是否处于激活状态
        /// </summary>
        public bool IsEnabled { get; private set; }

        /// <summary>
        /// 当前窗口溢出状态（由坞每帧更新）
        /// </summary>
        public OverflowState Overflow { get; set; }

        /// <summary>
        /// 是否存在至少一个需要钳制在安全区内的条目
        /// </summary>
        public bool HasAnyClamp { get; private set; }

        /// <summary>
        /// 是否存在至少一个启用的条目
        /// </summary>
        public bool HasAnyEnabled { get; private set; }

        /// <summary>
        /// 是否存在至少一个参与碰撞的条目
        /// </summary>
        public bool HasAnyCollisionEntry { get; private set; }

        /// <summary>
        /// 是否存在任何条目当前处于碰撞中（CollisionOffset 非零）。
        /// 由 SetCollisionOffset 自动维护，供外部查询。
        /// </summary>
        public bool HasAnyCollisionDirty { get; private set; }

        /// <summary>
        /// 碰撞重算标记 — 坐标自上次碰撞后是否变化，用于触发碰撞重新计算。
        /// 由 CheckPositionDirty 设置，碰撞计算后由 ResetCollisionState 复位。
        /// 与 CoordinateDirty 分离：CoordinateDirty 在 UpdateStatePosition 后被清除，
        /// 而此标记保留到碰撞计算完成。
        /// </summary>
        public bool CollisionDirty { get; set; }

        /// <summary>
        /// 坐标脏标记 - 当锚点世界坐标发生变化时标记
        /// </summary>
        public bool CoordinateDirty { get; set; }

        /// <summary>
        /// 上帧锚点世界坐标
        /// </summary>
        public Vector3 LastWorldPosition => _lastPosition;

        /// <summary>
        /// 上帧世界坐标（用于比较变化量）
        /// </summary>
        private Vector3 _lastPosition;

        #region 生命周期

        /// <summary>
        /// 从对象池取出时的初始化（Start事件）
        /// </summary>
        public void OnStart(WorldAnchor anchor, AnchorDock dock)
        {
            ResetState();

            Anchor = anchor;
            Dock = dock;

            // 解析所有窗口条目（原型对象或预制体实例化）
            ResolveWindowEntries();

            // 初始化位置记录
            if (anchor != null)
                _lastPosition = anchor.CachedTransform.position;
        }

        /// <summary>
        /// 解析所有窗口条目：遍历AnchorWindow的WindowEntries，
        /// 为每个条目判断窗口对象是原型还是预制体，并完成实例获取或创建。
        /// </summary>
        public void ResolveWindowEntries()
        {
            if (Dock == null || Anchor == null) return;

            if (Anchor is not IAnchorWindowEntry { DefaultTypeWindowEntriesCount: > 0 } anchorWindow)
                return;

            Entries.Clear();

            foreach (var srcEntry in anchorWindow.DefaultTypeWindowEntries)
            {
                if (srcEntry == null || srcEntry.WindowObject == null)
                    continue;

                var entry = srcEntry; // 直接复用WindowEntry引用，运行时状态写回

                // 通过锚点坞的哈希检查窗口对象是否在子项中
                entry.IsPrefab = !Dock.IsChildObject(entry.WindowObject);

                if (entry.IsPrefab)
                {
                    // 预制体：从对象池获取或创建实例，挂到坞下
                    entry.Instance = WindowPool.Get(entry.WindowObject, Dock.transform);
                }
                else
                {
                    // 原型：已在子项中的实例对象，直接引用
                    entry.Instance = entry.WindowObject;
                }

                // 通知条目实例已创建（无论是新实例化还是从池中复用的）
                entry.OnInstanceCreated(this, entry.Instance);

                // 将WindowState引用注入条目，使其能通过MarkDirty通知坞
                entry.State = this;

                // 立即按entry配置的Enabled决定实例的active状态。
                // OnInstanceCreated可能已修改Enabled为false，必须在此处应用，
                // 避免实例在后续OnEnable中被跳过激活但保持active的问题。
                if (entry.Instance != null)
                    entry.Instance.SetActive(entry.Enabled);

                Entries.Add(entry);
            }

            RefreshFlags();
        }

        /// <summary>
        /// 锚点激活时的处理（Enable事件，由脏更新触发）
        /// </summary>
        public void OnEnable()
        {
            IsEnabled = true;

            // 确保窗口条目已解析
            if (Entries.Count == 0)
                ResolveWindowEntries();

            // 按每个条目的Enabled配置统一设置实例active状态
            foreach (var entry in Entries)
            {
                if (entry.Instance != null)
                {
                    bool shouldActive = entry.Enabled;
                    entry.Instance.SetActive(shouldActive);
                    if (shouldActive)
                        entry.OnInstanceEnable(this, entry.Instance);
                }
            }

            // 激活时强制标记坐标脏，确保下一帧更新位置
            CoordinateDirty = true;
            CollisionDirty = true;
        }

        /// <summary>
        /// 锚点隐藏时的处理（Disable事件，由脏更新触发）
        /// </summary>
        public void OnDisable()
        {
            IsEnabled = false;

            foreach (var entry in Entries)
            {
                if (entry.Instance != null)
                {
                    entry.Instance.SetActive(false);
                    entry.OnInstanceDisable(this, entry.Instance);
                }
            }
        }

        /// <summary>
        /// 回收到对象池时的清理（End事件）
        /// </summary>
        public void OnEnd()
        {
            // 回收所有预制体实例到对象池
            foreach (var entry in Entries)
            {
                if (entry.IsPrefab && entry.Instance != null)
                {
                    entry.OnInstanceRecycle(this, entry.Instance);
                    WindowPool.Release(entry.WindowObject, entry.Instance);
                }

                entry.Instance = null;
                entry.IsPrefab = false;
            }

            Entries.Clear();
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
            IsEnabled = false;
            Overflow = OverflowState.InRange;
            HasAnyClamp = false;
            HasAnyEnabled = false;
            HasAnyCollisionEntry = false;
            CoordinateDirty = false;
            CollisionDirty = false;
            _lastPosition = Vector3.zero;
        }

        /// <summary>
        /// 刷新聚合标记：遍历所有条目，更新 HasAnyClamp 和 HasAnyEnabled。
        /// 由 AnchorWindow 的 SetEntryXxx 方法或 ResolveWindowEntries 调用。
        /// </summary>
        public void RefreshFlags()
        {
            bool anyClamp = false;
            bool anyEnabled = false;
            bool anyCollision = false;

            foreach (var entry in Entries)
            {
                if (entry.ClampToSafeZone) anyClamp = true;
                if (entry.Enabled) anyEnabled = true;
                if (entry.EnableCollision) anyCollision = true;
            }

            HasAnyClamp = anyClamp;
            HasAnyEnabled = anyEnabled;
            HasAnyCollisionEntry = anyCollision;
        }

        /// <summary>
        /// 立即将条目的Enabled状态应用到实例并触发对应生命周期回调。
        /// 仅在锚点处于激活状态（IsEnabled）时生效，否则仅刷新标记等待下次OnEnable。
        /// </summary>
        internal void ApplyEntryEnabled(WindowEntry entry, bool enabled)
        {
            if (entry == null || entry.Instance == null) return;
            if (!IsEnabled) return;

            bool wasActive = entry.Instance.activeSelf;
            entry.Instance.SetActive(enabled);

            if (enabled && !wasActive)
                entry.OnInstanceEnable(this, entry.Instance);
            else if (!enabled && wasActive)
                entry.OnInstanceDisable(this, entry.Instance);
        }

        #region 碰撞偏移管理

        /// <summary>
        /// 逐条目的碰撞矩形缓存（Canvas本地空间，不含碰撞偏移，不含Margin）。
        /// 在碰撞计算前由 AdvancedAnchorDock 负责分批刷新。
        /// </summary>
        private readonly Dictionary<WindowEntry, Rect> _collisionRects = new Dictionary<WindowEntry, Rect>();

        /// <summary>
        /// 获取条目的原始碰撞矩形缓存（Canvas本地空间，不含CollisionOffset和Margin）。
        /// </summary>
        public Rect GetCollisionRect(WindowEntry entry)
        {
            _collisionRects.TryGetValue(entry, out var r);
            return r;
        }

        /// <summary>
        /// 设置条目的碰撞矩形缓存。
        /// </summary>
        internal void SetCollisionRect(WindowEntry entry, Rect rect)
        {
            _collisionRects[entry] = rect;
        }

        /// <summary>
        /// 清空所有碰撞矩形缓存。
        /// </summary>
        internal void ClearCollisionRects()
        {
            _collisionRects.Clear();
        }

        /// <summary>
        /// 获取条目用于碰撞检测的矩形（Canvas本地空间，含 CollisionMargin 扩展）。
        /// 每次碰撞检测时以此扩展后的矩形判断是否重叠。
        /// </summary>
        public Rect GetEntryCollisionRect(WindowEntry entry)
        {
            if (entry == null || !_collisionRects.TryGetValue(entry, out Rect baseRect))
                return Rect.zero;

            float m = entry.CollisionMargin;
            if (m > 0f)
                return new Rect(baseRect.x - m, baseRect.y - m, baseRect.width + m * 2f, baseRect.height + m * 2f);
            return baseRect;
        }

        /// <summary>
        /// 获取条目在当前碰撞推挤位置下的矩形（Canvas本地空间，含 CollisionOffset + CollisionMargin）。
        /// 供碰撞计算迭代过程中使用。
        /// </summary>
        public Rect GetEntryPushedRect(WindowEntry entry)
        {
            Rect rect = GetEntryCollisionRect(entry);
            if (rect == Rect.zero) return Rect.zero;
            rect.position += entry.CollisionOffset;
            return rect;
        }

        /// <summary>
        /// 设置条目的碰撞偏移量。自动标记 IsColliding=true 和 HasAnyCollisionDirty。
        /// 碰撞偏移量相对于 ScreenOffset 空间（Canvas本地空间）。
        /// </summary>
        public void SetCollisionOffset(WindowEntry entry, Vector2 offset)
        {
            if (entry == null) return;
            entry.CollisionOffset = offset;
            if (offset != Vector2.zero)
            {
                entry.IsColliding = true;
                HasAnyCollisionDirty = true;
            }
        }

        /// <summary>
        /// 重置所有条目碰撞状态（碰撞偏移清零，IsColliding置false）。
        /// 由 AdvancedAnchorDock 在每次碰撞计算开始时调用，确保从原始位置重新计算。
        /// </summary>
        public void ResetCollisionState()
        {
            CollisionDirty = false;
            HasAnyCollisionDirty = false;
            foreach (var entry in Entries)
            {
                entry.CollisionOffset = Vector2.zero;
                entry.IsColliding = false;
            }
        }

        /// <summary>
        /// 碰撞计算完成后调用：标记 CoordinateDirty，让下一帧位置更新通过 EffectiveOffset 自动获取碰撞偏移。
        /// </summary>
        public void ApplyCollisionResult()
        {
            foreach (var entry in Entries)
            {
                if (entry.IsColliding)
                {
                    CoordinateDirty = true;
                    break;
                }
            }
        }

        #endregion

        /// <summary>
        /// 检查锚点坐标是否发生变化，超过阈值则标记脏
        /// 由锚点坞在每帧调用
        /// </summary>
        public void CheckPositionDirty(float threshold)
        {
            if (Anchor == null) return;

            var currentPos = Anchor.CachedTransform.position;
            if (Vector3.Distance(_lastPosition, currentPos) > threshold)
            {
                CoordinateDirty = true;
                CollisionDirty = true;  // 独立的碰撞重算标记，不会在 UpdateStatePosition 后被清除
                _lastPosition = currentPos;
            }
        }

        /// <summary>
        /// 获取第一个有效窗口条目的Transform（兼容旧API）
        /// </summary>
        public Transform GetTargetTransform()
        {
            foreach (var entry in Entries)
            {
                if (entry.Instance != null)
                    return entry.Instance.transform;
            }
            if (Anchor != null)
                return Anchor.CachedTransform;
            return null;
        }

        /// <summary>
        /// 获取所有需要定位的Transform
        /// </summary>
        public IEnumerable<Transform> GetAllTargetTransforms()
        {
            foreach (var entry in Entries)
                if (entry.Instance != null)
                    yield return entry.Instance.transform;
        }

        #endregion

        #region 静态工具方法 — 指向示意旋转计算

        /// <summary>
        /// 计算从钳制位置指向屏幕中心的方向矢量。
        /// </summary>
        /// <param name="clampedPosition">钳制后的屏幕坐标</param>
        /// <param name="screenCenter">屏幕中心坐标</param>
        /// <returns>归一化方向矢量（从clampedPosition指向screenCenter）</returns>
        public static Vector2 GetLookAtDirection(Vector2 clampedPosition, Vector2 screenCenter)
        {
            Vector2 dir = screenCenter - clampedPosition;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
        }

        /// <summary>
        /// 计算Z轴旋转角度，使初始指向矢量旋转后对准从钳制位置到屏幕中心的方向。
        /// 典型用法：传入 Vector2.up 作为初始指向，得到 transform.eulerAngles.z 的值。
        /// </summary>
        /// <param name="clampedPosition">钳制后的屏幕坐标</param>
        /// <param name="screenCenter">屏幕中心坐标</param>
        /// <param name="initialDirection">初始指向矢量（如 Vector2.up 表示默认向上）</param>
        /// <returns>Z轴旋转角度（度），可直接赋给 transform.eulerAngles.z</returns>
        public static float GetLookAtAngle(Vector2 clampedPosition, Vector2 screenCenter, Vector2 initialDirection)
        {
            Vector2 dir = GetLookAtDirection(clampedPosition, screenCenter);
            return Vector2.SignedAngle(initialDirection, dir);
        }

        /// <summary>
        /// 计算旋转四元数，使初始指向矢量旋转后对准从钳制位置到屏幕中心的方向。
        /// 典型用法：传入 Vector2.up 作为初始指向，得到 transform.rotation 的值。
        /// </summary>
        /// <param name="clampedPosition">钳制后的屏幕坐标</param>
        /// <param name="screenCenter">屏幕中心坐标</param>
        /// <param name="initialDirection">初始指向矢量（如 Vector2.up 表示默认向上）</param>
        /// <returns>Z轴旋转四元数，可直接赋给 transform.rotation</returns>
        public static Quaternion GetLookAtRotation(Vector2 clampedPosition, Vector2 screenCenter, Vector2 initialDirection)
        {
            float angle = GetLookAtAngle(clampedPosition, screenCenter, initialDirection);
            return Quaternion.Euler(0f, 0f, angle);
        }

        #endregion
    }
}
