using System;
using Inventories;
using Zenject;

public class EquipmentEnableController : IInitializable, IDisposable
{
    private readonly EquipmentView _equipment;
    private readonly SignalBus _signalBus;

    public EquipmentEnableController(EquipmentView equipment, SignalBus signalBus)
    {
        _equipment = equipment;
        _signalBus = signalBus;
    }

    public void Initialize()
    {
        _signalBus.Subscribe<EnableEquipmentSignal>(HandleEnableEquipment);
    }

    public void Dispose()
    {
        _signalBus.Unsubscribe<EnableEquipmentSignal>(HandleEnableEquipment);
    }

    private void HandleEnableEquipment(EnableEquipmentSignal signal)
    {
        if (_equipment.ID == signal.ID)
            _equipment.HandleToggle();
    }
}