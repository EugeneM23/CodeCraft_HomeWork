using Inventories;
using UnityEngine;

public class TestCharacter : MonoBehaviour, IItemConsumer
{
    public int Health = 50;

    public Inventory Inventory { get; set; }
    public Equipment Equipment { get; set; }

    public bool EquipWeapon(ItemInstance itemInstance)
    {
        if (Equipment.EquipWeapon(itemInstance, Inventory))
            return true;

        return false;
    }

    public bool EquipArmor(ItemInstance itemInstance)
    {
        if (Equipment.EquipArmor(itemInstance, Inventory))
            return true;

        return false;
    }

    public new T GetComponent<T>()
    {
        if (typeof(T) == typeof(IItemConsumer))
        {
            return (T)(IItemConsumer)this;
        }

        return base.GetComponent<T>();
    }
}