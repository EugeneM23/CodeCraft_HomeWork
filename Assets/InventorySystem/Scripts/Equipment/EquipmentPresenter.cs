using System;
using Equipment;
using Equipment.Equipment;
using Inventories;
using UnityEditor;
using Zenject;

public class EquipmentPresenter : IInitializable, IDisposable
{
    private readonly EquipmentModel _model;
    private readonly EquipmentView _view;
    private readonly SignalBus _signalBus;

    public EquipmentPresenter(EquipmentModel model, EquipmentView view, SignalBus signalBus)
    {
        _model = model;
        _view = view;
        _signalBus = signalBus;
    }

    public void Initialize()
    {
        // Подписываемся на сигнал открытия экипировки
        _signalBus.Subscribe<OpenEquipmentSignal>(ToggleEquipment);

        // Подписываемся на события вью
        _view.OnSlotClicked += HandleSlotClick;

        // Подписываемся на события модели
        _model.OnEquipped += HandleEquipped;
        _model.OnUnEquipped += HandleUnEquipped;
    }

    public void Dispose()
    {
        _signalBus.Unsubscribe<OpenEquipmentSignal>(ToggleEquipment);
        _view.OnSlotClicked -= HandleSlotClick;
        _model.OnEquipped -= HandleEquipped;
        _model.OnUnEquipped -= HandleUnEquipped;
    }

    public bool TryEquip(Item item)
    {
        return _model.Equip(item);
    }

    public Item UnEquip(ItemType itemType) => _model.UnEquip(itemType);

    private void ToggleEquipment()
    {
        _view.gameObject.SetActive(!_view.isActiveAndEnabled);
    }

    private void HandleSlotClick(ItemType itemType)
    {
        // Логика клика по слоту (например, снять предмет)
        var item = _model.UnEquip(itemType);
        if (item != null)
        {
            // Можно вернуть в инвентарь через сигнал
        }
    }

    private void HandleEquipped(ItemType itemType, Item item)
    {
        _view.ShowItem(itemType, item);
    }

    private void HandleUnEquipped(ItemType itemType, Item item)
    {
        _view.HideItem(itemType);
    }
}