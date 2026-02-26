using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace Inventories
{
    public class ItemDragHandler : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [SerializeField] private Image _draggableImage;

        [Inject] private readonly DragContext _dragContext;
        [Inject] private readonly DragChainProcessor _itemDragProcessor;

        public void OnBeginDrag(PointerEventData eventData)
        {
            transform.SetAsLastSibling();
            _itemDragProcessor.ProcessBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_draggableImage != null && _dragContext.IsDragging)
            {
                _draggableImage.rectTransform.position = eventData.position + _dragContext.DragOffset;

                var cell = eventData.pointerCurrentRaycast.gameObject.GetComponent<CellView>();
                _dragContext.UpdateCurrentCell(cell);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _itemDragProcessor.ProcessEndDrag(eventData);
            _draggableImage.gameObject.SetActive(false);
            _dragContext.Reset();
        }

        public void SetupDraggableImage(Item item)
        {
            _draggableImage.rectTransform.sizeDelta = new Vector2(
                item.Settings.Size.x * 75,
                item.Settings.Size.y * 75);

            _draggableImage.sprite = item.Settings.Icon;
            _draggableImage.gameObject.SetActive(true);
        }
    }
}