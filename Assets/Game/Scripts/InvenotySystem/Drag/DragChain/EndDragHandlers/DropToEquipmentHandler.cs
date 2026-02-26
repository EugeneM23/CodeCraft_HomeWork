using UnityEngine;
using UnityEngine.EventSystems;

namespace Inventories
{
    public class DropToEquipmentHandler : IEndDraghendler
    {
        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            Debug.Log("DropToEquipmentHandler");
            var slotObject = eventData.pointerCurrentRaycast.gameObject;

            if (slotObject == null)
                return false;

            var slotMarker = slotObject.GetComponentInParent<EquipmentSlotMarker>();

            if (slotMarker == null)
                return false;

            if (dragContext.Item.Settings.ItemType != slotMarker.ItemType)
                return false;

            return slotMarker.Presenter.TryEquip(dragContext.Item);
        }
    }
}