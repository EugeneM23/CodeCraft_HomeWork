using Game.Scripts.UI.Equipment.Game.Equipment.View;
using UnityEngine.EventSystems;

namespace Inventories
{
    public class DropToEquipmentHandler : IDragChainHandler
    {
        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            var slot = eventData.pointerCurrentRaycast.gameObject?.GetComponent<EquipmentSlotView>();

            if (slot == null)
                return false;

            return slot.Equip(dragContext.Item);
        }
    }
}