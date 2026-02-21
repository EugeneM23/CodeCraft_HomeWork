using UnityEngine.EventSystems;

namespace Inventories
{
    public interface IDragChainHandler
    {
        bool Handle(PointerEventData eventData, DragContext dragContext);
    }
}