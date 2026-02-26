using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Inventories
{
    public class BeginDragFromInventoryCellHandler : IBeginDraghendler
    {
        private readonly InventoryView _view;
        private readonly InventoryPresenter _presenter;
        private readonly ItemDragHandler _itemDragHandler;

        public BeginDragFromInventoryCellHandler(InventoryView view, InventoryPresenter presenter, ItemDragHandler itemDragHandler)
        {
            _view = view;
            _presenter = presenter;
            _itemDragHandler = itemDragHandler;
        }

        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            var cell = eventData.pointerPressRaycast.gameObject.GetComponent<CellView>();
            if (cell == null || cell.Item == null)
                return false;

            var item = cell.Item;
            IReadOnlyDictionary<string, GameObject> items = _view.GetItems();

            Vector2Int itemStartPosition = _presenter.GetItemPosition(item.ID);
            Vector2Int clickOffset = cell.MatrixPosition - itemStartPosition;
            Vector2 dragOffset = (Vector2)items[item.ID].transform.position - eventData.position;

            dragContext.BeginDrag(item, itemStartPosition, clickOffset, dragOffset, cell, null);

            _itemDragHandler.SetupDraggableImage(item);

            _presenter.RemoveItem(item);

            return true;
        }
    }
}