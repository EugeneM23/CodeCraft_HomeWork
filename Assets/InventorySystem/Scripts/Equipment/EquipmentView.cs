using UnityEngine;

namespace Equipment
{
    using System;
    using System.Collections.Generic;
    using Inventories;
    using UnityEngine;

    namespace Equipment
    {
        public class EquipmentView : MonoBehaviour
        {
            [SerializeField] private EquipmentSlot _headSlot;
            [SerializeField] private EquipmentSlot _bodySlot;
            [SerializeField] private EquipmentSlot _handsSlot;
            [SerializeField] private EquipmentSlot _legsSlot;
            [SerializeField] private EquipmentSlot _bootsSlot;
            [SerializeField] private EquipmentSlot _weaponSlot;
            [SerializeField] private EquipmentSlot _shieldSlot;

            public event Action<ItemType> OnSlotClicked;

            private Dictionary<ItemType, EquipmentSlot> _slots;

            private void Awake()
            {
                _slots = new Dictionary<ItemType, EquipmentSlot>
                {
                    { ItemType.Head, _headSlot },
                    { ItemType.Body, _bodySlot },
                    { ItemType.Hands, _handsSlot },
                    { ItemType.Legs, _legsSlot },
                    { ItemType.Boots, _bootsSlot },
                    { ItemType.Weapon, _weaponSlot },
                    { ItemType.Shield, _shieldSlot }
                };
            }

            public void ShowItem(ItemType itemType, Item item)
            {
                if (_slots.TryGetValue(itemType, out var slot))
                {
                    slot.Equip(item);
                }
            }

            public void HideItem(ItemType itemType)
            {
                if (_slots.TryGetValue(itemType, out var slot))
                {
                    slot.UnEquip();
                }
            }

            public EquipmentSlot[] GetAllSlots()
            {
                return new[]
                {
                    _headSlot, _bodySlot, _handsSlot, _legsSlot,
                    _bootsSlot, _weaponSlot, _shieldSlot
                };
            }
        }
    }
}