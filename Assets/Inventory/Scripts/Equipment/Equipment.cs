using Game.Scripts.UI.Equipment;
using Inventories;
using UnityEngine;

public class Equipment : MonoBehaviour
{
    [SerializeField] private EquipmentSlot _weaponSlot;
    [SerializeField] private EquipmentSlot _armorSlot;
    [SerializeField] private EquipmentSlot _itemSlot01;
    [SerializeField] private EquipmentSlot _itemSlot02;
    [SerializeField] private EquipmentSlot _itemSlot03;


    public bool EquipWeapon(ItemInstance item, Inventory inventory)
    {
        return EquipToSlot(item, inventory, _weaponSlot);
    }

    public bool EquipArmor(ItemInstance item, Inventory inventory)
    {
        return EquipToSlot(item, inventory, _armorSlot);
    }

    private bool EquipToSlot(ItemInstance item, Inventory inventory, EquipmentSlot slot)
    {
        if (item.itemData.ItemType != slot.ItemType)
            return false;

        if (!slot.IsEmpty)
        {
            if (!inventory.AddItem(slot.ItemInstance.itemData))
                return false;

            slot.Remove();
        }

        return true;
    }
}