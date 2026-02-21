using System;
using System.Collections.Generic;
using Game.Scripts.UI.Equipment.Game.Equipment.View;
using Inventories;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Inventories
{
    public partial class InventoryView
    {
        private EndDragChainProcessor _endDragProcessor;

        private void InitializeDragProcessor()
        {
            var handlers = new List<IDragEndHandler>
            {
                new DragConditionHandler(),
                new DropToEquipmentHandler(),
                new DropToInventoryHandler(),
                new ReturnItemToStartPositionHandler(_presenter)
            };

            _endDragProcessor = new EndDragChainProcessor(handlers);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_draggableImage != null && _dragContext.IsDragging)
            {
                _draggableImage.rectTransform.position = eventData.position + _dragContext.DragOffset;

                InventoryCell inventoryCell = GetCellUnderPointer(eventData);
                _dragContext.UpdateCurrentCell(inventoryCell);
                return;
            }

            DragWindow(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            var cell = GetCellUnderPointer(eventData);
            var slot = eventData.pointerPress?.GetComponent<EquipmentSlotView>();

            transform.SetAsLastSibling();

            if (slot != null && !slot.IsEmpty)
            {
                BeginDragFromEquipmentSlot(slot, eventData);
                return;
            }

            if (cell != null && cell.Item != null)
            {
                BeginDragFromInventoryCell(cell, eventData);
                return;
            }

            BeginWindowDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _endDragProcessor.ProcessChain(eventData, _dragContext);
            CompleteDrag();
        }

        private void BeginDragFromEquipmentSlot(EquipmentSlotView slot, PointerEventData eventData)
        {
            var item = slot.UnEquip();
            Vector2 dragOffset = (Vector2)slot.transform.position - eventData.position;

            _dragContext.BeginDrag(item, Vector2Int.zero, Vector2Int.zero, dragOffset);

            SetupDraggableImage(item);
        }

        private void BeginDragFromInventoryCell(InventoryCell cell, PointerEventData eventData)
        {
            Vector2Int itemStartPosition = _presenter.GetItemPosition(cell.Item.ID);
            Vector2Int clickOffset = cell.MatrixPosition - itemStartPosition;
            Vector2 dragOffset = (Vector2)_items[cell.Item.ID].transform.position - eventData.position;

            _dragContext.BeginDrag(cell.Item, itemStartPosition, clickOffset, dragOffset);

            SetupDraggableImage(cell.Item);
            _presenter.RemoveItem(cell.Item);
        }

        private void SetupDraggableImage(Item item)
        {
            _draggableImage.rectTransform.sizeDelta = new Vector2(
                item.itemData.Size.x * _cellSize.x,
                item.itemData.Size.y * _cellSize.y);

            _draggableImage.sprite = item.itemData.Icon;
            _draggableImage.gameObject.SetActive(true);
        }

        private InventoryCell GetCellUnderPointer(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _gridContainer as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint);

            int x = Mathf.FloorToInt(localPoint.x / _cellSize.x);
            int y = Mathf.FloorToInt(-localPoint.y / _cellSize.y);

            if (x >= 0 && x < _presenter.Width && y >= 0 && y < _presenter.Height)
                return _cells[x, y];

            return null;
        }

        private void CompleteDrag()
        {
            _draggableImage.gameObject.SetActive(false);
            _dragContext.EndDrag();
        }
    }

    public interface IDragEndHandler
    {
        bool IsComplete(PointerEventData eventData, DragContext dragContext);
    }

    public class DragConditionHandler : IDragEndHandler
    {
        public bool IsComplete(PointerEventData eventData, DragContext dragContext)
        {
            return !dragContext.IsDragging;
        }
    }

    public class DropToEquipmentHandler : IDragEndHandler
    {
        public bool IsComplete(PointerEventData eventData, DragContext dragContext)
        {
            var slot = eventData.pointerCurrentRaycast.gameObject?.GetComponent<EquipmentSlotView>();

            if (slot == null)
                return false;

            return slot.Equip(dragContext.Item);
        }
    }

    public class DropToInventoryHandler : IDragEndHandler
    {
        public bool IsComplete(PointerEventData eventData, DragContext dragContext)
        {
            var cell = eventData.pointerCurrentRaycast.gameObject?.GetComponent<InventoryCell>();

            if (cell == null)
                return false;

            Vector2Int targetPosition = cell.MatrixPosition - dragContext.ClickOffset;
            return cell.Presenter.AddItem(dragContext.Item, targetPosition);
        }
    }

    public class ReturnItemToStartPositionHandler : IDragEndHandler
    {
        private readonly InventoryPresenter _presenter;

        public ReturnItemToStartPositionHandler(InventoryPresenter presenter)
        {
            _presenter = presenter;
        }

        public bool IsComplete(PointerEventData eventData, DragContext dragContext)
        {
            return _presenter.AddItem(dragContext.Item, dragContext.StartPosition);
        }
    }

    public class EndDragChainProcessor
    {
        private readonly List<IDragEndHandler> _handlers;

        public EndDragChainProcessor(List<IDragEndHandler> handlers)
        {
            _handlers = handlers;
        }

        public void ProcessChain(PointerEventData eventData, DragContext dragContext)
        {
            foreach (var handler in _handlers)
            {
                if (handler.IsComplete(eventData, dragContext))
                    break;
            }
        }
    }
}