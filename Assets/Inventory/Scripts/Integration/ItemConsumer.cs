using Game.Scripts.UI.Equipment.Game.Equipment.Presenter;
using Inventories;
using UnityEngine;

public class ItemConsumer : MonoBehaviour, IItemConsumer
{
    public int Health = 50;
    public Inventory Inventory { get; private set; }
    public EquipmentPresenter EquipmentPresenter { get; private set; }

    public bool EquipWeapon(ItemInstance itemInstance)
    {
        if (EquipmentPresenter.EquipWeapon(itemInstance))
            return true;

        return false;
    }

    public bool EquipArmor(ItemInstance itemInstance)
    {
        return EquipmentPresenter != null && EquipmentPresenter.EquipArmor(itemInstance);
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

    public void SetEquipment(EquipmentPresenter equipmentPresenter)
    {
        EquipmentPresenter = equipmentPresenter;
    }
}