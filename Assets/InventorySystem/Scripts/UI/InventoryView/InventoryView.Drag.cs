using System;
using System.Collections.Generic;
using Inventories;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Inventories
{
    public partial class InventoryView
    {
        public void OnBeginDrag(PointerEventData eventData)
        {
            transform.SetAsLastSibling();

            if (!_dragProcessor.ProcessChain(eventData, _dragContext, _beginDragHandlers))
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
            _dragProcessor.ProcessChain(eventData, _dragContext, _endDragHandlers);
            CompleteDrag();
        }

        public void SetupDraggableImage(Item item)
        {
            _draggableImage.rectTransform.sizeDelta = new Vector2(
                item.itemData.Size.x * _cellSize.x,
                item.itemData.Size.y * _cellSize.y);

            _draggableImage.sprite = item.itemData.Icon;
            _draggableImage.gameObject.SetActive(true);
        }

        private void CompleteDrag()
        {
            _draggableImage.gameObject.SetActive(false);
            _dragContext.EndDrag();
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

    }
}