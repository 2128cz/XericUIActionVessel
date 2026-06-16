using System.Collections.Generic;
using UnityEngine;

namespace XericUI.VisualForm
{
    /// <summary>
    /// 高级锚点坞 — 在 AnchorDock 基础上扩展屏幕空间碰撞分离功能。
    /// 通过四叉树加速碰撞检测，迭代推挤分离重叠的UI窗口条目。
    /// 
    /// 核心流程：
    ///   1. 判断是否有脏标记（坐标变化），无脏则跳过碰撞
    ///   2. 重置所有条目碰撞状态（从原始 ScreenOffset 重新计算，不依赖上一帧）
    ///   3. 刷新各 WindowState 中条目的 Canvas 本地空间碰撞矩形缓存
    ///   4. 构建四叉树 → 迭代推挤分离 → 写入 CollisionOffset
    ///   5. 下一帧 UpdateStatePosition 通过 EffectiveOffset 自动应用碰撞偏移
    /// </summary>
    [AddComponentMenu("Xeric UI Vessel/AnchorWindow/AdvancedAnchorDock")]
    public class AdvancedAnchorDock : AnchorDock
    {
        [Header("碰撞分离")]
        [SerializeField, Tooltip("启用屏幕空间碰撞分离计算")]
        private bool m_EnableCollision = false;

        [SerializeField, Tooltip("碰撞计算执行间隔（秒），值越小越频繁")]
        private float m_CollisionInterval = 0.04f;

        [SerializeField, Tooltip("碰撞分离最大迭代次数（10为推荐值）")]
        private int m_CollisionIterations = 10;

        [SerializeField, Tooltip("碰撞分离推力系数（0-1，0.5为推荐值）")]
        private float m_CollisionPushForce = 0.5f;

        [SerializeField, Tooltip("四叉树每节点最大容纳数")]
        private int m_QuadTreeMaxPerNode = 5;

        [SerializeField, Tooltip("四叉树最大深度")]
        private int m_QuadTreeMaxDepth = 4;

        // --- 运行时 ---

        private float _collisionTimer;
        private QuadTree<WindowEntry> _quadTree;
        private readonly List<WindowEntry> _collisionEntries = new List<WindowEntry>();
        private readonly HashSet<WindowEntry> _overlapResults = new HashSet<WindowEntry>();

        #region 生命周期

        protected override void OnEnable()
        {
            base.OnEnable();
            _collisionTimer = 0f;
            _quadTree = null;
        }

        /// <summary>
        /// 在基类位置更新之前注入碰撞分离计算。
        /// 仅在启用碰撞且存在脏标记时才执行。
        /// 摄像机变化、坐标变化或首次激活时立即触发，其余情况按间隔节流。
        /// </summary>
        protected override void OnBeforePositionUpdates()
        {
            if (!m_EnableCollision) return;

            _collisionTimer += Time.unscaledDeltaTime;

            // 摄像机变化时立即触发碰撞（如场景启动）
            bool forceCheck = IsCameraDirty;
            bool timerElapsed = _collisionTimer >= m_CollisionInterval;

            if (!forceCheck && !timerElapsed) return;

            if (timerElapsed)
                _collisionTimer %= m_CollisionInterval;

            // 仅在有脏标记时才计算（摄像机变化或坐标变化触发，碰撞偏移本身不作为触发条件）
            if (!NeedsCollisionRecalculation()) return;

            ComputeCollisionSeparation();
        }

        #endregion

        #region 碰撞流程 — 封装方法

        /// <summary>
        /// 是否需要重新计算碰撞。仅在摄像机变化或窗口坐标变化时返回 true。
        /// 使用独立的 CollisionDirty 标记（不被 UpdateStatePosition 清除），而非 CoordinateDirty。
        /// </summary>
        private bool NeedsCollisionRecalculation()
        {
            if (IsCameraDirty) return true;

            foreach (var kvp in ActiveStates)
            {
                var state = kvp.Value;
                if (state == null || !state.IsEnabled) continue;
                if (state.CollisionDirty)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 主碰撞分离计算入口。
        /// 流程：重置 → 刷新矩形 → 构建四叉树 → 迭代推挤 → 应用结果。
        /// </summary>
        private void ComputeCollisionSeparation()
        {
            // 1. 重置所有条目碰撞状态（从原始 ScreenOffset 重新计算）
            foreach (var kvp in ActiveStates)
            {
                if (kvp.Value == null) continue;
                kvp.Value.ResetCollisionState();
            }

            // 2. 刷新所有条目的 Canvas 本地空间碰撞矩形缓存
            RefreshAllCollisionRects();

            // 3. 收集参与碰撞的条目
            CollectCollisionEntries();
            if (_collisionEntries.Count < 2) return;

            // 4. 构建四叉树
            BuildQuadTree();

            // 5. 迭代推挤分离
            IterateCollisionPush();

            // 6. 标记碰撞结果，让 EffectiveOffset 在下次 UpdateStatePosition 中生效
            foreach (var kvp in ActiveStates)
            {
                if (kvp.Value == null) continue;
                kvp.Value.ApplyCollisionResult();
            }
        }

        /// <summary>
        /// 刷新所有活跃 WindowState 中各条目的碰撞矩形缓存。
        /// 每个矩形位于 Canvas 本地空间，中心 = 锚点世界→Canvas坐标 + ScreenOffset，
        /// 尺寸 = 条目实例 RectTransform.rect.size。
        /// </summary>
        private void RefreshAllCollisionRects()
        {
            var camWorld = WorldCamera;
            var camUI = UICamera;
            var canvas = Canvas;
            var container = RectTransform;

            if (camWorld == null || canvas == null || container == null) return;

            foreach (var kvp in ActiveStates)
            {
                var state = kvp.Value;
                if (state == null || !state.IsEnabled || !state.HasAnyEnabled) continue;
                if (!state.HasAnyCollisionEntry) continue;

                var anchor = state.Anchor;
                if (anchor == null) continue;

                state.ClearCollisionRects();

                Vector3 worldPos = anchor.CachedTransform.position;
                if (!TryWorldToScreenPoint(worldPos, camWorld, camUI, canvas, container, out Vector2 canvasPos))
                    continue;

                for (int i = 0; i < state.Entries.Count; i++)
                {
                    var entry = state.Entries[i];
                    if (!entry.EnableCollision || !entry.Enabled || entry.Instance == null) continue;

                    RectTransform rt = entry.Instance.transform as RectTransform;
                    Vector2 size = (rt != null) ? rt.rect.size : Vector2.one * 10f;
                    Vector2 center = canvasPos + entry.ScreenOffset;
                    Rect rect = new Rect(center - size * 0.5f, size);

                    state.SetCollisionRect(entry, rect);
                }
            }
        }

        /// <summary>
        /// 收集所有参与碰撞的活跃条目到一个扁平列表中。
        /// </summary>
        private void CollectCollisionEntries()
        {
            _collisionEntries.Clear();

            foreach (var kvp in ActiveStates)
            {
                var state = kvp.Value;
                if (state == null || !state.IsEnabled || !state.HasAnyEnabled) continue;
                if (!state.HasAnyCollisionEntry) continue;

                for (int i = 0; i < state.Entries.Count; i++)
                {
                    var entry = state.Entries[i];
                    if (!entry.EnableCollision || !entry.Enabled || entry.Instance == null) continue;

                    Rect rect = state.GetCollisionRect(entry);
                    if (rect.width <= 0f || rect.height <= 0f) continue;

                    _collisionEntries.Add(entry);
                }
            }
        }

        /// <summary>
        /// 构建四叉树：计算所有条目包围盒 → 清空重建 → 插入所有条目。
        /// 树中存储的是 WindowEntry，getRectFunc 返回的是含 CollisionOffset 和 Margin 的实时矩形。
        /// </summary>
        private void BuildQuadTree()
        {
            // 计算包围盒
            Rect bounds = GetEntryPushedRect(_collisionEntries[0]);
            for (int i = 1; i < _collisionEntries.Count; i++)
            {
                var r = GetEntryPushedRect(_collisionEntries[i]);
                bounds = Encapsulate(bounds, r);
            }

            // 略微扩展边界
            bounds = new Rect(bounds.x - 1f, bounds.y - 1f, bounds.width + 2f, bounds.height + 2f);

            if (_quadTree == null)
                _quadTree = new QuadTree<WindowEntry>(bounds, GetEntryPushedRect, m_QuadTreeMaxPerNode, m_QuadTreeMaxDepth);
            else
                _quadTree.Rebuild(_collisionEntries);
        }

        /// <summary>
        /// 迭代推挤分离算法。
        /// 对每个条目从四叉树查询重叠对象，沿最小重叠轴推开，重复直到无重叠或达到最大迭代次数。
        /// </summary>
        private void IterateCollisionPush()
        {
            float pushForce = Mathf.Clamp01(m_CollisionPushForce);
            int maxIter = Mathf.Clamp(m_CollisionIterations, 1, 50);

            for (int iter = 0; iter < maxIter; iter++)
            {
                bool anyOverlap = false;

                for (int i = 0; i < _collisionEntries.Count; i++)
                {
                    var entryA = _collisionEntries[i];
                    var stateA = entryA.State;
                    if (stateA == null) continue;

                    Rect rectA = stateA.GetEntryPushedRect(entryA);

                    _quadTree.Retrieve(rectA, _overlapResults);

                    foreach (var entryB in _overlapResults)
                    {
                        if (entryB == entryA) continue;

                        var stateB = entryB.State;
                        if (stateB == null) continue;

                        Rect rectB = stateB.GetEntryPushedRect(entryB);

                        if (!CollisionUtils.Overlaps(rectA, rectB)) continue;

                        anyOverlap = true;

                        Vector2 push = CollisionUtils.CalculateRepulsion(rectA, rectB, pushForce);
                        if (push == Vector2.zero) continue;

                        stateA.SetCollisionOffset(entryA, entryA.CollisionOffset + push);
                        stateB.SetCollisionOffset(entryB, entryB.CollisionOffset - push);

                        // 更新 rectA 以反映新的碰撞偏移（同一次迭代内继续使用）
                        rectA = stateA.GetEntryPushedRect(entryA);
                    }
                }

                if (!anyOverlap) break;
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取条目当前的推挤位置矩形（含 CollisionOffset + CollisionMargin）。
        /// 作为四叉树的 getRectFunc 委托，实时反映条目最新碰撞偏移。
        /// </summary>
        private static Rect GetEntryPushedRect(WindowEntry entry)
        {
            if (entry?.State == null) return Rect.zero;
            return entry.State.GetEntryPushedRect(entry);
        }

        /// <summary>
        /// 计算两个矩形的包围盒。
        /// </summary>
        private static Rect Encapsulate(Rect a, Rect b)
        {
            float xMin = Mathf.Min(a.xMin, b.xMin);
            float yMin = Mathf.Min(a.yMin, b.yMin);
            float xMax = Mathf.Max(a.xMax, b.xMax);
            float yMax = Mathf.Max(a.yMax, b.yMax);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        #endregion
    }
}
