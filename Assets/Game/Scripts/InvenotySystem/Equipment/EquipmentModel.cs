using System;
using System.Collections.Generic;
using Inventories;
using UnityEngine;

public class EquipmentModel
{
    public event Action<ItemType, Item> OnEquipped;
    public event Action<ItemType, Item> OnUnEquipped;

    private readonly Dictionary<ItemType, Item> _slots = new()
    {
        { ItemType.Head, null },
        { ItemType.Armor, null },
        { ItemType.Hands, null },
        { ItemType.Legs, null },
        { ItemType.Boots, null },
        { ItemType.Weapon, null },
        { ItemType.Shield, null }
    };

    public EquipmentModel() { }

    public EquipmentModel(List<Item> initialItems)
    {
        foreach (var item in initialItems)
        {
            var itemType = item.Settings.ItemType;
            
            if (_slots.ContainsKey(itemType) && _slots[itemType] == null)
            {
                _slots[itemType] = item;
            }
        }
    }

    public Item GetEquippedItem(ItemType itemType)
    {
        return _slots.ContainsKey(itemType) ? _slots[itemType] : null;
    }

    public bool Equip(Item item)
    {
        var itemType = item.Settings.ItemType;

        if (!_slots.ContainsKey(itemType))
            return false;

        if (_slots[itemType] != null)
            return false;

        _slots[itemType] = item;

        OnEquipped?.Invoke(itemType, item);
        return true;
    }

    public Item UnEquip(ItemType itemType)
    {
        if (!_slots.ContainsKey(itemType))
            return null;

        var item = _slots[itemType];

        if (item == null)
            return null;

        _slots[itemType] = null;

        OnUnEquipped?.Invoke(itemType, item);
        return item;
    }
}