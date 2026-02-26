using UnityEngine.EventSystems;

namespace Inventories
{
    public class DragConditionHandler : IEndDraghendler
    {
        public bool Handle(PointerEventData eventData, DragContext dragContext)
            => !dragContext.IsDragging;
    }
}