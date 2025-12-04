using Inventories;
using UnityEngine;

namespace Game.Scripts.UI.Equipment
{
    public class EquipmentSlot : MonoBehaviour
    {
        [SerializeField] private ItemType _itemType;
        public ItemType ItemTipe => _itemType;
        public InventoryItem CurrentItem => _currentItem;

        private InventoryItem _currentItem;
        private Vector2 _itemSize;

        public void AddItem(InventoryItem item)
        {
            _currentItem = item;

            RectTransform itemRect = item.GetComponent<RectTransform>();
            RectTransform slotRect = GetComponent<RectTransform>();

            _itemSize = itemRect.sizeDelta;
            itemRect.SetParent(slotRect, false);

            itemRect.sizeDelta = slotRect.sizeDelta;

            itemRect.anchoredPosition = Vector2.zero;

            itemRect.anchorMin = slotRect.anchorMin;
            itemRect.anchorMax = slotRect.anchorMax;
            itemRect.pivot = slotRect.pivot;
        }
    }
}