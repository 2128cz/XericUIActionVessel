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
            SelectionHandle.Select(row, col);
            OnCellSelected?.Invoke(row, col);
        }

        internal void UpdateSelectionDrag(RectTransform source, Vector2 screenPoint, Camera eventCamera)
        {
            if (!m_SelectionDragActive) return;
            m_LastSelectionPointer = screenPoint;
            m_SelectionPointerCamera = eventCamera;
            if (TryGetCellFromScreenPoint(source, screenPoint, eventCamera, true, out int row, out int col))
                SelectionHandle.ExtendTo(row, col);
        }

        internal void EndSelection()
        {
            m_SelectionDragActive = false;
            m_SelectionPointerCamera = null;
        }

        internal bool TryGetCellFromScreenPoint(RectTransform source, Vector2 screenPoint, Camera eventCamera,
            bool clamp, out int row, out int col)
        {
            row = -1;
            col = -1;
            if (source == null || ContentTransform is not RectTransform contentRt) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(source, screenPoint, eventCamera, out Vector2 sourceLocal))
                return false;
            Vector3 world = source.TransformPoint(sourceLocal);
            Vector3 contentLocal = contentRt.InverseTransformPoint(world);
            Vector2 point = new(
                contentLocal.x - contentRt.rect.xMin - m_ContentOffset,
                contentRt.rect.yMax - contentLocal.y - m_ContentOffset);
            return TryGetCellFromTablePoint(point, clamp, out row, out col);
        }

        internal bool TryGetCellFromTablePoint(Vector2 point, bool clamp, out int row, out int col)
        {
            row = -1;
            col = -1;
            if (m_RowHeights == null || m_ColWidths == null || m_RowHeights.Length == 0 || m_ColWidths.Length == 0)
                return false;
            if (clamp)
            {
                point.x = Mathf.Clamp(point.x, 0f, Mathf.Max(0f, m_TotalWidth - .001f));
                point.y = Mathf.Clamp(point.y, 0f, Mathf.Max(0f, m_TotalHeight - .001f));
            }
            else if (point.x < 0f || point.x >= m_TotalWidth || point.y < 0f || point.y >= m_TotalHeight)
                return false;

            float cursor = 0f;
            for (col = 0; col < m_ColWidths.Length; col++)
            {
                cursor += m_ColWidths[col];
                if (point.x < cursor) break;
            }
            cursor = 0f;
            for (row = 0; row < m_RowHeights.Length; row++)
            {
                cursor += m_RowHeights[row];
                if (point.y < cursor) break;
            }
            if (row >= m_RowHeights.Length || col >= m_ColWidths.Length) return false;
            NormalizeMergeSource(ref row, ref col);
            return true;
        }

        internal Rect GetTableRect(int rowStart, int rowEnd, int colStart, int colEnd)
        {
            return new Rect(GetColLeftX(colStart), GetRowTopY(rowStart),
                GetColLeftX(colEnd) - GetColLeftX(colStart), GetRowTopY(rowEnd) - GetRowTopY(rowStart));
        }

        private void NormalizeMergeSource(ref int row, ref int col)
        {
            if (m_TableData == null || !m_TableData.IsCellMerged(row, col)) return;
            for (int r = 0; r <= row; r++)
            for (int c = 0; c <= col; c++)
                if (m_TableData.IsMergeSource(r, c, out int rowSpan, out int colSpan)
                    && row < r + rowSpan && col < c + colSpan)
                {
                    row = r;
                    col = c;
                    return;
                }
        }

        private void UpdateSelectionAutoScroll()
        {
            if (!m_SelectionDragActive || !m_EnableSelectionAutoScroll || m_ScrollRect?.viewport == null || m_ScrollRect.content == null)
                return;
            RectTransform viewport = m_ScrollRect.viewport;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, m_LastSelectionPointer, m_SelectionPointerCamera, out Vector2 local))
                return;
            Rect rect = viewport.rect;
            if (!rect.Contains(local)) return;
            float x = local.x - rect.xMin;
            float y = rect.yMax - local.y;
            float freezeWidth = GetColLeftX(Mathf.Clamp(m_FreezeColCount, 0, m_ColWidths.Length));
            float freezeHeight = GetRowTopY(Mathf.Clamp(m_FreezeRowCount, 0, m_RowHeights.Length));
            if (x < freezeWidth || y < freezeHeight) return;

            float width = rect.width - freezeWidth;
            float height = rect.height - freezeHeight;
            float px = x - freezeWidth;
            float py = y - freezeHeight;
            float threshold = Mathf.Min(m_SelectionAutoScrollEdgeThreshold, Mathf.Min(width, height) * .5f);
            float sx = GetAutoScrollSpeed(px, width, threshold);
            float sy = GetAutoScrollSpeed(py, height, threshold);
            if (Mathf.Approximately(sx, 0f) && Mathf.Approximately(sy, 0f)) return;

            var content = m_ScrollRect.content;
            Vector2 position = content.anchoredPosition;
            position.x = Mathf.Clamp(position.x + sx * Time.unscaledDeltaTime, 0f, Mathf.Max(0f, m_TotalWidth - width));
            position.y = Mathf.Clamp(position.y + sy * Time.unscaledDeltaTime, 0f, Mathf.Max(0f, m_TotalHeight - height));
            content.anchoredPosition = position;
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
