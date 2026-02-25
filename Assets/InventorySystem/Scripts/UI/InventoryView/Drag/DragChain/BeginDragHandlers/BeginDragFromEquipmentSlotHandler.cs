using Equipment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace Inventories
{
    public class BeginDragFromEquipmentSlotHandler : IBeginDraghendler
    {
        [Inject] private readonly InventoryView _inventoryView;
        [Inject] private readonly EquipmentView _equipmentView;

        public bool Handle(PointerEventData eventData, DragContext dragContext)
        {
            var slotObject = eventData.pointerPressRaycast.gameObject;
            var slotMarker = slotObject.GetComponentInParent<EquipmentSlotMarker>();
            var equipmentSlotImage = slotMarker.GetComponent<Image>();

            if (slotMarker == null)
                return false;

            var itemType = slotMarker.ItemType;
            var item = _equipmentView.RequestUnEquip(itemType);

            if (item == null)
                return false;

            Vector2 dragOffset = (Vector2)slotMarker.transform.position - eventData.position;

            dragContext.BeginDrag(item, Vector2Int.zero, Vector2Int.zero, dragOffset, null, equipmentSlotImage);

            _inventoryView.SetupDraggableImage(item);

            return true;
        }
    }
}