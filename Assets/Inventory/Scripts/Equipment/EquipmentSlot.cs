using System;
using Inventories;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Scripts.UI.Equipment
{
    public class EquipmentSlot : SerializedMonoBehaviour
    {
        public event Action OnItemAdded;
        public event Action OnItemRemoved;

        [SerializeField] private ItemType _itemType;
        [SerializeField] private Image _itemSlot;

        public ItemType ItemType => _itemType;
        public bool IsEmpty => ItemInstance == null;

        public ItemInstance ItemInstance;

        public bool AddItem(ItemInstance item)
        {
            if (ItemInstance != null) return false;
            if (item.itemData.ItemType != _itemType) return false;

            OnItemAdded?.Invoke();

            _itemSlot.enabled = true;
            _itemSlot.sprite = item.itemData.Icon;
            ItemInstance = item;

            return true;
        }

        public void RemoveItem()
        {
            OnItemRemoved?.Invoke();

            _itemSlot.enabled = false;
            ItemInstance = null;
            _itemSlot.enabled = false;
        }
    }
}