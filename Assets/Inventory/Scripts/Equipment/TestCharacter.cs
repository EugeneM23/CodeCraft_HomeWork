using Inventories;
using UnityEngine;
using UnityEngine.Serialization;

public class TestCharacter : MonoBehaviour, IItemConsumer
{
    public int Health = 50;

    public Equipment Equipment;
    public Inventory Inventory { get; set; }

    public bool EquipWeapon(ItemInstance itemInstance)
    {
        if (Equipment.EquipWeapon(itemInstance))
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