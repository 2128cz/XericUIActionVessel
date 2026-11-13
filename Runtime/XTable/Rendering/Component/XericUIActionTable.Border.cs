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
            {
                m_BorderRenderer.raycastTarget = false;
                return m_BorderRenderer;
            }

            Transform contentT = ContentTransform;
            // 查找已有对象
            var existing = contentT.Find("_TableBorders");
            if (existing != null)
            {
                m_BorderRenderer = existing.GetComponent<UILineRendererV2>();
                if (m_BorderRenderer != null)
                {
                    // 顺带自愈旧对象的标记：运行期渲染物一律不可编辑，编辑期改动才不会被记成预制体覆盖。
                    existing.gameObject.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy | HideFlags.NotEditable;
                    return m_BorderRenderer;
                }
            }

            var go = new GameObject("_TableBorders",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(UILineRendererV2));
            go.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy | HideFlags.NotEditable;
            go.transform.SetParent(contentT, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            float pad = m_ContentOffset * 2f;
            rt.anchoredPosition = new Vector2(m_ContentOffset, -m_ContentOffset);
            rt.sizeDelta = new Vector2(m_TotalWidth + pad, m_TotalHeight + pad);

            m_BorderRenderer = go.GetComponent<UILineRendererV2>();
            m_BorderRenderer.raycastTarget = false;
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

        /// <summary>按边界范围提交表格边框；默认样式连续边会合并为单条线段。</summary>
        private void RebuildBorders()
        {
            if (m_RowHeights == null || m_ColWidths == null || m_RowHeights.Length == 0 || m_ColWidths.Length == 0)
                return;

            var renderer = GetOrCreateBorderRenderer();
            renderer.ClearAll();
            renderer.rectTransform.sizeDelta = new Vector2(m_TotalWidth, m_TotalHeight);

            int rows = m_RowHeights.Length;
            int cols = m_ColWidths.Length;
            int freezeRows = Mathf.Clamp(m_FreezeRowCount, 0, rows);
            int freezeCols = Mathf.Clamp(m_FreezeColCount, 0, cols);
            SubmitBorders(renderer, freezeRows, rows, freezeCols, cols);
            RebuildFrozenBorders(freezeRows, freezeCols, rows, cols);
        }

        private void SubmitBorders(UILineRendererV2 renderer, int rowStart, int rowEnd, int colStart, int colEnd)
        {
            if (renderer == null || rowStart >= rowEnd || colStart >= colEnd) return;
            renderer.ClearAll();
            renderer.rectTransform.sizeDelta = new Vector2(m_TotalWidth, m_TotalHeight);
            for (int boundary = rowStart; boundary <= rowEnd; boundary++)
                SubmitHorizontalBoundary(renderer, boundary, colStart, colEnd);
            for (int boundary = colStart; boundary <= colEnd; boundary++)
                SubmitVerticalBoundary(renderer, boundary, rowStart, rowEnd);
        }

        private void SubmitHorizontalBoundary(UILineRendererV2 renderer, int boundary, int colStart, int colEnd)
        {
            int start = colStart;
            while (start < colEnd)
            {
                if (IsMergedInteriorBoundary(XTableBorderAxis.Horizontal, boundary, start))
                {
                    start++;
                    continue;
                }
                string ns = ResolveBorderNamespace(XTableBorderAxis.Horizontal, boundary, start);
                int end = start + 1;
                while (end < colEnd && !IsMergedInteriorBoundary(XTableBorderAxis.Horizontal, boundary, end)
                    && ResolveBorderNamespace(XTableBorderAxis.Horizontal, boundary, end) == ns)
                    end++;

                float y = -GetRowTopY(boundary);
                renderer.DrawLine(new Vector2(GetColLeftX(start), y), new Vector2(GetColLeftX(end), y),
                    GetCellBorderColor(ns), GetCellBorderWidth(ns), UVMode.ByDistance);
                start = end;
            }
        }

        private void SubmitVerticalBoundary(UILineRendererV2 renderer, int boundary, int rowStart, int rowEnd)
        {
            int start = rowStart;
            while (start < rowEnd)
            {
                if (IsMergedInteriorBoundary(XTableBorderAxis.Vertical, boundary, start))
                {
                    start++;
                    continue;
                }
                string ns = ResolveBorderNamespace(XTableBorderAxis.Vertical, boundary, start);
                int end = start + 1;
                while (end < rowEnd && !IsMergedInteriorBoundary(XTableBorderAxis.Vertical, boundary, end)
                    && ResolveBorderNamespace(XTableBorderAxis.Vertical, boundary, end) == ns)
                    end++;

                float x = GetColLeftX(boundary);
                renderer.DrawLine(new Vector2(x, -GetRowTopY(start)), new Vector2(x, -GetRowTopY(end)),
                    GetCellBorderColor(ns), GetCellBorderWidth(ns), UVMode.ByDistance);
                start = end;
            }
        }

        private void RebuildFrozenBorders(int freezeRows, int freezeCols, int rows, int cols)
        {
            if (freezeRows == 0 && freezeCols == 0)
            {
                DestroyFrozenBorderRenderers();
                return;
            }
            if (freezeCols > 0 && freezeRows < rows)
                SubmitBorders(GetOrCreateFrozenBorderRenderer(m_FrozenColumnsContainer, ref m_FrozenColumnBorderRenderer, "_FrozenColumnBorders"),
                    freezeRows, rows, 0, freezeCols);
            if (freezeRows > 0 && freezeCols < cols)
                SubmitBorders(GetOrCreateFrozenBorderRenderer(m_FrozenRowsContainer, ref m_FrozenRowBorderRenderer, "_FrozenRowBorders"),
                    0, freezeRows, freezeCols, cols);
            if (freezeRows > 0 && freezeCols > 0)
                SubmitBorders(GetOrCreateFrozenBorderRenderer(m_FrozenCornerContainer, ref m_FrozenCornerBorderRenderer, "_FrozenCornerBorders"),
                    0, freezeRows, 0, freezeCols);
        }

        private static UILineRendererV2 GetOrCreateFrozenBorderRenderer(Transform parent,
            ref UILineRendererV2 renderer, string name)
        {
            if (renderer != null)
            {
                renderer.raycastTarget = false;
                return renderer;
            }
            if (parent == null) return null;
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UILineRendererV2));
            go.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            renderer = go.GetComponent<UILineRendererV2>();
            renderer.raycastTarget = false;
            return renderer;
        }

        private void DestroyFrozenBorderRenderers()
        {
            DestroyFrozenBorderRenderer(ref m_FrozenColumnBorderRenderer);
            DestroyFrozenBorderRenderer(ref m_FrozenRowBorderRenderer);
            DestroyFrozenBorderRenderer(ref m_FrozenCornerBorderRenderer);
        }

        private static void DestroyFrozenBorderRenderer(ref UILineRendererV2 renderer)
        {
            if (renderer == null) return;
            if (Application.isPlaying) Destroy(renderer.gameObject);
            else DestroyImmediate(renderer.gameObject);
            renderer = null;
        }

        private string ResolveBorderNamespace(XTableBorderAxis axis, int fixedIndex, int index)
        {
            string ns = m_TableData?.GetBorderNamespace(axis, fixedIndex, index);
            return string.IsNullOrEmpty(ns) ? m_DefaultStyleNamespace : ns;
        }

        private bool IsMergedInteriorBoundary(XTableBorderAxis axis, int fixedIndex, int index)
        {
            if (m_TableData == null) return false;
            int rowA, colA, rowB, colB;
            if (axis == XTableBorderAxis.Horizontal)
            {
                if (fixedIndex <= 0 || fixedIndex >= m_RowHeights.Length) return false;
                rowA = fixedIndex - 1; colA = index;
                rowB = fixedIndex; colB = index;
            }
            else
            {
                if (fixedIndex <= 0 || fixedIndex >= m_ColWidths.Length) return false;
                rowA = index; colA = fixedIndex - 1;
                rowB = index; colB = fixedIndex;
            }
            return m_TableData.TryGetMergeRect(rowA, colA, out int sourceRowA, out int sourceColA, out _, out _)
                && m_TableData.TryGetMergeRect(rowB, colB, out int sourceRowB, out int sourceColB, out _, out _)
                && sourceRowA == sourceRowB && sourceColA == sourceColB;
        }
    }
}
