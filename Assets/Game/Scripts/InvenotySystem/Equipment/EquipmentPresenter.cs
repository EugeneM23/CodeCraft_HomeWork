using System;
using System.Collections.Generic;
using Inventories;

public class EquipmentPresenter : IDisposable
{
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

    public bool TryEquip(Item item)
    {
        return _model.Equip(item);
    }

    public Item UnEquip(ItemType itemType) => _model.UnEquip(itemType);

    public Item GetEquippedItem(ItemType itemType) => _model.GetEquippedItem(itemType);

    public Dictionary<ItemType, Item> GetAllEquippedItems()
    {
        var equipped = new Dictionary<ItemType, Item>();
        var types = new[] { ItemType.Head, ItemType.Armor, ItemType.Hands, ItemType.Legs, ItemType.Boots, ItemType.Weapon, ItemType.Shield };
        
        foreach (var type in types)
        {
            var item = _model.GetEquippedItem(type);
            if (item != null)
                equipped[type] = item;
        }
        
        return equipped;
    }

    private void HandleModelEquipped(ItemType itemType, Item item)
    {
        OnEquipped?.Invoke(itemType, item);
    }

    private void HandleModelUnEquipped(ItemType itemType, Item item) => OnUnEquipped?.Invoke(itemType, item);
}