using System;
using Zenject;

public class EquipmentEnableController : IInitializable, IDisposable
{
    private readonly InventoryPresenter _inventory;
    private readonly EquipmentPresenter _equipment;

    public EquipmentEnableController(InventoryPresenter inventory, EquipmentPresenter equipment)
    {
        _inventory = inventory;
        _equipment = equipment;
    }

    public void Initialize()
    {
        _inventory.OnToggleEquipment += _equipment.ToggleEquipment;
        _inventory.OnClose += _equipment.CloseEquipment;
    }

    public void Dispose()
    {
        _inventory.OnToggleEquipment -= _equipment.ToggleEquipment;
        _inventory.OnClose -= _equipment.CloseEquipment;
    }
}