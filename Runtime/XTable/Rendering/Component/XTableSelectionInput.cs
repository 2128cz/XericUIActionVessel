using UnityEngine;
using UnityEngine.EventSystems;

namespace XericUI.XTable.Rendering.Component
{
    [RequireComponent(typeof(RectTransform))]
    internal sealed class XTableSelectionInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IEndDragHandler
    {
        private XericUIActionTable m_Table;
        private bool m_Dragging;

        internal void Configure(XericUIActionTable table) => m_Table = table;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (m_Table == null || !m_Table.TryGetCellFromScreenPoint((RectTransform)transform, eventData.position,
                    eventData.pressEventCamera, true, out int row, out int col)) return;
            m_Dragging = true;
            m_Table.BeginSelection(row, col);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!m_Dragging || m_Table == null) return;
            m_Table.UpdateSelectionDrag((RectTransform)transform, eventData.position, eventData.pressEventCamera);
        }

        public void OnPointerUp(PointerEventData eventData) => EndSelection();
        public void OnEndDrag(PointerEventData eventData) => EndSelection();
        private void OnDisable() => EndSelection();

        private void EndSelection()
        {
            if (!m_Dragging) return;
            m_Dragging = false;
            // 用 Unity 判空而非 ?.：表格可能已被销毁（“假 null”）。
            if (m_Table != null) m_Table.EndSelection();
        }
    }
}
