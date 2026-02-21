using Game.Scripts.UI.Equipment.Game.Equipment.View;
using UnityEngine.EventSystems;

namespace Inventories
{
    public class DropToEquipmentHandler : IEndDraghendler
    {
        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            // Получаем слот экипировки под курсором при отпускании предмета
            var slot = eventData.pointerCurrentRaycast.gameObject?.GetComponent<EquipmentSlotView>();

            // Если курсор не над слотом экипировки, передаем обработку дальше
            if (slot == null)
                return false;

            // Пытаемся экипировать предмет в слот
            return slot.Equip(dragContext.Item);
        }
    }
}