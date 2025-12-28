using System;
using Inventories;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Scripts.UI.Equipment.Game.Equipment.View
{
    public class EquipmentSlotView : MonoBehaviour
    {
        public event Action<Item> OnEquipped;
        public event Action<Item> OnUnEquipped;
        public event Action<Item> OnReturnToInventory;
        public event Action<Item> OnItemDropped;

        [SerializeField] private Image _itemIcon;
        [SerializeField] private ItemType _itemType;
        [SerializeField] private DoubleClickHandler _doubleClick;
        [SerializeField] private Image _background;
        [SerializeField] private Sprite _emptySprite;
        [SerializeField] private Sprite _occupiedSprite;

        public ItemType ItemType => _itemType;
        public Item CurrentItem { get; private set; }
        public bool IsEmpty => CurrentItem == null;

        private void OnEnable() => _doubleClick.OnDoubleClick += HandleDoubleClick;
        private void OnDisable() => _doubleClick.OnDoubleClick -= HandleDoubleClick;

        private void HandleDoubleClick()
        {
            if (CurrentItem != null)
                OnReturnToInventory?.Invoke(CurrentItem);
        }

        public void DropItem(Item item) => OnItemDropped?.Invoke(item);

        public bool Equip(Item item)
        {
            if (item?.itemData.ItemType != _itemType)
                return false;

            CurrentItem = item;
            _itemIcon.enabled = true;
            _itemIcon.sprite = item.itemData.Icon;
            _background.sprite = _occupiedSprite;

            OnEquipped?.Invoke(item);
            return true;
        }

        public Item UnEquip()
        {
            Item item = CurrentItem;
            CurrentItem = null;

            _itemIcon.enabled = false;
            _itemIcon.sprite = null;
            _background.sprite = _emptySprite;

            OnUnEquipped?.Invoke(item);
            return item;
        }
    }
}