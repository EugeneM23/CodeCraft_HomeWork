using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace Inventories
{
    public class DragChainProcessor
    {
        private readonly DragContext _dragContext;
        private readonly List<IBeginDraghendler> _beginHandlers;
        private readonly List<IEndDraghendler> _endHandlers;

        public DragChainProcessor(DragContext dragContext, List<IBeginDraghendler> beginHandlers,
            List<IEndDraghendler> endHandlers)
        {
            _dragContext = dragContext;
            _beginHandlers = beginHandlers;
            _endHandlers = endHandlers;
        }

        public bool ProcessBeginDrag(PointerEventData eventData) => Process(eventData, _beginHandlers);

        public bool ProcessEndDrag(PointerEventData eventData) => Process(eventData, _endHandlers);

        private bool Process(PointerEventData eventData, IEnumerable<IDragChainHandler> handlers)
        {
            foreach (var handler in handlers)
                if (handler.Handle(eventData, _dragContext))
                    return true;

            return false;
        }
    }
}