using System;
using System.Collections.Generic;
using UnityEngine;
using XericUI.Core.Base;
using XericUI.XTable.Core;

namespace XericUI.XTable.Rendering.Component
{
    /// <summary>
    /// 表格数据与坐标计算——可见性判定、坐标公式、尺寸重算、四叉树维护。
    /// </summary>
    public partial class XericUIActionTable
    {
        #region 可见性判定

        /// <summary>判断指定单元格是否与当前可见矩形有重叠（用于裁剪剔除）</summary>
        private bool IsCellVisible(int row, int col)
        {
            // 计入 m_ContentOffset 以匹配单元格在 Content 中的实际渲染位置
            float cellLeft = GetColLeftX(col) + m_ContentOffset;
            float cellRight = cellLeft + m_ColWidths[col];
            float cellTop = GetRowTopY(row) + m_ContentOffset;
            float cellBottom = cellTop + m_RowHeights[row];

            return cellRight > m_VisibleRect.x && cellLeft < m_VisibleRect.xMax &&
                   cellBottom > m_VisibleRect.y && cellTop < m_VisibleRect.yMax;
        }

        #endregion

        #region 渲染分区

        private bool IsNormalRegionCell(int row, int col)
        {
            return row >= Mathf.Clamp(m_FreezeRowCount, 0, m_RowHeights?.Length ?? 0)
                && col >= Mathf.Clamp(m_FreezeColCount, 0, m_ColWidths?.Length ?? 0);
        }

        #endregion

        #region 坐标公式

        private float GetRowTopY(int row)
        {
            float y = 0;
            for (int i = 0; i < row && i < m_RowHeights.Length; i++)
                y += m_RowHeights[i];
            return y;
        }

        private float GetColLeftX(int col)
        {
            float x = 0;
            for (int i = 0; i < col && i < m_ColWidths.Length; i++)
                x += m_ColWidths[i];
            return x;
        }

        #endregion

        #region Sprite 缓存

        /// <summary>将 Texture2D 转为 Sprite（带缓存），用于 Image 类型单元格渲染</summary>
        private Sprite GetOrCreateSprite(Texture2D texture)
        {
            if (texture == null) return null;

            if (m_SpriteCache == null)
                m_SpriteCache = new Dictionary<Texture2D, Sprite>();

            if (!m_SpriteCache.TryGetValue(texture, out Sprite sprite))
            {
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                m_SpriteCache[texture] = sprite;
            }
            return sprite;
        }

        #endregion

        #region 尺寸计算与可见矩形

        private void RecalculateTotalSize()
        {
            m_TotalWidth = 0;
            if (m_ColWidths != null)
                foreach (float w in m_ColWidths)
                    m_TotalWidth += w;

            m_TotalHeight = 0;
            if (m_RowHeights != null)
                foreach (float h in m_RowHeights)
                    m_TotalHeight += h;

            m_ContentOffset = GetCellBorderWidth(m_DefaultStyleNamespace) * 0.5f;
            float pad = m_ContentOffset * 2f;

            if (m_ScrollRect != null && m_ScrollRect.content != null)
                m_ScrollRect.content.sizeDelta = new Vector2(
                    m_TotalWidth + pad, m_TotalHeight + pad);
        }

        private void UpdateVisibleRect()
        {
            m_VisibleRect = new Rect(0, 0, m_TotalWidth, m_TotalHeight);

            if (m_ScrollRect == null || m_ScrollRect.viewport == null || m_ScrollRect.content == null)
                return;

            RectTransform vt = m_ScrollRect.viewport;
            RectTransform ct = m_ScrollRect.content as RectTransform;
            if (vt == null || ct == null) return;

            // Content 的 anchor/pivot 在 Awake 中强制设为 (0,1)，anchoredPosition
            // 直接表示 Content 左上角相对于 Viewport 左上角的偏移。
            Vector2 ap = ct.anchoredPosition;
            m_VisibleRect = new Rect(ap.x, ap.y, vt.rect.width, vt.rect.height);
        }

        private void CalcVisibleRange(out int sr, out int er, out int sc, out int ec)
        {
            sr = 0;
            er = 0;
            sc = 0;
            ec = 0;

            float accY = 0;
            for (int r = 0; r < m_RowHeights.Length; r++)
            {
                float rowBottom = accY + m_RowHeights[r];
                if (rowBottom > m_VisibleRect.y && accY < m_VisibleRect.yMax)
                {
                    if (er == 0 && sr == 0) sr = r;
                    er = r + 1;
                }

                accY = rowBottom;
                if (accY > m_VisibleRect.yMax) break;
            }

            float accX = 0;
            for (int c = 0; c < m_ColWidths.Length; c++)
            {
                float colRight = accX + m_ColWidths[c];
                if (colRight > m_VisibleRect.x && accX < m_VisibleRect.xMax)
                {
                    if (ec == 0 && sc == 0) sc = c;
                    ec = c + 1;
                }

                accX = colRight;
                if (accX > m_VisibleRect.xMax) break;
            }

            if (sr < 0) sr = 0;
            if (sc < 0) sc = 0;
        }

        #endregion

        #region 四叉树

        /// <summary>强制重建四叉树（仅多样式表会实际构建）。</summary>
        public void ForceRebuildQuadtree()
        {
            m_QuadtreeDirty = true;
            EnsureQuadtreeBuilt();
        }

        private string ResolveCellNamespace(XTableCellData data)
        {
            return data != null ? data.GetEffectiveStyleNamespace(m_DefaultStyleNamespace) : m_DefaultStyleNamespace;
        }

        private bool HasMultipleStyleNamespaces()
        {
            RefreshActiveStyleNamespaces();
            return m_ActiveStyleNamespaces.Count > 1;
        }

        private string GetSingleStyleNamespace()
        {
            RefreshActiveStyleNamespaces();
            foreach (string ns in m_ActiveStyleNamespaces) return ns;
            return m_DefaultStyleNamespace;
        }

        private void RefreshActiveStyleNamespaces()
        {
            if (!m_StyleNamespaceDirty) return;
            m_ActiveStyleNamespaces.Clear();
            if (m_TableData == null || m_RowHeights == null || m_ColWidths == null)
            {
                m_ActiveStyleNamespaces.Add(m_DefaultStyleNamespace);
                m_StyleNamespaceDirty = false;
                return;
            }
            for (int r = 0; r < m_RowHeights.Length; r++)
            for (int c = 0; c < m_ColWidths.Length; c++)
            {
                if (m_TableData.IsCellMerged(r, c)) continue;
                m_ActiveStyleNamespaces.Add(ResolveCellNamespace(m_TableData.GetCell(r, c)));
            }
            if (m_ActiveStyleNamespaces.Count == 0)
                m_ActiveStyleNamespaces.Add(m_DefaultStyleNamespace);
            m_StyleNamespaceDirty = false;
        }

        /// <summary>只在多样式背景分区需要时，从当前数据构建四叉树。</summary>
        private void EnsureQuadtreeBuilt()
        {
            if (!HasMultipleStyleNamespaces())
            {
                m_Quadtree?.Clear();
                m_QuadtreeDirty = false;
                return;
            }
            if (!m_QuadtreeDirty && m_Quadtree != null && m_Quadtree.IsBuilt) return;
            MaintainQuadtree();
        }

        private void MaintainQuadtree()
        {
            if (m_TableData == null || m_RowHeights == null || m_ColWidths == null) return;
            if (m_Quadtree == null) m_Quadtree = new XTableQuadtree();

            var cells = new List<XTableQuadtreeCell>();
            for (int r = 0; r < m_RowHeights.Length; r++)
            for (int c = 0; c < m_ColWidths.Length; c++)
            {
                if (m_TableData.IsCellMerged(r, c)) continue;
                var data = m_TableData.GetCell(r, c);
                cells.Add(new XTableQuadtreeCell { Row = r, Col = c, Namespace = ResolveCellNamespace(data) });
            }

            m_Quadtree.Build(m_TotalWidth, m_TotalHeight, m_RowHeights, m_ColWidths, cells, Config.QuadtreeMaxCellsPerNode);
            m_QuadtreeDirty = false;
        }

        #endregion
    }
}
