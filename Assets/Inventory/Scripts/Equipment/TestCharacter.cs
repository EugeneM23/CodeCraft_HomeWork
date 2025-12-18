using Inventories;
using UnityEngine;

public class TestCharacter : MonoBehaviour, IItemConsumer
{
    public int Health = 50;
    [SerializeField] private Equipment _equipment;
    public Inventory Inventory { get; set; }

    public bool EquipWeapon(ItemInstance itemInstance)
    {
        if (_equipment.EquipWeapon(itemInstance))
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