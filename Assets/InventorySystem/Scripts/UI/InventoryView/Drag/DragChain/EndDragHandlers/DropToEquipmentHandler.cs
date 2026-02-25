using Equipment;
using UnityEngine.EventSystems;

namespace Inventories
{
    public class DropToEquipmentHandler : IEndDraghendler
    {
        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            // Получаем слот экипировки под курсором при отпускании предмета
            EquipmentSlot slot = eventData.pointerCurrentRaycast.gameObject?.GetComponent<EquipmentSlot>();

            // Если курсор не над слотом экипировки, передаем обработку дальше
            if (slot == null || !slot.IsEmpty || slot.ItemType != dragContext.Item.Settings.ItemType)
                return false;

            slot.Presenter.TryEquip(dragContext.Item);

            return true;
        }
    }
}