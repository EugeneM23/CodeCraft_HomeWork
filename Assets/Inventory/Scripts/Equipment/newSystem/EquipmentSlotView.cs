using System;
using Inventories;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Scripts.UI.Equipment.Game.Equipment.View
{
    public class EquipmentSlotView : MonoBehaviour
    {
        public event Action<ItemInstance, EquipmentSlotView> OnRemoveToInventory;
        public event Action<ItemInstance, EquipmentSlotView> OnDropItemToSlot;

        [SerializeField] private Image _itemIcon;
        [SerializeField] private ItemType _allowedItemType;
        [SerializeField] private DoubleClickHandler _doubleClick;

        private void OnEnable() => _doubleClick.OnDoubleClick += RemoveItemToInventory;

        private void OnDisable() => _doubleClick.OnDoubleClick -= RemoveItemToInventory;

        private void RemoveItemToInventory() => OnRemoveToInventory?.Invoke(CurrentItem, this);

        public ItemType AllowedItemType => _allowedItemType;
        public ItemInstance CurrentItem { get; private set; }
        public bool IsEmpty => CurrentItem == null;

        private bool CanEquip(ItemInstance item)
        {
            return item?.itemData.ItemType == AllowedItemType;
        }

        public void DropItemToSlot(ItemInstance itemInstance)
        {
            OnDropItemToSlot?.Invoke(itemInstance, this);
        }

        public bool EquipItem(ItemInstance item)
        {
            if (!CanEquip(item))
                return false;

            CurrentItem = item;
            ShowItem(item.itemData.Icon);

            return true;
        }

        public ItemInstance UnequipItem()
        {
            var item = CurrentItem;
            CurrentItem = null;
            ShowEmpty();
            return item;
        }

        private void ShowItem(Sprite icon)
        {
            _itemIcon.enabled = true;
            _itemIcon.sprite = icon;
        }

        private void ShowEmpty()
        {
            _itemIcon.enabled = false;
            _itemIcon.sprite = null;
        }
    }
}