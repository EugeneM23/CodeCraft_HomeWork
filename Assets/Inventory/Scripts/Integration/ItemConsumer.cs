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
        _controller.Equip(itemInstance);
        
        // if (EquipmentPresenter.EquipWeapon(itemInstance))
        //     return true;

        return false;
    }

    public bool EquipArmor(ItemInstance itemInstance)
    {
        return EquipmentPresenter != null && EquipmentPresenter.EquipArmor(itemInstance);
    }

    public bool EquipAmmo(ItemInstance itemInstance)
    {
        return EquipmentPresenter != null && EquipmentPresenter.EquipAmmo(itemInstance);
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