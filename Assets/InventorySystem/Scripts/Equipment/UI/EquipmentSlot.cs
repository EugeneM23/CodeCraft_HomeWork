using System;
using Inventories;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Equipment
{
    public class EquipmentSlot : MonoBehaviour
    {
        [SerializeField] private ItemType _itemType;
        [SerializeField] private Image _itemIcon;

        [Inject] private readonly SignalBus _signalBus;

        private Item _currentItem;
        public bool IsEmpty => _currentItem == null;

        public bool Equip(Item item)
        {
            if (item.Settings.ItemType != _itemType)
                return false;

            _currentItem = item;
            _itemIcon.enabled = true;
            _itemIcon.sprite = item.Settings.Icon;

            return true;
        }

        public Item UnEquip()
        {
            var item = _currentItem;
            _currentItem = null;

            _itemIcon.enabled = false;
            _itemIcon.sprite = null;

            return item;
        }
    }
}