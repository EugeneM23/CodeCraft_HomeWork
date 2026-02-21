using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace Inventories
{
    public class DragChainProcessor
    {
        public bool ProcessChain(PointerEventData eventData, DragContext dragContext, List<IDragChainHandler> handlers)
        {
            foreach (var handler in handlers)
            {
                if (handler.Handle(eventData, dragContext))
                    return true;
            }

            return false;
        }
    }
}