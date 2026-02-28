using UnityEngine;
using UnityEngine.EventSystems;

namespace Inventories
{
    public class BeginDragFromEquipmentSlotHandler : IBeginDraghendler
    {
        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            GameObject slotObject = eventData.pointerPressRaycast.gameObject;

            if (!slotObject.TryGetComponent(out EquipmentSlot slotMarker))
                return false;

            var itemType = slotMarker.ItemType;
            var item = slotMarker.Presenter.UnEquip(itemType);

            var handler = slotMarker.ItemDragHandler;

            if (item == null)
                return false;

            Vector2 dragOffset = (Vector2)slotMarker.transform.position - eventData.position;

            dragContext.BeginDrag(item, Vector2Int.zero, Vector2Int.zero, dragOffset, null, slotMarker.Presenter);

            handler.SetupDraggableImage(item);

            return true;
        }
    }
}