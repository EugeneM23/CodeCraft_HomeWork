using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Inventories
{
    public class BeginDragFromEquipmentSlotHandler : IBeginDraghendler
    {
        [Inject] private readonly ItemDragHandler _itemDragHandler;
        [Inject] private readonly EquipmentPresenter _equipmentPresenter;

        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            var slotObject = eventData.pointerPressRaycast.gameObject;

            if (!slotObject.TryGetComponent(out EquipmentSlotMarker slotMarker))
                return false;

            var itemType = slotMarker.ItemType;
            var item = _equipmentPresenter.UnEquip(itemType);

            if (item == null)
                return false;

            Vector2 dragOffset = (Vector2)slotMarker.transform.position - eventData.position;

            dragContext.BeginDrag(item, Vector2Int.zero, Vector2Int.zero, dragOffset, null, _equipmentPresenter);

            _itemDragHandler.SetupDraggableImage(item);

            return true;
        }
    }
}