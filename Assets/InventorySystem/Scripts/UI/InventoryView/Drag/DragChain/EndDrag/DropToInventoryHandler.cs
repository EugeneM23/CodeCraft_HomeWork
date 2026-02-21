using UnityEngine;
using UnityEngine.EventSystems;

namespace Inventories
{
    public class DropToInventoryHandler : IEndDraghendler
    {
        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            var cell = eventData.pointerCurrentRaycast.gameObject?.GetComponent<InventoryCell>();

            if (cell == null)
                return false;

            Vector2Int targetPosition = cell.MatrixPosition - dragContext.ClickOffset;
            return cell.Presenter.AddItem(dragContext.Item, targetPosition);
        }
    }
}