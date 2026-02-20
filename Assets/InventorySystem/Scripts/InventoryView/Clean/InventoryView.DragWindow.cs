using UnityEngine;
using UnityEngine.EventSystems;

namespace Inventories
{
    public partial class InventoryView
    {
        private Vector2 _windowOffset;
        private bool _isDraggingWindow;

        private void BeginWindowDrag(PointerEventData eventData)
        {
            _isDraggingWindow = true;
            transform.SetAsLastSibling();

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                transform as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out _windowOffset
            );
        }

        private void DragWindow(PointerEventData eventData)
        {
            var canvas = GetComponentInParent<Canvas>();
            var rectTransform = transform as RectTransform;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvas.transform as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 mousePos))
            {
                rectTransform.anchoredPosition = mousePos - _windowOffset;
            }
        }
    }
}