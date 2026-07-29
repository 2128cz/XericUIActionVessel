using UnityEngine;

namespace XericUI.XTable.Rendering.Component
{
    public partial class XericUIActionTable
    {
        internal float TotalWidth => m_TotalWidth;
        internal float TotalHeight => m_TotalHeight;
        internal Color SelectionOverlayColor => Application.isPlaying ? Config.RuntimeSelectionColor : GetCellSelectionBgColor(m_DefaultStyleNamespace);

        internal void BeginSelection(int row, int col)
        {
            m_SelectionDragActive = true;
            SetNormalizedSelection(row, col, row, col);
            OnCellSelected?.Invoke(row, col);
        }

        internal void UpdateSelectionDrag(RectTransform source, Vector2 screenPoint, Camera eventCamera)
        {
            if (!m_SelectionDragActive) return;
            m_LastSelectionPointer = screenPoint;
            m_SelectionPointerCamera = eventCamera;
            if (TryGetCellFromScreenPoint(source, screenPoint, eventCamera, true, out int row, out int col))
                UpdateSelectionDragToCell(row, col);
        }

        internal void UpdateSelectionDragToCell(int row, int col)
        {
            if (!m_SelectionDragActive) return;
            SetNormalizedSelection(SelectionHandle.StartRow, SelectionHandle.StartCol, row, col);
        }

        internal void NormalizeAndSelect(int startRow, int startCol, int endRow, int endCol) =>
            SetNormalizedSelection(startRow, startCol, endRow, endCol);

        private void SetNormalizedSelection(int startRow, int startCol, int endRow, int endCol)
        {
            int minRow = Mathf.Min(startRow, endRow), maxRow = Mathf.Max(startRow, endRow);
            int minCol = Mathf.Min(startCol, endCol), maxCol = Mathf.Max(startCol, endCol);
            bool changed;
            do
            {
                changed = false;
                for (int r = minRow; r <= maxRow; r++)
                for (int c = minCol; c <= maxCol; c++)
                    if (m_TableData != null && m_TableData.TryGetMergeRect(r, c, out int sr, out int sc, out int rs, out int cs))
                    {
                        int er = sr + rs - 1, ec = sc + cs - 1;
                        int nr = Mathf.Min(minRow, sr), nxr = Mathf.Max(maxRow, er);
                        int nc = Mathf.Min(minCol, sc), nxc = Mathf.Max(maxCol, ec);
                        changed |= nr != minRow || nxr != maxRow || nc != minCol || nxc != maxCol;
                        minRow = nr; maxRow = nxr; minCol = nc; maxCol = nxc;
                    }
            } while (changed);
            int normalizedEndRow = endRow >= startRow ? maxRow : minRow;
            int normalizedEndCol = endCol >= startCol ? maxCol : minCol;
            SelectionHandle.SetEndpoints(startRow, startCol, normalizedEndRow, normalizedEndCol);
        }

        internal void EndSelection() { m_SelectionDragActive = false; m_SelectionPointerCamera = null; }

        internal bool TryGetCellFromScreenPoint(RectTransform source, Vector2 screenPoint, Camera eventCamera, bool clamp, out int row, out int col)
        {
            row = col = -1;
            if (source == null || ContentTransform is not RectTransform contentRt || !RectTransformUtility.ScreenPointToLocalPointInRectangle(source, screenPoint, eventCamera, out Vector2 sourceLocal)) return false;
            Vector3 contentLocal = contentRt.InverseTransformPoint(source.TransformPoint(sourceLocal));
            return TryGetCellFromTablePoint(new Vector2(contentLocal.x - contentRt.rect.xMin - m_ContentOffset, contentRt.rect.yMax - contentLocal.y - m_ContentOffset), clamp, out row, out col);
        }

        internal bool TryGetCellFromTablePoint(Vector2 point, bool clamp, out int row, out int col)
        {
            row = col = -1;
            if (m_RowHeights == null || m_ColWidths == null || m_RowHeights.Length == 0 || m_ColWidths.Length == 0) return false;
            if (clamp) { point.x = Mathf.Clamp(point.x, 0, Mathf.Max(0, m_TotalWidth - .001f)); point.y = Mathf.Clamp(point.y, 0, Mathf.Max(0, m_TotalHeight - .001f)); }
            else if (point.x < 0 || point.y < 0 || point.x >= m_TotalWidth || point.y >= m_TotalHeight) return false;
            float cursor = 0; for (col = 0; col < m_ColWidths.Length && point.x >= (cursor += m_ColWidths[col]); col++) { }
            cursor = 0; for (row = 0; row < m_RowHeights.Length && point.y >= (cursor += m_RowHeights[row]); row++) { }
            if (row >= m_RowHeights.Length || col >= m_ColWidths.Length) return false;
            if (m_TableData != null && m_TableData.TryGetMergeRect(row, col, out int sr, out int sc, out _, out _)) { row = sr; col = sc; }
            return true;
        }

        internal Rect GetTableRect(int rowStart, int rowEnd, int colStart, int colEnd) => new(GetColLeftX(colStart), GetRowTopY(rowStart), GetColLeftX(colEnd) - GetColLeftX(colStart), GetRowTopY(rowEnd) - GetRowTopY(rowStart));

        private void UpdateSelectionAutoScroll()
        {
            if (!m_SelectionDragActive || !m_EnableSelectionAutoScroll || m_ScrollRect?.viewport == null || m_ScrollRect.content == null) return;
            RectTransform viewport = m_ScrollRect.viewport;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, m_LastSelectionPointer, m_SelectionPointerCamera, out Vector2 local)) return;
            Rect rect = viewport.rect;
            if (!rect.Contains(local)) return;
            float x = local.x - rect.xMin, y = rect.yMax - local.y;
            float freezeWidth = GetColLeftX(Mathf.Clamp(m_FreezeColCount, 0, m_ColWidths.Length));
            float freezeHeight = GetRowTopY(Mathf.Clamp(m_FreezeRowCount, 0, m_RowHeights.Length));
            if (x < freezeWidth || y < freezeHeight) return;
            float width = rect.width - freezeWidth, height = rect.height - freezeHeight;
            float threshold = Mathf.Min(m_SelectionAutoScrollEdgeThreshold, Mathf.Min(width, height) * .5f);
            float sx = GetAutoScrollSpeed(x - freezeWidth, width, threshold), sy = GetAutoScrollSpeed(y - freezeHeight, height, threshold);
            if (Mathf.Approximately(sx, 0) && Mathf.Approximately(sy, 0)) return;
            Vector2 position = m_ScrollRect.content.anchoredPosition;
            position.x = Mathf.Clamp(position.x + sx * Time.unscaledDeltaTime, 0, Mathf.Max(0, m_TotalWidth - width));
            position.y = Mathf.Clamp(position.y + sy * Time.unscaledDeltaTime, 0, Mathf.Max(0, m_TotalHeight - height));
            m_ScrollRect.content.anchoredPosition = position;
            UpdateSelectionDrag(viewport, m_LastSelectionPointer, m_SelectionPointerCamera);
        }

        private float GetAutoScrollSpeed(float point, float length, float threshold)
        {
            float direction = point < threshold ? -1f : point > length - threshold ? 1f : 0f;
            if (direction == 0f) return 0f;
            float distance = direction < 0f ? point : length - point;
            float strength = 1f - Mathf.Clamp01(distance / threshold);
            return direction * m_SelectionAutoScrollMaxSpeed * Mathf.Pow(strength, m_SelectionAutoScrollAcceleration);
        }
    }
}
