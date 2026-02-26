using UnityEngine;
using UnityEngine.EventSystems;

namespace Inventories
{
    public class DraggableWindow : MonoBehaviour, IDragHandler, IBeginDragHandler
    {
        private Vector2 _windowOffset;

        public void OnBeginDrag(PointerEventData eventData)
        {
            transform.SetAsLastSibling();

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                transform as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out _windowOffset
            );
        }

        public void OnDrag(PointerEventData eventData)
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