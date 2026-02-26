using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Inventories
{
    public class BeginDragTestlotHandler : IBeginDraghendler
    {
        [Inject] private readonly InventoryView _view;

        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            // Получаем слот экипировки под курсором
            var transform = eventData.pointerPressRaycast.gameObject.GetComponent<Transform>();

            Debug.Log(transform.name);

            return true;
        }
    }
}