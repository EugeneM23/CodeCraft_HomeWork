using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Inventories
{
    public class BeginDragFromInventoryCellHandler : IDragChainHandler
    {
        [Inject] private readonly InventoryView _view;

        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            var cell = eventData.pointerPressRaycast.gameObject.GetComponent<InventoryCell>();

            if (cell == null || cell.Item == null)
                return false;

            var item = cell.Item;
            var items = _view.GetItems();

            Vector2Int itemStartPosition = _view.Presenter.GetItemPosition(item.ID);
            Vector2Int clickOffset = cell.MatrixPosition - itemStartPosition;
            Vector2 dragOffset = (Vector2)items[item.ID].transform.position - eventData.position;

            dragContext.BeginDrag(item, itemStartPosition, clickOffset, dragOffset);

            _view.SetupDraggableImage(item);
            _view.Presenter.RemoveItem(item);

            return true;
        }
    }
}