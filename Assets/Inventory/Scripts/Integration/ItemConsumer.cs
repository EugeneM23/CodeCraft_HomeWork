using Game.Scripts.UI.Equipment.Game.Equipment.Presenter;
using Inventories;
using UnityEngine;

public class ItemConsumer : MonoBehaviour, IItemConsumer
{
    public int Health = 50;
    private EquipmentController _controller;
    public Inventory Inventory { get; private set; }
    private EquipmentPresenter EquipmentPresenter { get; set; }

    public bool Equip(ItemInstance itemInstance)
    {
        if (EquipmentPresenter.EquipToSlot(itemInstance))
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

    public void SetInventory(Inventory inventory)
    {
        Inventory = inventory;
    }

    public void SetEquipment(EquipmentPresenter equipmentPresenter)
    {
        EquipmentPresenter = equipmentPresenter;
    }

    public void SetController(EquipmentController controller)
    {
        _controller = controller;
    }
}