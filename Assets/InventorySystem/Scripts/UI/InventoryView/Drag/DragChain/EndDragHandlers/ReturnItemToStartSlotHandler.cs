using UnityEngine.EventSystems;
using Zenject;

namespace Inventories
{
    public class ReturnItemToStartSlotHandler : IEndDraghendler
    {
        [Inject] private readonly InventoryPresenter _presenter;

        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            if (dragContext.Slot == null)
                return false;

            if (dragContext.Slot.Equip(dragContext.Item))
                return true;

            return false;
        }
    }
}