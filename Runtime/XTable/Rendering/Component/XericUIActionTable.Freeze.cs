using System;
using UnityEngine;
using UnityEngine.UI;
using XericUI.XTable.Core;
using XericUI.XTable.Rendering.Elements;
using XericLibrary.Runtime.SuperStyleSheet;

namespace XericUI.XTable.Rendering.Component
{
    public partial class XericUIActionTable
    {
        #region Viewport 冻结渲染

        /// <summary>创建或重新创建 Viewport 层冻结渲染器</summary>
        private void CreateViewportRenderer()
        {
            if (m_ScrollRect == null || m_ScrollRect.viewport == null) return;
            if (m_FreezeRowCount <= 0 && m_FreezeColCount <= 0) return;

            // 清理旧渲染器
            m_ViewportRenderer?.DestroyAll();

            Transform viewportT = m_ScrollRect.viewport;
            m_ViewportRenderer = new TableRenderer(viewportT, m_DefaultStyleNamespace, OnPoolStyleChanged);

            // 配置委托
            m_ViewportRenderer.GetStyleColor = (ns, path, fb) => GetStyleColor(ns, path, fb);
            m_ViewportRenderer.GetStyleFloat = (ns, path, fb) => GetStyleFloat(ns, path, fb);
            m_ViewportRenderer.GetStyleInt = (ns, path, fb) => GetStyleInt(ns, path, fb);
            m_ViewportRenderer.GetStyleFontAsset = (ns, path) => GetStyleFontAsset(ns, path);
            m_ViewportRenderer.GetRowHeights = () => m_RowHeights;
            m_ViewportRenderer.GetColWidths = () => m_ColWidths;
            m_ViewportRenderer.GetTableData = () => m_TableData;
            m_ViewportRenderer.GetDefaultStyleNamespace = () => m_DefaultStyleNamespace;
            m_ViewportRenderer.IsInSelectionRange = (r, c) => IsInSelectionRange(r, c);
            m_ViewportRenderer.GetSelectedRow = () => SelectionHandle.StartRow;
            m_ViewportRenderer.GetSelectedCol = () => SelectionHandle.StartCol;

            // 仅渲染冻结范围
            m_ViewportRenderer.StartRow = 0;
            m_ViewportRenderer.EndRow = m_FreezeRowCount;
            m_ViewportRenderer.StartCol = 0;
            m_ViewportRenderer.EndCol = m_FreezeColCount;

            // 共享预制体对象池
            m_ViewportRenderer.PrefabPool = m_PrefabPool;
            m_ViewportRenderer.ActivePrefabMap = m_ActivePrefabMap;
            m_ViewportRenderer.CellPadding = Config.CellHorizontalPadding;
            m_ViewportRenderer.Config = Config;
        }

        /// <summary>渲染 Viewport 层冻结单元格，随滚动偏移调整位置</summary>
        private void RenderFrozenCells()
        {
            if (m_FreezeRowCount <= 0 && m_FreezeColCount <= 0)
            {
                // 无冻结需求时清理渲染器
                if (m_ViewportRenderer != null)
                {
                    m_ViewportRenderer.DestroyAll();
                    m_ViewportRenderer = null;
                }
                return;
            }

            // 检测冻结范围变更，重建渲染器
            if (m_ViewportRenderer == null ||
                m_ViewportRenderer.EndRow != m_FreezeRowCount ||
                m_ViewportRenderer.EndCol != m_FreezeColCount)
                CreateViewportRenderer();

            // 计算冻结区域尺寸
            float freezeW = 0, freezeH = 0;
            for (int c = 0; c < m_FreezeColCount && c < m_ColWidths.Length; c++)
                freezeW += m_ColWidths[c];
            for (int r = 0; r < m_FreezeRowCount && r < m_RowHeights.Length; r++)
                freezeH += m_RowHeights[r];

            // 创建/更新背景
            m_ViewportRenderer.CreateBackground(freezeW, freezeH, m_DefaultStyleNamespace);

            // 获取滚动偏移
            Vector2 scrollOffset = Vector2.zero;
            if (m_ScrollRect != null && m_ScrollRect.content != null)
            {
                scrollOffset.x = Mathf.Max(0, -m_ScrollRect.content.anchoredPosition.x);
                scrollOffset.y = Mathf.Max(0, -m_ScrollRect.content.anchoredPosition.y);
            }

            // 调整背景位置（随滚动偏移移动，保持在视口可见范围内）
            if (m_ViewportRenderer.BackgroundGo != null)
            {
                var bgRt = m_ViewportRenderer.BackgroundGo.GetComponent<RectTransform>();
                bgRt.anchoredPosition = new Vector2(scrollOffset.x, -scrollOffset.y);
            }

            // 回收并重新渲染冻结单元格
            m_ViewportRenderer.ReturnAll();
            m_ViewportRenderer.RenderCells(
                0, m_FreezeRowCount,
                0, m_FreezeColCount,
                !Application.isPlaying);

            // 冻结单元格需要跟随滚动偏移（相对于 Viewport 定位）
            if (m_ViewportRenderer.Assembler != null)
            {
                // 调整所有活跃的池对象的 anchoredPosition 以抵消滚动
                // 这通过重新定位背景实现，单元格相对于背景 (0,0) 定位，所以随背景一起移动
            }

            // 背景始终可见
            m_ViewportRenderer.BackgroundGo?.SetActive(true);
        }

        #endregion
    }
}
