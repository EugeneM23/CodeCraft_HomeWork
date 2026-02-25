using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Inventories
{
    public class ReturnItemToStartSlotHandler : IEndDraghendler
    {
        [Inject] private readonly InventoryPresenter _presenter;
        [Inject] private readonly EquipmentPresenter _equipmentView;

        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            if (dragContext.EquipmentSlotImage != null)
            {
                _equipmentView.TryEquip(dragContext.Item);
                return true;
            }

            return false;
        }
    }
}