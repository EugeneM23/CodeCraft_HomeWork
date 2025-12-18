using Game.Scripts.UI.Equipment;
using Inventories;
using UnityEngine;

public class Equipment : MonoBehaviour
{
    [SerializeField] private EquipmentSlot _weaponSlot;

    public EquipmentSlot WeaponSlot => _weaponSlot;

    public bool EquipWeapon(ItemInstance itemInstance)
    {
        
        if (_weaponSlot.AddItem(itemInstance))
            return true;

        return false;
    }
}