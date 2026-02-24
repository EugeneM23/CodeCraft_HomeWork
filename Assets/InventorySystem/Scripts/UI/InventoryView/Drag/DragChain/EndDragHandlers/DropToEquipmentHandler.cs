using Equipment;
using UnityEngine.EventSystems;

namespace Inventories
{
    public class DropToEquipmentHandler : IEndDraghendler
    {
        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            // Получаем слот экипировки под курсором при отпускании предмета
            var slot = eventData.pointerCurrentRaycast.gameObject?.GetComponent<EquipmentSlot>();

            // Если курсор не над слотом экипировки, передаем обработку дальше
            if (slot == null || !slot.IsEmpty)
                return false;

            slot.Equip(dragContext.Item);
            // Пытаемся экипировать предмет в слот
            //var success = slot.Presenter.TryEquip(dragContext.Item);

            return true;
        }
    }
}