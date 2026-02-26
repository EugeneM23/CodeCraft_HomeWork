using System;
using Inventories;
using Zenject;

public class EquipmentPresenter : IInitializable, IDisposable
{
    public event Action OnToggle;
    public event Action OnClose;

    public event Action<ItemType, Item> OnEquipped;
    public event Action<ItemType, Item> OnUnEquipped;

    private readonly EquipmentModel _model;
    private readonly SignalBus _signalBus;

    public EquipmentPresenter(EquipmentModel model, SignalBus signalBus)
    {
        _model = model;
        _signalBus = signalBus;
    }

    public void Initialize()
    {
        _signalBus.Subscribe<ToggleEquipmentSignal>(ToggleEquipment);
        _signalBus.Subscribe<CloseEquipmentSignal>(CloseEquipment);

        _model.OnEquipped += HandleModelEquipped;
        _model.OnUnEquipped += HandleModelUnEquipped;
    }

    public void Dispose()
    {
        _signalBus.Unsubscribe<ToggleEquipmentSignal>(ToggleEquipment);
        _signalBus.Unsubscribe<CloseEquipmentSignal>(ToggleEquipment);

        _model.OnEquipped -= HandleModelEquipped;
        _model.OnUnEquipped -= HandleModelUnEquipped;
    }

    private void CloseEquipment() => OnClose?.Invoke();

    public bool TryEquip(Item item) => _model.Equip(item);

    public Item UnEquip(ItemType itemType) => _model.UnEquip(itemType);

    private void ToggleEquipment() => OnToggle?.Invoke();

    private void HandleModelEquipped(ItemType itemType, Item item) => OnEquipped?.Invoke(itemType, item);

    private void HandleModelUnEquipped(ItemType itemType, Item item) => OnUnEquipped?.Invoke(itemType, item);
}