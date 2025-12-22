using Inventories;
using UnityEngine;

public class ItemConsumer : MonoBehaviour, IItemConsumer
{
    public int Health = 50;
    public Inventory Inventory { get; private set; }
    public Equipment Equipment { get; private set; }

    public bool EquipWeapon(ItemInstance itemInstance)
    {
        if (Equipment.EquipWeapon(itemInstance, Inventory))
            return true;

        return false;
    }

    public bool EquipArmor(ItemInstance itemInstance)
    {
        return Equipment != null && Equipment.EquipArmor(itemInstance, Inventory);
    }

    public new T GetComponent<T>()
    {
        if (typeof(T) == typeof(IItemConsumer))
        {
            return (T)(IItemConsumer)this;
        }

        return base.GetComponent<T>();
    }

    public void SetInventory(Inventory inventory)
    {
        Inventory = inventory;
    }

    public void SetEquipment(Equipment equipmentEquipment)
    {
        Equipment = equipmentEquipment;
    }
}