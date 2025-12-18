using System;
using Inventories;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game.Scripts.UI.Equipment
{
    public class EquipmentSlot : SerializedMonoBehaviour
    {
        public event Action OnItemAdded;
        public event Action OnItemRemoved;

        [SerializeField] private BackPack _backPack;
        [SerializeField] private ItemType _itemType;
        [SerializeField] private Image _itemSlot;

        public ItemInstance ItemInstance;

        public bool AddItem(ItemInstance item)
        {
            if (ItemInstance != null) return false;

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