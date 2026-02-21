using System;
using System.Collections.Generic;
using Inventories;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace Inventories
{
    public partial class InventoryView
    {
        [SerializeField] private Image _draggableImage;
        [Inject] private readonly DragContext _dragContext;
        [Inject] private readonly DragChainProcessor _itemDragProcessor;

        #region Drag

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
            _dragContext.EndDrag();
        }

        #endregion

        #region AdditionalMethods

        public void SetupDraggableImage(Item item)
        {
            _draggableImage.rectTransform.sizeDelta = new Vector2(
                item.itemData.Size.x * _cellSize.x,
                item.itemData.Size.y * _cellSize.y);

            _draggableImage.sprite = item.itemData.Icon;
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

        #endregion
    }
}