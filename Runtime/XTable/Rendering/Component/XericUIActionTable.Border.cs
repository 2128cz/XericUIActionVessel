using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XericUI.Core.Base;
using XericUI.XTable.Core;
using XericUI.XTable.Mapping;
using XericUI.XTable.Rendering.Elements;
using XericLibrary.Runtime.SuperStyleSheet;
using XericLibrary.Runtime.UIGraph;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace XericUI.XTable.Rendering.Component
{
    public partial class XericUIActionTable
    {
        /// <summary>
        /// 获取或创建边框渲染器 GameObject 和 <see cref="UILineRendererV2"/> 组件。
        /// </summary>
        private UILineRendererV2 GetOrCreateBorderRenderer()
        {
            if (m_BorderRenderer != null)
                return m_BorderRenderer;

            Transform contentT = ContentTransform;
            // 查找已有对象
            var existing = contentT.Find("_TableBorders");
            if (existing != null)
            {
                m_BorderRenderer = existing.GetComponent<UILineRendererV2>();
                if (m_BorderRenderer != null) return m_BorderRenderer;
            }

            var go = new GameObject("_TableBorders",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(UILineRendererV2));
            go.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
            go.transform.SetParent(contentT, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            float pad = m_ContentOffset * 2f;
            rt.anchoredPosition = new Vector2(m_ContentOffset, -m_ContentOffset);
            rt.sizeDelta = new Vector2(m_TotalWidth + pad, m_TotalHeight + pad);

            m_BorderRenderer = go.GetComponent<UILineRendererV2>();
            return m_BorderRenderer;
        }

        /// <summary>销毁边框渲染器</summary>
        private void DestroyBorderRenderer()
        {
            if (m_BorderRenderer != null)
            {
                if (Application.isPlaying)
                    Destroy(m_BorderRenderer.gameObject);
                else
                    DestroyImmediate(m_BorderRenderer.gameObject);
                m_BorderRenderer = null;
            }
        }

        /// <summary>
        /// 重建表格的全部边框线段。
        /// 第一阶段：从 OuterBorderNs 绘制 -1 行（顶部外框）和 -1 列（左侧外框）。
        /// 第二阶段：遍历非合并覆盖单元格，从边框缓存读取样式命名空间，按 per-cell 样式绘制右侧和底部边框。
        /// 合并源在最外沿绘制，内部边框自动跳过。
        /// </summary>
        private void RebuildBorders()
        {
            if (m_RowHeights == null || m_ColWidths == null) return;
            if (m_RowHeights.Length == 0 || m_ColWidths.Length == 0) return;

            var renderer = GetOrCreateBorderRenderer();
            renderer.ClearAll();

            var borderRt = renderer.GetComponent<RectTransform>();
            borderRt.sizeDelta = new Vector2(m_TotalWidth, m_TotalHeight);

#if UNITY_EDITOR
            Color borderColor;
            float borderWidth;
            if (!Application.isPlaying)
            {
                borderColor = GetCellBorderColor(m_DefaultStyleNamespace);
                borderWidth = GetCellBorderWidth(m_DefaultStyleNamespace);
            }
            else
            {
                borderColor = Config.RuntimeBorderColor;
                borderWidth = Config.RuntimeBorderWidth;
            }
#else
            Color borderColor = Config.RuntimeBorderColor;
            float borderWidth = Config.RuntimeBorderWidth;
#endif

            int rows = m_RowHeights.Length;
            int cols = m_ColWidths.Length;

            // ════════════════════════════════════════════
            // 阶段 1：-1 行（顶部外框）和 -1 列（左侧外框）
            // ════════════════════════════════════════════

            // 顶部外框：从 col=0 到 col=cols-1，每列一段
            float topY = 0;
            for (int c = 0; c < cols; c++)
            {
                string ns = m_TableData?.GetOuterBorderNs(-1, c);
                if (string.IsNullOrEmpty(ns)) ns = m_DefaultStyleNamespace;
                Color color = GetCellBorderColor(ns);
                float width = GetCellBorderWidth(ns);
                float leftX = GetColLeftX(c);
                renderer.DrawLine(
                    new Vector2(leftX, topY),
                    new Vector2(leftX + m_ColWidths[c], topY),
                    color, width, UVMode.ByDistance);
            }

            // 左侧外框：从 row=0 到 row=rows-1，每行一段
            float xEdge = 0;
            for (int r = 0; r < rows; r++)
            {
                string ns = m_TableData?.GetOuterBorderNs(r, -1);
                if (string.IsNullOrEmpty(ns)) ns = m_DefaultStyleNamespace;
                Color color = GetCellBorderColor(ns);
                float width = GetCellBorderWidth(ns);
                float rowTopY = -GetRowTopY(r);
                renderer.DrawLine(
                    new Vector2(xEdge, rowTopY),
                    new Vector2(xEdge, rowTopY - m_RowHeights[r]),
                    color, width, UVMode.ByDistance);
            }

            // ════════════════════════════════════════════
            // 阶段 2：每个单元格的右侧和底部边框
            // ════════════════════════════════════════════

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    // 跳过被合并覆盖的单元格
                    if (m_TableData != null && m_TableData.IsCellMerged(r, c))
                        continue;

                    float cellX = GetColLeftX(c);
                    float cellY = -GetRowTopY(r);
                    float cellW = m_ColWidths[c];
                    float cellH = m_RowHeights[r];

                    int mergeRS = 1, mergeCS = 1;
                    if (m_TableData != null)
                        m_TableData.IsMergeSource(r, c, out mergeRS, out mergeCS);

                    // 扩展合并尺寸
                    for (int i = 1; i < mergeCS && c + i < cols; i++)
                        cellW += m_ColWidths[c + i];
                    for (int i = 1; i < mergeRS && r + i < rows; i++)
                        cellH += m_RowHeights[r + i];

                    float rightX = cellX + cellW;
                    float bottomY = cellY - cellH;

                    // —— 右侧边框 ——
                    string rightNs = m_TableData?.GetBorderRightNs(r, c);
                    if (string.IsNullOrEmpty(rightNs)) rightNs = m_DefaultStyleNamespace;
                    Color rightColor = GetCellBorderColor(rightNs);
                    float rightWidth = GetCellBorderWidth(rightNs);

                    int rightCol = c + mergeCS;
                    if (rightCol <= cols)
                    {
                        renderer.DrawLine(
                            new Vector2(rightX, cellY),
                            new Vector2(rightX, bottomY),
                            rightColor, rightWidth, UVMode.ByDistance);
                    }

                    // —— 底部边框 ——
                    string bottomNs = m_TableData?.GetBorderBottomNs(r, c);
                    if (string.IsNullOrEmpty(bottomNs)) bottomNs = m_DefaultStyleNamespace;
                    Color bottomColor = GetCellBorderColor(bottomNs);
                    float bottomWidth = GetCellBorderWidth(bottomNs);

                    int bottomRow = r + mergeRS;
                    if (bottomRow <= rows)
                    {
                        renderer.DrawLine(
                            new Vector2(cellX, bottomY),
                            new Vector2(rightX, bottomY),
                            bottomColor, bottomWidth, UVMode.ByDistance);
                    }
                }
            }
        }
    }
}
