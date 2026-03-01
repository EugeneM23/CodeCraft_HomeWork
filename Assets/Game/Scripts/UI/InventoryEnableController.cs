using System;
using Inventories;
using Zenject;

public class InventoryEnableController : IInitializable, IDisposable
{
    private readonly InventoryView _inventory;
    private readonly SignalBus _signalBus;

    public InventoryEnableController(InventoryView inventory, SignalBus signalBus)
    {
        _inventory = inventory;
        _signalBus = signalBus;
    }

    public void Initialize()
    {
        _signalBus.Subscribe<EnableInventorySignal>(HandleEnableInventory);
    }

    public void Dispose()
    {
        _signalBus.Unsubscribe<EnableInventorySignal>(HandleEnableInventory);
    }

    private void HandleEnableInventory(EnableInventorySignal signal)
    {
        if (_inventory.ID == signal.ID)
            _inventory.gameObject.SetActive(!_inventory.gameObject.activeSelf);
    }
}