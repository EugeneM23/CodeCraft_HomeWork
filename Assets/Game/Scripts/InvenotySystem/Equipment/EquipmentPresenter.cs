using System;
using Inventories;

public class EquipmentPresenter : IDisposable
{
    public event Action OnToggle;
    public event Action OnClose;

    public event Action<ItemType, Item> OnEquipped;
    public event Action<ItemType, Item> OnUnEquipped;

    private readonly EquipmentModel _model;

    public EquipmentPresenter(EquipmentModel model) => _model = model;

    public void Initialize()
    {
        _model.OnEquipped += HandleModelEquipped;
        _model.OnUnEquipped += HandleModelUnEquipped;
    }

    public void Dispose()
    {
        _model.OnEquipped -= HandleModelEquipped;
        _model.OnUnEquipped -= HandleModelUnEquipped;
    }

    public void CloseEquipment() => OnClose?.Invoke();

    public bool TryEquip(Item item)
    {
        return _model.Equip(item);
    }

    public Item UnEquip(ItemType itemType) => _model.UnEquip(itemType);

    public void ToggleEquipment() => OnToggle?.Invoke();

    private void HandleModelEquipped(ItemType itemType, Item item)
    {
        OnEquipped?.Invoke(itemType, item);
    }

    private void HandleModelUnEquipped(ItemType itemType, Item item) => OnUnEquipped?.Invoke(itemType, item);
}