using UnityEngine.EventSystems;
using Zenject;

namespace Inventories
{
    public class ReturnItemToStartPositionHandler : IEndDraghendler
    {
        [Inject] private readonly InventoryPresenter _presenter;

        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            if (dragContext.Item == null)
                return true;

            return _presenter.AddItem(dragContext.Item, dragContext.StartPosition);
        }
    }
}