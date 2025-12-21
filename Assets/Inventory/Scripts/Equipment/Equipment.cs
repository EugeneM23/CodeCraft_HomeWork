using Game.Scripts.UI.Equipment;
using Inventories;
using UnityEngine;

public class Equipment : MonoBehaviour
{
    [SerializeField] private EquipmentSlot _weaponSlot;
    [SerializeField] private EquipmentSlot _armorSlot;

    public EquipmentSlot ArmorSlot => _armorSlot;
    public EquipmentSlot WeaponSlot => _weaponSlot;

    public bool EquipWeapon(ItemInstance itemInstance, Inventory inventory)
    {
        if (!_weaponSlot.IsEmpty)
        {
            if (inventory.AddItem(_weaponSlot.ItemInstance.itemData))
            {
                _weaponSlot.RemoveItem();
                _weaponSlot.AddItem(itemInstance);
                return true;
            }

            return false;
        }

        if (_weaponSlot.AddItem(itemInstance))
            return true;

        return false;
    }

    public bool EquipArmor(ItemInstance itemInstance, Inventory inventory)
    {
        if (!_armorSlot.IsEmpty)
        {
            if (inventory.AddItem(_armorSlot.ItemInstance.itemData))
            {
                _armorSlot.RemoveItem();
                _armorSlot.AddItem(itemInstance);
                return true;
            }

            return false;
        }

        if (_armorSlot.AddItem(itemInstance))
            return true;

        return false;
    }
}