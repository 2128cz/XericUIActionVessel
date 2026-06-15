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

            // 激活所有启用的条目实例
            foreach (var entry in Entries)
            {
                if (entry.Instance != null && entry.Enabled)
                    entry.Instance.SetActive(true);
            }

            // 激活时强制标记坐标脏，确保下一帧更新位置
            CoordinateDirty = true;
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
                    entry.Instance.SetActive(false);
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
                    WindowPool.Release(entry.WindowObject, entry.Instance);

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
            CoordinateDirty = false;
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

            foreach (var entry in Entries)
            {
                if (entry.ClampToSafeZone) anyClamp = true;
                if (entry.Enabled) anyEnabled = true;
            }

            HasAnyClamp = anyClamp;
            HasAnyEnabled = anyEnabled;
        }

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
