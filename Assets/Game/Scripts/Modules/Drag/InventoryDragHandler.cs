using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace Inventories
{
    public class InventoryDragHandler : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [SerializeField] private Image _draggableImage;
        [SerializeField] private Vector2Int _cellSize;

        [Inject] private readonly DragContext _dragContext;
        [Inject] private readonly DragChainProcessor _itemDragProcessor;

        private Vector2 _windowOffset;

        public void OnBeginDrag(PointerEventData eventData)
        {
            transform.SetAsLastSibling();

            bool success = _itemDragProcessor.ProcessBeginDrag(eventData);

            if (!success)
                BeginWindowDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (TryMoveItem(eventData))
                return;

            DragWindow(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _itemDragProcessor.ProcessEndDrag(eventData);
            _draggableImage.gameObject.SetActive(false);
            _dragContext.Reset();
        }

        public void SetupDraggableImage(Item item, Vector2Int cellSize)
        {
            _draggableImage.rectTransform.sizeDelta = new Vector2(
                item.Settings.Size.x * cellSize.x,
                item.Settings.Size.y * cellSize.y);

            _draggableImage.sprite = item.Settings.Icon;
            _draggableImage.gameObject.SetActive(true);
        }

        private bool TryMoveItem(PointerEventData eventData)
        {
            if (_draggableImage != null && _dragContext.IsDragging)
            {
                _draggableImage.rectTransform.position = eventData.position + _dragContext.DragOffset;

                var cell = eventData.pointerCurrentRaycast.gameObject.GetComponent<InventoryCell>();
                _dragContext.UpdateCurrentCell(cell);
                return true;
            }

            return false;
        }

        private void BeginWindowDrag(PointerEventData eventData)
        {
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