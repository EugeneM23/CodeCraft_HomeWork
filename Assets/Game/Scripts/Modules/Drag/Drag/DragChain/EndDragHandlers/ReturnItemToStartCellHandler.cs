using UnityEngine.EventSystems;
using Zenject;

namespace Inventories
{
    public class ReturnItemToStartCellHandler : IEndDraghendler
    {
        [Inject] private readonly InventoryPresenter _presenter;

        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            if (dragContext.StartCell == null)
                return false;

            return dragContext.StartCell.Presenter.AddItem(dragContext.Item, dragContext.StartPosition);
        }
    }
}