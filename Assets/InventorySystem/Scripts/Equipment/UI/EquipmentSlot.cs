using System;
using Inventories;
using UnityEngine;
using UnityEngine.UI;

namespace Equipment
{
    public class EquipmentSlot : MonoBehaviour
    {
        [SerializeField] private ItemType _itemType;
        [SerializeField] private Image _itemIcon;

        private Item _currentItem;
        public bool IsEmpty => _currentItem == null;

        public event Action<Item> OnEquipped;
        public event Action<Item> OnUnEquipped;

        public bool Equip(Item item)
        {
            if (item.Settings.ItemType != _itemType)
                return false;

            _currentItem = item;
            _itemIcon.enabled = true;
            _itemIcon.sprite = item.Settings.Icon;

            OnEquipped?.Invoke(item);

            return true;
        }

        public Item UnEquip()
        {
            var item = _currentItem;
            _currentItem = null;

            _itemIcon.enabled = false;
            _itemIcon.sprite = null;

            OnUnEquipped?.Invoke(item);

            return item;
        }

        public bool CanEquip(Item item)
        {
            return item.Settings.ItemType == _itemType && IsEmpty;
        }
    }
}