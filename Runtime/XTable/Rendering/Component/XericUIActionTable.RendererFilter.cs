using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XericLibrary.Runtime.MacroLibrary;
using XericUI.XTable.Core;
using XericUI.XTable.Rendering.Elements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace XericUI.XTable.Rendering.Component
{
    public partial class XericUIActionTable
    {
        #region 增量渲染追踪

        [NonSerialized] private HashSet<(int, int)> m_LastRenderedCells = new();
        [NonSerialized] private HashSet<(int, int)> m_VisibleCellsScratch = new();
        [NonSerialized] private Dictionary<(int, int), CellTrackedObjs> m_CellTracked = new();

        /// <summary>可复用列表，避免每帧分配 GC</summary>
        [NonSerialized] private List<(int, int)> m_ReusableCellList = new(capacity: 128);

        [NonSerialized] private List<QuadrantResult> m_ReusableQuadrants = new(capacity: 32);

        /// <summary>控制滚动时是否使用四叉树（大表格推荐）</summary>
        private bool UseQuadtreeForScroll => m_Quadtree != null && m_Quadtree.IsBuilt
                                                                && m_RowHeights.Length * m_ColWidths.Length > 2000;

        private struct CellTrackedObjs
        {
            public CellAssembler.PooledText CellText;
            public CellAssembler.PooledImage CellImage;
        }

        #endregion

        #region 追踪对象生命周期

        private void ReleaseTrackedCell(int row, int col)
        {
            var key = (row, col);
            if (m_CellTracked.TryGetValue(key, out var objs))
            {
                m_Assembler?.ReleaseText(objs.CellText);
                m_Assembler?.ReleaseImage(objs.CellImage);
                m_CellTracked.Remove(key);
            }
            ReleasePrefabCell(row, col);
        }

        private void ReleasePrefabCell(int row, int col)
        {
            var key = (row, col);
            if (m_ActivePrefabMap == null || !m_ActivePrefabMap.TryGetValue(key, out var instance)) return;
            m_PrefabPool?.Release(instance, TableData.GetCell(row, col)?.OnPrefabRelease);
            m_ActivePrefabMap.Remove(key);
        }

        private void ReleaseAllTrackedCells()
        {
            foreach (var kv in m_CellTracked)
            {
                var objs = kv.Value;
                m_Assembler?.ReleaseText(objs.CellText);
                m_Assembler?.ReleaseImage(objs.CellImage);
            }

            m_CellTracked.Clear();
            m_LastRenderedCells.Clear();
        }

        #endregion

        #region 可见单元格计算

        /// <summary>计算当前可见矩形内所有未被合并覆盖的单元格</summary>
        private void ComputeVisibleCellsInto(HashSet<(int, int)> result)
        {
            result.Clear();

            if (m_Quadtree != null && m_Quadtree.IsBuilt)
            {
                m_ReusableQuadrants.Clear();
                m_Quadtree.QueryVisible(m_VisibleRect, m_ReusableQuadrants);
                foreach (var q in m_ReusableQuadrants)
                {
                    if (string.IsNullOrEmpty(q.Namespace) || q.Cells == null) continue;
                    foreach (var cell in q.Cells)
                    {
                        if (!TableData.IsCellMerged(cell.Item1, cell.Item2)
                            && IsNormalRegionCell(cell.Item1, cell.Item2)
                            && IsCellVisible(cell.Item1, cell.Item2))
                            result.Add(cell);
                    }
                }
            }
            else
            {
                CalcVisibleRange(out int sr, out int er, out int sc, out int ec);
                for (int r = sr; r < er; r++)
                for (int c = sc; c < ec; c++)
                {
                    if (!TableData.IsCellMerged(r, c) && IsNormalRegionCell(r, c) && IsCellVisible(r, c))
                        result.Add((r, c));
                }
            }
        }

        /// <summary>[Scroll] 仅用简单范围遍历计算可见单元格（避免四叉树遍历及 QuadrantResult 分配）</summary>
        private void ComputeVisibleCellsScrollInto(HashSet<(int, int)> result)
        {
            result.Clear();
            CalcVisibleRange(out int sr, out int er, out int sc, out int ec);
            for (int r = sr; r < er; r++)
            for (int c = sc; c < ec; c++)
            {
                if (!TableData.IsCellMerged(r, c) && IsNormalRegionCell(r, c) && IsCellVisible(r, c))
                    result.Add((r, c));
            }
        }

        #endregion

        #region 单元格坐标

        private float GetCellContentX(int col)
        {
            return GetColLeftX(col) + m_ContentOffset;
        }

        private float GetCellContentY(int row)
        {
            return -GetRowTopY(row) - m_ContentOffset;
        }

        #endregion

        #region 单元格内容渲染（统一入口）

        /// <summary>渲染单个单元格内容（选择高亮 + 文本/图片/预制体）。</summary>
        private void RenderCellContent(int row, int col, string defaultNs, bool editorMode)
        {
            XTableCellData data = TableData.GetCell(row, col);
            string cellNS = ResolveCellNamespace(data);

            float cellX = GetCellContentX(col);
            float cellY = GetCellContentY(row);
            float cellW = m_ResolvedColWidths[col];
            float cellH = m_ResolvedRowHeights[row];

            // 合并源：扩展尺寸
            if (TableData.IsMergeSource(row, col, out int mergeRS, out int mergeCS))
            {
                for (int i = 1; i < mergeCS && col + i < m_ResolvedColWidths.Length; i++)
                    cellW += m_ResolvedColWidths[col + i];
                for (int i = 1; i < mergeRS && row + i < m_ResolvedRowHeights.Length; i++)
                    cellH += m_ResolvedRowHeights[row + i];
            }

            int cellId = row * m_ColWidths.Length + col;
            var key = (row, col);
            m_CellTracked.TryGetValue(key, out var previousObjs);
            var objs = new CellTrackedObjs();

            // ── 单元格内容 ──
            if (data != null)
            {
                switch (data.ContentType)
                {
                    case CellContentType.Text:
                        RenderCellText(cellId, data, cellX, cellY, cellW, cellH, cellNS, editorMode, ref objs);
                        break;
                    case CellContentType.Image:
                        RenderCellImage(cellId, data, cellX, cellY, cellW, cellH, cellNS, ref objs);
                        break;
                    case CellContentType.Prefab:
                        RenderCellPrefab(cellId, data, row, col, cellX, cellY, cellW, cellH);
                        break;
                }
            }

            if (previousObjs.CellText != null && previousObjs.CellText != objs.CellText)
                m_Assembler.ReleaseText(previousObjs.CellText);
            if (previousObjs.CellImage != null && previousObjs.CellImage != objs.CellImage)
                m_Assembler.ReleaseImage(previousObjs.CellImage);

            if (objs.CellText != null || objs.CellImage != null)
                m_CellTracked[key] = objs;
            else
                m_CellTracked.Remove(key);
            m_LastRenderedCells.Add(key);
        }

        private void RenderCellText(int cellId, XTableCellData data,
            float cellX, float cellY, float cellW, float cellH,
            string cellNS, bool editorMode, ref CellTrackedObjs objs)
        {
            if (string.IsNullOrEmpty(data.Text)) return;

            var txt = m_Assembler.GetText(cellId, "cell_text", cellNS, ContentTransform);
            if (txt == null) return;
            txt.Rect.anchoredPosition = new Vector2(cellX + CellPadding, cellY);
            txt.Rect.sizeDelta = new Vector2(cellW - CellPadding2, cellH);
            txt.Text.SetText(data.Text);

            if (editorMode)
            {
                txt.Text.color = GetCellFgColor(cellNS);
                txt.Text.fontSize = GetCellFontSize(cellNS);
                txt.Text.alignment = TMPro.TextAlignmentOptions.Midline;
                txt.Text.font = GetCellFontAsset(cellNS) ?? txt.Text.font;
            }

            objs.CellText = txt;
        }

        private void RenderCellImage(int cellId, XTableCellData data,
            float cellX, float cellY, float cellW, float cellH,
            string cellNS, ref CellTrackedObjs objs)
        {
            if (data.Image == null) return;

            Sprite sprite = GetOrCreateSprite(data.Image);
            var img = m_Assembler.GetImage(cellId, "cell_image", cellNS, ContentTransform);
            if (img == null) return;
            img.Rect.anchoredPosition = new Vector2(cellX + CellPadding, cellY);
            img.Rect.sizeDelta = new Vector2(cellW - CellPadding2, cellH);
            img.Image.sprite = sprite;

            objs.CellImage = img;
        }

        private void RenderCellPrefab(int cellId, XTableCellData data, int row, int col,
            float cellX, float cellY, float cellW, float cellH)
        {
            if (data.Prefab == null) return;

            var key = (row, col);
            GameObject instance = null;
            bool wasActive = m_ActivePrefabMap != null
                             && m_ActivePrefabMap.TryGetValue(key, out instance);
            if (wasActive && !m_PrefabPool.IsInstanceOfPrefab(instance, data.Prefab))
            {
                m_PrefabPool.Release(instance, TableData.GetCell(row, col)?.OnPrefabRelease);
                m_ActivePrefabMap.Remove(key);
                wasActive = false;
            }
            if (!wasActive)
                instance = m_PrefabPool.Acquire(data.Prefab, cellId);
            if (instance == null) return;
            RectTransform prt = instance.GetComponent<RectTransform>();
            prt.anchoredPosition = new Vector2(cellX, cellY);
            prt.sizeDelta = new Vector2(cellW, cellH);
            m_ActivePrefabMap[key] = instance;
            if (!wasActive)
                data.OnPrefabAcquire?.Invoke(instance);
        }

        #endregion

        #region 完整刷新

        private void ReleaseRenderedBindings(Dictionary<GameObject, System.Action<GameObject>> prefabReleaseCallbacks = null)
        {
            // 结构重建必须解除所有 cellId 映射；不能只依赖当前可见区域的 tracked 集合。
            m_Assembler?.ReleaseAllBindings();
            ReleaseAllTrackedCells();
            ReleaseFrozenCells();
            m_LastRenderedCells.Clear();
            m_PrefabPool?.ReturnAll(instance =>
            {
                if (prefabReleaseCallbacks != null && prefabReleaseCallbacks.TryGetValue(instance, out var callback))
                    callback(instance);
            });
            m_ActivePrefabMap?.Clear();
        }

        /// <summary>结构删除后的唯一绑定重建入口；普通刷新和滚动不得调用。</summary>
        private void RebuildRenderBindings(Dictionary<GameObject, System.Action<GameObject>> prefabReleaseCallbacks)
        {
            ReleaseRenderedBindings(prefabReleaseCallbacks);
        }

        public void RefreshView(bool releaseBindings = true)
        {
            TableDirtyType refreshedFlags = m_DirtyFlag.Flags;
            if (m_Assembler == null)
            {
                if (rectTransform != null)
                    m_Assembler = new CellAssembler(ContentTransform, m_DefaultStyleNamespace,
                        Config.TextPoolMaxCapacity, Config.ImagePoolMaxCapacity,
                        Config.TextPoolRouteBucketCount, Config.ImagePoolRouteBucketCount);
                else return;
            }

            if (m_RowHeights == null || m_ColWidths == null) return;
            if (m_RowHeights.Length == 0 || m_ColWidths.Length == 0) return;

            UpdateResolvedCellSizes();
            CreateBackground();

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Canvas.ForceUpdateCanvases();
#endif

            RecalculateTotalSize();
            UpdateVisibleRect();

            // 确保 ScrollRect Content 尺寸正确。
            // 有 ScrollRect 但 Content 为空时 ContentTransform 是表自己：这时写尺寸＝把表本身改成
            // 内容尺寸（预制体一保存就是那个"奇怪的尺寸"），所以先不写。
            RectTransform ctRt = m_ScrollRect != null && m_ScrollRect.content == null
                ? null
                : ContentTransform as RectTransform;
            if (ctRt != null)
            {
                float pad2 = m_ContentOffset * 2f;
                ctRt.sizeDelta = new Vector2(m_TotalWidth + pad2, m_TotalHeight + pad2);
            }

            UpdateBackground();

            // 清理孤立子对象
            CleanOrphanChildren();

            // 同步主区可见集合：只释放不再可见的绑定，保留仍可见单元格的路由归属。
#if UNITY_EDITOR
            bool editorMode = !Application.isPlaying;
#else
            bool editorMode = false;
#endif
            string ns = m_DefaultStyleNamespace;
            ComputeVisibleCellsInto(m_VisibleCellsScratch);
            foreach (var cell in m_LastRenderedCells)
                if (!m_VisibleCellsScratch.Contains(cell))
                    ReleaseTrackedCell(cell.Item1, cell.Item2);
            foreach (var cell in m_VisibleCellsScratch)
                RenderCellContent(cell.Item1, cell.Item2, ns, editorMode);
            (m_LastRenderedCells, m_VisibleCellsScratch) = (m_VisibleCellsScratch, m_LastRenderedCells);

            // 主 Content 已先渲染，冻结区随后复用其释放的对象；边框按区域最后提交。
            RenderFrozenCells(true);
            RebuildBorders();

            m_DirtyFlag.ClearFlags(refreshedFlags);
            OnTableRefreshed?.Invoke();
            SetChildrenVisible(m_ChildrenVisible);
        }

        #endregion

        #region 轻刷新（增量——仅处理可见性变更）

        private void RefreshViewLight()
        {
            TableDirtyType refreshedFlags = m_DirtyFlag.Flags;
            if (m_Assembler == null)
            {
                if (rectTransform != null)
                    m_Assembler = new CellAssembler(ContentTransform, m_DefaultStyleNamespace,
                        Config.TextPoolMaxCapacity, Config.ImagePoolMaxCapacity,
                        Config.TextPoolRouteBucketCount, Config.ImagePoolRouteBucketCount);
                else return;
            }

            if (m_RowHeights == null || m_ColWidths == null) return;
            if (m_RowHeights.Length == 0 || m_ColWidths.Length == 0) return;

            UpdateVisibleRect();

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Canvas.ForceUpdateCanvases();
#endif

#if UNITY_EDITOR
            bool editorMode = !Application.isPlaying;
#else
            bool editorMode = false;
#endif
            string ns = m_DefaultStyleNamespace;

            // 选区由固定 Overlay 绘制，选择变化无需回收或重绘单元格内容。

            // 1. 计算新一帧的可见单元格（滚动路径用简单范围遍历）。
            ComputeVisibleCellsScrollInto(m_VisibleCellsScratch);

            // 2. 先释放离屏单元格的 tracked 槽位与预制体实例；Counter 仅在全部槽位释放后整体归还。
            foreach (var cell in m_LastRenderedCells)
                if (!m_VisibleCellsScratch.Contains(cell))
                    ReleaseTrackedCell(cell.Item1, cell.Item2);

            // 3. 仅渲染新进入的单元格。
            foreach (var cell in m_VisibleCellsScratch)
                if (!m_LastRenderedCells.Contains(cell))
                    RenderCellContent(cell.Item1, cell.Item2, ns, editorMode);

            // 冻结列、冻结行与左上角在 Content 后更新，并复用同一全局对象池。
            RenderFrozenCells(false);

            // 5. 交换：scratch 不作为 Compute 输出前保留旧集合。
            (m_LastRenderedCells, m_VisibleCellsScratch) = (m_VisibleCellsScratch, m_LastRenderedCells);

            m_DirtyFlag.ClearFlags(refreshedFlags & (TableDirtyType.ScrollChanged | TableDirtyType.SelectionChanged));
            OnTableRefreshed?.Invoke();
            SetChildrenVisible(m_ChildrenVisible);
        }

        #endregion

        #region 孤立子对象清理

        private void CleanOrphanChildren()
        {
            Transform contentT = ContentTransform;
            if (contentT == null) return;
            // 骨架缺失时 ContentTransform 回落到表自己：这时"清理孤立子物体"清的是
            // 表自己的子物体（Viewport/Content），越清越空 ⇒ 一个都不清。
            if (m_ScrollRect != null && m_ScrollRect.content == null && ReferenceEquals(contentT, rectTransform))
                return;
            Transform borderT = contentT.Find("_TableBorders");

            for (int i = contentT.childCount - 1; i >= 0; i--)
            {
                var child = contentT.GetChild(i);
                // 池管理的对象（POOL_FLAGS = DontSave | HideInHierarchy）照旧跳过；
                // 但历史上叠出来的重复背景要清掉：背景缓存是 [NonSerialized]，域重载后必丢，
                // 而 DontSave 的 _TableBackground 还活在场景里，不清理就会一层层叠加
                // （旧层按旧尺寸留在原位、且 HideInHierarchy 看不见，表现为边框错位/内容被盖）。
                if ((child.hideFlags & HideFlags.DontSave) != 0)
                {
                    if (child.name == "_TableBackground" &&
                        (m_BackgroundRenderer == null || child.gameObject != m_BackgroundRenderer.gameObject))
                    {
                        child.gameObject.hideFlags = HideFlags.None;
                        if (Application.isPlaying) Destroy(child.gameObject);
                        else DestroyImmediate(child.gameObject);
                    }
                    continue;
                }
                if (m_BackgroundRenderer != null && child.gameObject == m_BackgroundRenderer.gameObject) continue;
                if (borderT != null && child == borderT) continue;
                // 预制体自己的子物体归预制体所有，编辑模式 DestroyImmediate 会抛 InvalidOperationException。
                // 实例和 Prefab 模式（资产）两种上下文都要跳过，否则“进去一个样、出来一个样”。
#if UNITY_EDITOR
                if (!Application.isPlaying &&
                    (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(child.gameObject)
                     || UnityEditor.PrefabUtility.IsPartOfPrefabAsset(child.gameObject))) continue;
#endif

                child.gameObject.hideFlags = HideFlags.None;
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        #endregion

        #region 背景创建

        /// <summary>运行时渲染子物体的统一标记：不存盘、隐藏，且在编辑器里不可编辑（避免被记成预制体改动）。</summary>
        private const HideFlags RuntimeRendererFlags = HideFlags.DontSave | HideFlags.HideInHierarchy | HideFlags.NotEditable;

        private void CreateBackground()
        {
            Transform contentT = ContentTransform;
            if (m_BackgroundRenderer == null && contentT != null)
            {
                // 域重载会清空 [NonSerialized] 缓存，但 DontSave 的 _TableBackground 还活在场景里。
                // 先收养它，否则每次重载都会再建一层，旧层按旧尺寸留在原位并盖住内容。
                var existing = contentT.Find("_TableBackground");
                if (existing != null)
                {
                    m_BackgroundRenderer = existing.GetComponent<XTableBackgroundRenderer>();
                    if (m_BackgroundRenderer != null)
                    {
                        existing.gameObject.hideFlags = RuntimeRendererFlags;
                        var adoptedSelection = existing.GetComponent<XTableSelectionInput>();
                        if (adoptedSelection != null) adoptedSelection.Configure(this);
                    }
                }
            }
            if (m_BackgroundRenderer != null)
            {
                UpdateBackground();
                return;
            }

            var backgroundGo = new GameObject("_TableBackground",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(XTableBackgroundRenderer),
                typeof(XTableSelectionInput));
            backgroundGo.hideFlags = RuntimeRendererFlags;
            backgroundGo.transform.SetParent(ContentTransform, false);
            backgroundGo.transform.SetAsFirstSibling();

            RectTransform backgroundRt = backgroundGo.GetComponent<RectTransform>();
            backgroundRt.anchorMin = new Vector2(0, 1);
            backgroundRt.anchorMax = new Vector2(0, 1);
            backgroundRt.pivot = new Vector2(0, 1);
            backgroundRt.anchoredPosition = new Vector2(m_ContentOffset, -m_ContentOffset);
            backgroundRt.sizeDelta = new Vector2(m_TotalWidth, m_TotalHeight);

            m_BackgroundRenderer = backgroundGo.GetComponent<XTableBackgroundRenderer>();
            m_BackgroundRenderer.raycastTarget = true;
            backgroundGo.GetComponent<XTableSelectionInput>().Configure(this);
            UpdateBackground();
        }

        private void UpdateBackground()
        {
            if (m_BackgroundRenderer == null) return;

            m_BackgroundRenderer.rectTransform.sizeDelta = new Vector2(m_TotalWidth, m_TotalHeight);
            string baseNamespace = GetSingleStyleNamespace();
            var rects = new List<XTableBackgroundRenderer.RectData>
            {
                new(new Rect(0f, 0f, m_TotalWidth, m_TotalHeight), GetCellBgColor(baseNamespace))
            };

            if (HasMultipleStyleNamespaces())
            {
                EnsureQuadtreeBuilt();
                if (m_Quadtree != null && m_Quadtree.IsBuilt)
                {
                    m_ReusableQuadrants.Clear();
                    m_Quadtree.QueryAll(m_ReusableQuadrants);
                    foreach (var quadrant in m_ReusableQuadrants)
                    {
                        if (string.IsNullOrEmpty(quadrant.Namespace) || quadrant.Namespace == baseNamespace)
                            continue;
                        rects.Add(new XTableBackgroundRenderer.RectData(quadrant.Region,
                            GetCellBgColor(quadrant.Namespace)));
                    }
                }
            }
            else
            {
                m_Quadtree?.Clear();
            }

            m_BackgroundRenderer.ReplaceRects(rects);
        }

        private void DestroyBackgroundRenderer()
        {
            if (m_BackgroundRenderer == null) return;

            if (Application.isPlaying) Destroy(m_BackgroundRenderer.gameObject);
            else DestroyImmediate(m_BackgroundRenderer.gameObject);
            m_BackgroundRenderer = null;
        }

        #endregion
    }
}