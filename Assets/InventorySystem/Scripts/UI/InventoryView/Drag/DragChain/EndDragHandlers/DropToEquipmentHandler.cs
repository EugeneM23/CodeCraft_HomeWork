using Equipment;
using UnityEngine.EventSystems;
using Zenject;

namespace Inventories
{
    public class DropToEquipmentHandler : IEndDraghendler
    {
        [Inject] private readonly EquipmentPresenter _equipmentView;

        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            var slotObject = eventData.pointerCurrentRaycast.gameObject;

            if (slotObject == null)
                return false;

            var slotMarker = slotObject.GetComponentInParent<EquipmentSlotMarker>();

            if (slotMarker == null)
                return false;

            if (dragContext.Item.Settings.ItemType != slotMarker.ItemType)
                return false;

            return _equipmentView.TryEquip(dragContext.Item);
        }
    }
}