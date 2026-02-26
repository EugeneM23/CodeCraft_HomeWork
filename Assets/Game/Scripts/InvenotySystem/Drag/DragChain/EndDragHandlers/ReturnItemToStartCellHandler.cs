using UnityEngine.EventSystems;
using Zenject;

namespace Inventories
{
    public class ReturnItemToStartCellHandler : IEndDraghendler
    {
        [Inject] private readonly InventoryPresenter _presenter;

        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            if (dragContext.StartCellView == null)
                return false;

            return dragContext.StartCellView.Presenter.AddItem(dragContext.Item, dragContext.StartPosition);
        }
    }
}