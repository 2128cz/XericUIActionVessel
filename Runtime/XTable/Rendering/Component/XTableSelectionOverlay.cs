using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XericLibrary.Runtime.MacroLibrary;

namespace XericUI.XTable.Rendering.Component
{
    /// <summary>单个渲染层的固定框选输入与视觉覆盖层。</summary>
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public sealed class XTableSelectionOverlay : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IEndDragHandler
    {
        private XericUIActionTable m_Table;
        private Image m_Fill;
        private int m_RowStart;
        private int m_RowEnd;
        private int m_ColStart;
        private int m_ColEnd;
        private bool m_Dragging;

        public void Configure(XericUIActionTable table, int rowStart, int rowEnd, int colStart, int colEnd)
        {
            m_Table = table;
            m_RowStart = rowStart;
            m_RowEnd = rowEnd;
            m_ColStart = colStart;
            m_ColEnd = colEnd;
            Rect region = table.GetTableRect(rowStart, rowEnd, colStart, colEnd);
            var rt = transform.RectTransform();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(region.x, -region.y);
            rt.sizeDelta = region.size;
            EnsureVisuals();
            RefreshVisual();
        }

        public void RefreshVisual()
        {
            EnsureVisuals();
            if (m_Table == null || !m_Table.SelectionHandle.HasSelection || m_RowStart >= m_RowEnd || m_ColStart >= m_ColEnd)
            {
                m_Fill.gameObject.SetActive(false);
                return;
            }

            int rs = Mathf.Max(m_RowStart, m_Table.SelectionHandle.MinRow);
            int re = Mathf.Min(m_RowEnd - 1, m_Table.SelectionHandle.MaxRow);
            int cs = Mathf.Max(m_ColStart, m_Table.SelectionHandle.MinCol);
            int ce = Mathf.Min(m_ColEnd - 1, m_Table.SelectionHandle.MaxCol);
            if (rs > re || cs > ce)
            {
                m_Fill.gameObject.SetActive(false);
                return;
            }

            Rect rect = m_Table.GetTableRect(rs, re + 1, cs, ce + 1);
            Rect region = m_Table.GetTableRect(m_RowStart, m_RowEnd, m_ColStart, m_ColEnd);
            var fillRt = m_Fill.rectTransform;
            fillRt.anchoredPosition = new Vector2(rect.x - region.x, region.y - rect.y);
            fillRt.sizeDelta = rect.size;
            m_Fill.color = m_Table.SelectionOverlayColor;
            m_Fill.gameObject.SetActive(true);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (m_Table == null || !m_Table.TryGetCellFromScreenPoint(transform.RectTransform(), eventData.position, eventData.pressEventCamera, true, out int row, out int col))
                return;
            m_Dragging = true;
            m_Table.BeginSelection(row, col);
            eventData.Use();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!m_Dragging || m_Table == null) return;
            m_Table.UpdateSelectionDrag(transform.RectTransform(), eventData.position, eventData.pressEventCamera);
            eventData.Use();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            EndDrag();
            eventData.Use();
        }

        public void OnEndDrag(PointerEventData eventData) => EndDrag();

        private void OnDisable() => EndDrag();
        private void OnDestroy() => EndDrag();

        private void EndDrag()
        {
            if (!m_Dragging) return;
            m_Dragging = false;
            m_Table?.EndSelection();
        }

        private void EnsureVisuals()
        {
            var input = GetComponent<Image>();
            input.color = Color.clear;
            input.raycastTarget = true;
            if (m_Fill != null) return;
            var go = new GameObject("_SelectionRect", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            go.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
            go.transform.SetParent(transform, false);
            m_Fill = go.GetComponent<Image>();
            m_Fill.raycastTarget = false;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.55f, 1f, 1f);
            outline.effectDistance = new Vector2(1f, -1f);
            var rt = m_Fill.rectTransform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
        }
    }
}
