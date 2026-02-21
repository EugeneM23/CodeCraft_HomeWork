using Zenject;

public class EquipmentPresenter : IInitializable, System.IDisposable
{
    private readonly SignalBus _signalBus;
    private readonly EquipmentView _equipmentView;

    public EquipmentPresenter(SignalBus signalBus, EquipmentView equipmentView)
    {
        _signalBus = signalBus;
        _equipmentView = equipmentView;
    }

    public void Initialize()
    {
        _signalBus.Subscribe<OpenEquipmentSignal>(ToggleEquipment);
    }

    public void Dispose()
    {
        _signalBus.Unsubscribe<OpenEquipmentSignal>(ToggleEquipment);
    }

    private void ToggleEquipment()
    {
        _equipmentView.gameObject.SetActive(!_equipmentView.isActiveAndEnabled);
    }
}