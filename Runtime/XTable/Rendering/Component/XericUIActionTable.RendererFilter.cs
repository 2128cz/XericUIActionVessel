using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
            public CellAssembler.PooledImage SelectBg;
            public CellAssembler.PooledImage HLine;
            public CellAssembler.PooledImage VLine;
        }

        #endregion

        #region 追踪对象生命周期

        private void ReleaseTrackedCell(int row, int col)
        {
            var key = (row, col);
            if (!m_CellTracked.TryGetValue(key, out var objs)) return;
            m_Assembler?.ReleaseText(objs.CellText);
            m_Assembler?.ReleaseImage(objs.CellImage);
            m_Assembler?.ReleaseImage(objs.SelectBg);
            m_Assembler?.ReleaseImage(objs.HLine);
            m_Assembler?.ReleaseImage(objs.VLine);
            m_CellTracked.Remove(key);
        }

        private void ReleaseAllTrackedCells()
        {
            foreach (var kv in m_CellTracked)
            {
                var objs = kv.Value;
                m_Assembler?.ReleaseText(objs.CellText);
                m_Assembler?.ReleaseImage(objs.CellImage);
                m_Assembler?.ReleaseImage(objs.SelectBg);
                m_Assembler?.ReleaseImage(objs.HLine);
                m_Assembler?.ReleaseImage(objs.VLine);
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
                    if (!TableData.IsCellMerged(r, c) && IsCellVisible(r, c))
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
                if (!TableData.IsCellMerged(r, c) && IsCellVisible(r, c))
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

        /// <summary>渲染单个单元格的完整内容（选择高亮 + 网格线 + 文本/图片/预制体）。</summary>
        private void RenderCellContent(int row, int col, string defaultNs,
            bool withGridLines, bool editorMode)
        {
            XTableCellData data = TableData.GetCell(row, col);
            string cellNS = (data != null && !string.IsNullOrEmpty(data.StyleNamespace))
                ? data.StyleNamespace : defaultNs;

            float cellX = GetCellContentX(col);
            float cellY = GetCellContentY(row);
            float cellW = m_ColWidths[col];
            float cellH = m_RowHeights[row];

            // 合并源：扩展尺寸
            if (TableData.IsMergeSource(row, col, out int mergeRS, out int mergeCS))
            {
                for (int i = 1; i < mergeCS && col + i < m_ColWidths.Length; i++)
                    cellW += m_ColWidths[col + i];
                for (int i = 1; i < mergeRS && row + i < m_RowHeights.Length; i++)
                    cellH += m_RowHeights[row + i];
            }

            var objs = new CellTrackedObjs();

            // ── 选择高亮 ──
            ApplySelectionHighlight(row, col, cellX, cellY, cellW, cellH, cellNS, editorMode, ref objs);

            // ── 网格线（仅完整刷新时）──
            if (withGridLines)
                ApplyGridLines(cellX, cellY, cellW, cellH, cellNS, editorMode, ref objs);

            // ── 单元格内容 ──
            if (data != null)
            {
                switch (data.ContentType)
                {
                    case CellContentType.Text:
                        RenderCellText(data, cellX, cellY, cellW, cellH, cellNS, editorMode, ref objs);
                        break;
                    case CellContentType.Image:
                        RenderCellImage(data, cellX, cellY, cellW, cellH, cellNS, ref objs);
                        break;
                    case CellContentType.Prefab:
                        RenderCellPrefab(data, row, col, cellX, cellY, cellW, cellH);
                        break;
                }
            }

            m_CellTracked[(row, col)] = objs;
            m_LastRenderedCells.Add((row, col));
        }

        private void ApplySelectionHighlight(int row, int col,
            float cellX, float cellY, float cellW, float cellH,
            string cellNS, bool editorMode, ref CellTrackedObjs objs)
        {
            bool inRange = IsInSelectionRange(row, col);
            bool isFocused = row == SelectionHandle.StartRow && col == SelectionHandle.StartCol;

            if (!inRange && !isFocused) return;

            string key = inRange ? "cell_multiselect" : "cell_select";
            var img = m_Assembler.GetImage(key, cellNS);
            if (img == null) return;
            img.Rect.anchoredPosition = new Vector2(cellX, cellY);
            img.Rect.sizeDelta = new Vector2(cellW, cellH);

            if (editorMode)
            {
                Color selColor = GetCellSelectionBgColor(cellNS);
                if (inRange)
                    img.Image.color = isFocused
                        ? new Color(selColor.r + Config.FocusRAdd, selColor.g + Config.FocusGAdd,
                            selColor.b + Config.FocusBAdd, selColor.a * Config.FocusAlphaMul)
                        : new Color(selColor.r, selColor.g, selColor.b,
                            selColor.a * Config.MultiSelectAlphaFactor);
                else
                    img.Image.color = selColor;
            }
            else
            {
                if (inRange)
                    img.Image.color = isFocused ? Config.RuntimeFocusColor : Config.RuntimeMultiSelectColor;
                else
                    img.Image.color = Config.RuntimeSelectionColor;
            }

            objs.SelectBg = img;
        }

        private void ApplyGridLines(float cellX, float cellY, float cellW, float cellH,
            string cellNS, bool editorMode, ref CellTrackedObjs objs)
        {
            Color borderColor;
            float borderWidth;

            if (editorMode)
            {
                borderColor = GetCellBorderColor(cellNS);
                borderWidth = GetCellBorderWidth(cellNS);
            }
            else
            {
                borderColor = Config.RuntimeBorderColor;
                borderWidth = Config.RuntimeBorderWidth;
            }

            // 水平网格线（底部）
            var hLine = m_Assembler.GetImage("cell_hline", cellNS);
            if (hLine != null)
            {
                hLine.Rect.anchoredPosition = new Vector2(cellX, cellY - cellH);
                hLine.Rect.sizeDelta = new Vector2(cellW, borderWidth);
                hLine.Image.color = borderColor;
                objs.HLine = hLine;
            }

            // 垂直网格线（右侧）
            var vLine = m_Assembler.GetImage("cell_vline", cellNS);
            if (vLine != null)
            {
                vLine.Rect.anchoredPosition = new Vector2(cellX + cellW - borderWidth, cellY);
                vLine.Rect.sizeDelta = new Vector2(borderWidth, cellH);
                vLine.Image.color = borderColor;
                objs.VLine = vLine;
            }
        }

        private void RenderCellText(XTableCellData data,
            float cellX, float cellY, float cellW, float cellH,
            string cellNS, bool editorMode, ref CellTrackedObjs objs)
        {
            if (string.IsNullOrEmpty(data.Text)) return;

            var txt = m_Assembler.GetText("cell_text", cellNS);
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

        private void RenderCellImage(XTableCellData data,
            float cellX, float cellY, float cellW, float cellH,
            string cellNS, ref CellTrackedObjs objs)
        {
            if (data.Image == null) return;

            Sprite sprite = GetOrCreateSprite(data.Image);
            var img = m_Assembler.GetImage("cell_image", cellNS);
            if (img == null) return;
            img.Rect.anchoredPosition = new Vector2(cellX + CellPadding, cellY);
            img.Rect.sizeDelta = new Vector2(cellW - CellPadding2, cellH);
            img.Image.sprite = sprite;

            objs.CellImage = img;
        }

        private void RenderCellPrefab(XTableCellData data, int row, int col,
            float cellX, float cellY, float cellW, float cellH)
        {
            if (data.Prefab == null) return;

            GameObject instance = m_PrefabPool.Acquire(data.Prefab, row, col);
            if (instance == null) return;
            RectTransform prt = instance.GetComponent<RectTransform>();
            prt.anchoredPosition = new Vector2(cellX, cellY);
            prt.sizeDelta = new Vector2(cellW, cellH);
            m_ActivePrefabMap[(row, col)] = instance;
            data.OnPrefabAcquire?.Invoke(instance);
        }

        #endregion

        #region 完整刷新

        public void RefreshView()
        {
            if (m_Assembler == null)
            {
                if (rectTransform != null)
                    m_Assembler = new CellAssembler(ContentTransform, m_DefaultStyleNamespace);
                else return;
            }

            if (m_RowHeights == null || m_ColWidths == null) return;
            if (m_RowHeights.Length == 0 || m_ColWidths.Length == 0) return;

            CreateBackground();

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Canvas.ForceUpdateCanvases();
#endif

            RecalculateTotalSize();
            UpdateVisibleRect();

            // 确保 ScrollRect Content 尺寸正确
            RectTransform ctRt = ContentTransform as RectTransform;
            if (ctRt != null)
            {
                float pad2 = m_ContentOffset * 2f;
                ctRt.sizeDelta = new Vector2(m_TotalWidth + pad2, m_TotalHeight + pad2);
            }

            // 更新背景尺寸
            if (m_BackgroundGo != null)
            {
                var bgRt = m_BackgroundGo.GetComponent<RectTransform>();
                bgRt.sizeDelta = new Vector2(m_TotalWidth, m_TotalHeight);
            }

            // 归还所有追踪对象 + 预制体
            if (m_ActivePrefabMap != null)
            {
                foreach (var kv in m_ActivePrefabMap)
                {
                    var cellData = TableData.GetCell(kv.Key.Item1, kv.Key.Item2);
                    cellData?.OnPrefabRelease?.Invoke(kv.Value);
                }
                m_ActivePrefabMap.Clear();
            }
            m_PrefabPool?.ReturnAll();
            ReleaseAllTrackedCells();

            // 清理孤立子对象
            CleanOrphanChildren();

            // 渲染所有可见单元格
#if UNITY_EDITOR
            bool editorMode = !Application.isPlaying;
#else
            bool editorMode = false;
#endif
            string ns = m_DefaultStyleNamespace;
            ComputeVisibleCellsInto(m_LastRenderedCells);
            foreach (var (r, c) in m_LastRenderedCells)
                RenderCellContent(r, c, ns, withGridLines: true, editorMode: editorMode);

            // 边框 + 冻结
            RebuildBorders();
            RenderFrozenCells();

            m_DirtyFlag.Clear();
            OnTableRefreshed?.Invoke();
            SetChildrenVisible(m_ChildrenVisible);
        }

        #endregion

        #region 轻刷新（增量——仅处理可见性变更）

        private void RefreshViewLight()
        {
            if (m_Assembler == null)
            {
                if (rectTransform != null)
                    m_Assembler = new CellAssembler(ContentTransform, m_DefaultStyleNamespace);
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

            // 选区变更时：所有单元格的选择高亮需要重绘，但预制体不受影响
            bool selectionChanged = m_DirtyFlag.Has(TableDirtyType.SelectionChanged);
            if (selectionChanged)
            {
                ReleaseAllTrackedCells();
            }

            // 1. 计算新一帧的可见单元格（滚动路径用简单范围遍历，避免四叉树/分配开销）
            var newVisible = new HashSet<(int, int)>();
            ComputeVisibleCellsScrollInto(newVisible);

            // 2. 回收不再可见的追踪对象
            if (!selectionChanged)
            {
                m_ReusableCellList.Clear();
                foreach (var cell in m_LastRenderedCells)
                    if (!newVisible.Contains(cell))
                        m_ReusableCellList.Add(cell);
                foreach (var cell in m_ReusableCellList)
                    ReleaseTrackedCell(cell.Item1, cell.Item2);
            }

            // 3. 回收不再可见的预制体
            if (m_ActivePrefabMap != null && m_ActivePrefabMap.Count > 0)
            {
                m_ReusableCellList.Clear();
                foreach (var kv in m_ActivePrefabMap)
                    if (!newVisible.Contains(kv.Key))
                        m_ReusableCellList.Add(kv.Key);

                foreach (var key in m_ReusableCellList)
                {
                    var cellData = TableData.GetCell(key.Item1, key.Item2);
                    cellData?.OnPrefabRelease?.Invoke(m_ActivePrefabMap[key]);
                    m_PrefabPool.Release(m_ActivePrefabMap[key]);
                    m_ActivePrefabMap.Remove(key);
                }
            }

            // 4. 渲染单元格：选区变更时全量重渲所有可见单元格，否则仅新进入的
            foreach (var cell in newVisible)
            {
                if (selectionChanged || !m_LastRenderedCells.Contains(cell))
                    RenderCellContent(cell.Item1, cell.Item2, ns,
                        withGridLines: false, editorMode: editorMode);
            }

            // 5. 交换：将 newVisible 作为下一帧的比较基准
            (m_LastRenderedCells, newVisible) = (newVisible, m_LastRenderedCells);

            m_DirtyFlag.ClearFlags(TableDirtyType.ScrollChanged | TableDirtyType.SelectionChanged);
            OnTableRefreshed?.Invoke();
            SetChildrenVisible(m_ChildrenVisible);
        }

        #endregion

        #region 孤立子对象清理

        private void CleanOrphanChildren()
        {
            Transform contentT = ContentTransform;
            Transform borderT = contentT?.Find("_TableBorders");

            for (int i = contentT.childCount - 1; i >= 0; i--)
            {
                var child = contentT.GetChild(i);
                // 跳过池管理的对象（标记有 POOL_FLAGS = DontSave | HideInHierarchy）
                if ((child.hideFlags & HideFlags.DontSave) != 0) continue;
                if (m_BackgroundGo != null && child.gameObject == m_BackgroundGo) continue;
                if (borderT != null && child == borderT) continue;

                child.gameObject.hideFlags = HideFlags.None;
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        #endregion

        #region 背景创建

        private void CreateBackground()
        {
            if (m_BackgroundGo != null) return;

            m_BackgroundGo = new GameObject("_TableBackground",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            m_BackgroundGo.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
            m_BackgroundGo.transform.SetParent(ContentTransform, false);
            m_BackgroundGo.transform.SetAsFirstSibling();

            RectTransform bgRt = m_BackgroundGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 1);
            bgRt.anchorMax = new Vector2(0, 1);
            bgRt.pivot = new Vector2(0, 1);
            bgRt.anchoredPosition = new Vector2(m_ContentOffset, -m_ContentOffset);
            bgRt.sizeDelta = new Vector2(m_TotalWidth, m_TotalHeight);

            var bgImg = m_BackgroundGo.GetComponent<Image>();
            bgImg.color = GetCellBgColor(m_DefaultStyleNamespace);
            bgImg.raycastTarget = false;
        }

        #endregion
    }
}
