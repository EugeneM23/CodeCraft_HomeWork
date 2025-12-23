using System;
using Inventories;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Scripts.UI.Equipment
{
    public class EquipmentSlot : MonoBehaviour
    {
        public event Action<ItemInstance> OnEquip;
        public event Action<ItemInstance> OnUnequip;

        [SerializeField] private ItemType _itemType;
        [SerializeField] private Image _itemIcon;

        public ItemType ItemType => _itemType;
        public ItemInstance ItemInstance { get; private set; }
        public bool IsEmpty => ItemInstance == null;

        public void Remove()
        {
            OnUnequip?.Invoke(ItemInstance);

            ItemInstance = null;
            _itemIcon.enabled = false;
            _itemIcon.sprite = null;
        }

        public bool AddItem(ItemInstance itemInstance)
        {
            if (!IsEmpty) return false;

            OnEquip?.Invoke(itemInstance);
            return true;
        }
    }
}