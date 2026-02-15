using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Inventories
{
    public class DraggableItem : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform _rectTransform;

        private Transform _originalParent;
        private Vector2 _originalAnchoredPosition;
        private int _originalSiblingIndex;

        [Inject] private Canvas _canvas;
        [Inject] private InventoryAdapter _adapter;

        private CanvasGroup _canvasGroup;
        private Item _item;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _originalParent = _rectTransform.parent;
            _originalAnchoredPosition = _rectTransform.anchoredPosition;
            _originalSiblingIndex = _rectTransform.GetSiblingIndex();

            _rectTransform.SetParent(_canvas.transform);
            _rectTransform.SetAsLastSibling();
            _canvasGroup.blocksRaycasts = false;

            var itemPosition = _adapter.GetItemPosition(_item.ID);
            // _adapter.RemoveItem(itemPosition[0]);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _rectTransform.anchoredPosition += eventData.delta / _canvas.scaleFactor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            var cellView = eventData.pointerEnter?.GetComponent<CellView>();

            if (cellView != null)
            {
                _adapter.AddItem(_item.itemData);
                Destroy(gameObject);
            }
            else
            {
                // Включаем raycast обратно
                _canvasGroup.blocksRaycasts = true;
                // Вернуть на место
                _rectTransform.SetParent(_originalParent);
                _rectTransform.anchoredPosition = _originalAnchoredPosition;
                _rectTransform.SetSiblingIndex(_originalSiblingIndex);
            }
        }

        public void SetItem(Item item)
        {
            _item = item;
        }
    }
}