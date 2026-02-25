using System;
using Equipment;
using Inventories;
using UnityEditor;
using Zenject;

public class EquipmentPresenter : IInitializable, IDisposable
{
    private readonly EquipmentModel _model;
    private readonly SignalBus _signalBus;

    public event Action<ItemType, Item> OnEquipped;
    public event Action<ItemType, Item> OnUnEquipped;

    public EquipmentPresenter(EquipmentModel model, SignalBus signalBus)
    {
        _model = model;
        _signalBus = signalBus;
    }

    public void Initialize()
    {
        _signalBus.Subscribe<OpenEquipmentSignal>(ToggleEquipment);

        _model.OnEquipped += HandleModelEquipped;
        _model.OnUnEquipped += HandleModelUnEquipped;
    }

    public void Dispose()
    {
        _signalBus.Unsubscribe<OpenEquipmentSignal>(ToggleEquipment);
        _model.OnEquipped -= HandleModelEquipped;
        _model.OnUnEquipped -= HandleModelUnEquipped;
    }

    public bool TryEquip(Item item)
    {
        return _model.Equip(item);
    }

    public Item UnEquip(ItemType itemType)
    {
        return _model.UnEquip(itemType);
    }

    private void ToggleEquipment()
    {
        // Логика открытия/закрытия окна экипировки
        // Можно через сигнал управлять
    }

    private void HandleModelEquipped(ItemType itemType, Item item)
    {
        OnEquipped?.Invoke(itemType, item);
    }

    private void HandleModelUnEquipped(ItemType itemType, Item item)
    {
        OnUnEquipped?.Invoke(itemType, item);
    }
}