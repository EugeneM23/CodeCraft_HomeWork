using Inventories;
using UnityEngine;

public class ItemConsumer : MonoBehaviour, IItemConsumer
{
    public int Health = 50;
    public InventoryPresenter InventoryPresenter { get; private set; }
    private EquipmentPresenter EquipmentPresenter { get; set; }

    public bool Equip(Item item)
    {
        if (EquipmentPresenter.EquipItem(item))
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

    public void SetInventory(InventoryPresenter presenter)
    {
        InventoryPresenter = presenter;
    }

    public void SetEquipment(EquipmentPresenter equipmentPresenter)
    {
        EquipmentPresenter = equipmentPresenter;
    }
}