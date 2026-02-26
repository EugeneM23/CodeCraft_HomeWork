using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Inventories
{
    public class ReturnItemToStartSlotHandler : IEndDraghendler
    {
        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            if (dragContext.EquipmentPresenter != null)
            {
                dragContext.EquipmentPresenter.TryEquip(dragContext.Item);
                return true;
            }

            return false;
        }
    }
}