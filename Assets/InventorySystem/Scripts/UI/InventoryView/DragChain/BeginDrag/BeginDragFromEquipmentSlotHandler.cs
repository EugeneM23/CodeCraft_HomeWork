using Game.Scripts.UI.Equipment.Game.Equipment.View;
using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Inventories
{
    public class BeginDragFromEquipmentSlotHandler : IDragChainHandler
    {
        [Inject] private readonly InventoryView _view;

        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            var slotView = eventData.pointerPressRaycast.gameObject.GetComponent<EquipmentSlotView>();

            if (slotView == null || slotView.IsEmpty)
                return false;

            Item item = slotView.UnEquip();
            Vector2 dragOffset = (Vector2)slotView.transform.position - eventData.position;

            dragContext.BeginDrag(item, Vector2Int.zero, Vector2Int.zero, dragOffset);

            _view.SetupDraggableImage(item);

            return true;
        }
    }
}