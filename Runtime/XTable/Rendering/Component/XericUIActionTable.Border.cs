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
            for (int boundary = 0; boundary <= rows; boundary++)
                SubmitHorizontalBoundary(renderer, boundary, cols);
            for (int boundary = 0; boundary <= cols; boundary++)
                SubmitVerticalBoundary(renderer, boundary, rows);
        }

        private void SubmitHorizontalBoundary(UILineRendererV2 renderer, int boundary, int cols)
        {
            int start = 0;
            while (start < cols)
            {
                if (IsMergedInteriorBoundary(XTableBorderAxis.Horizontal, boundary, start))
                {
                    start++;
                    continue;
                }
                string ns = ResolveBorderNamespace(XTableBorderAxis.Horizontal, boundary, start);
                int end = start + 1;
                while (end < cols && !IsMergedInteriorBoundary(XTableBorderAxis.Horizontal, boundary, end)
                    && ResolveBorderNamespace(XTableBorderAxis.Horizontal, boundary, end) == ns)
                    end++;

                float y = -GetRowTopY(boundary);
                renderer.DrawLine(new Vector2(GetColLeftX(start), y), new Vector2(GetColLeftX(end), y),
                    GetCellBorderColor(ns), GetCellBorderWidth(ns), UVMode.ByDistance);
                start = end;
            }
        }

        private void SubmitVerticalBoundary(UILineRendererV2 renderer, int boundary, int rows)
        {
            int start = 0;
            while (start < rows)
            {
                if (IsMergedInteriorBoundary(XTableBorderAxis.Vertical, boundary, start))
                {
                    start++;
                    continue;
                }
                string ns = ResolveBorderNamespace(XTableBorderAxis.Vertical, boundary, start);
                int end = start + 1;
                while (end < rows && !IsMergedInteriorBoundary(XTableBorderAxis.Vertical, boundary, end)
                    && ResolveBorderNamespace(XTableBorderAxis.Vertical, boundary, end) == ns)
                    end++;

                float x = GetColLeftX(boundary);
                renderer.DrawLine(new Vector2(x, -GetRowTopY(start)), new Vector2(x, -GetRowTopY(end)),
                    GetCellBorderColor(ns), GetCellBorderWidth(ns), UVMode.ByDistance);
                start = end;
            }
        }

        private string ResolveBorderNamespace(XTableBorderAxis axis, int fixedIndex, int index)
        {
            string ns = m_TableData?.GetBorderNamespace(axis, fixedIndex, index);
            return string.IsNullOrEmpty(ns) ? m_DefaultStyleNamespace : ns;
        }

        private bool IsMergedInteriorBoundary(XTableBorderAxis axis, int fixedIndex, int index)
        {
            if (m_TableData == null) return false;
            if (axis == XTableBorderAxis.Horizontal)
            {
                if (fixedIndex <= 0 || fixedIndex >= m_RowHeights.Length) return false;
                return m_TableData.IsCellMerged(fixedIndex - 1, index)
                    || m_TableData.IsCellMerged(fixedIndex, index);
            }
            if (fixedIndex <= 0 || fixedIndex >= m_ColWidths.Length) return false;
            return m_TableData.IsCellMerged(index, fixedIndex - 1)
                || m_TableData.IsCellMerged(index, fixedIndex);
        }
    }
}
