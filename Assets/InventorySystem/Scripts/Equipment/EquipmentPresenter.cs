using System;
using System.Collections.Generic;
using Game.Scripts.UI.Equipment.Game.Equipment.View;
using Inventories;
using UnityEngine;

public class EquipmentPresenter : MonoBehaviour
{
    [SerializeField] private EquipmentPanelView _view;
    [SerializeField] private CharacterEquipment _character;

    private Inventory _inventory;
    private Dictionary<ItemType, EquipmentSlotView> _slots;

    public void Initialize(Inventory inventory)
    {
        _inventory = inventory;
        InitializeSlots();
        SubscribeToSlots();
    }

    private void InitializeSlots()
    {
        _slots = new Dictionary<ItemType, EquipmentSlotView>
        {
            [ItemType.Head] = _view.HeadSlot,
            [ItemType.Body] = _view.BodySlot,
            [ItemType.Legs] = _view.LegsSlot,
            [ItemType.Boots] = _view.BootsSlot,
            [ItemType.Hands] = _view.HandsSlot,
            [ItemType.Weapon] = _view.WeaponSlot01,
            [ItemType.Shield] = _view.WeaponSlot02
        };
    }

    private void SubscribeToSlots()
    {
        foreach (var slot in _slots.Values)
        {
            slot.OnEquipped += _character.Equip;
            slot.OnUnEquipped += _character.Unequip;
            slot.OnReturnToInventory += ReturnToInventory;
            slot.OnItemDropped += HandleItemDrop;
        }
    }

    private void OnDestroy()
    {
        if (_slots == null) return;

        foreach (var slot in _slots.Values)
        {
            slot.OnEquipped -= _character.Equip;
            slot.OnUnEquipped -= _character.Unequip;
            slot.OnReturnToInventory -= ReturnToInventory;
            slot.OnItemDropped -= HandleItemDrop;
        }
    }

    private void HandleItemDrop(ItemInstance item)
    {
        var slot = _slots[item.itemData.ItemType];

        if (!slot.IsEmpty)
        {
            var currentItem = slot.CurrentItem;
            slot.Unequip();
            _inventory.AddItem(currentItem.itemData);
        }

        slot.Equip(item);
    }

    private void ReturnToInventory(ItemInstance item)
    {
        if (_inventory.AddItem(item.itemData))
            _slots[item.itemData.ItemType].Unequip();
    }

    public bool EquipItem(ItemInstance item)
    {
        if (!_slots.TryGetValue(item.itemData.ItemType, out var slot))
            return false;

        if (!slot.IsEmpty)
        {
            var currentItem = slot.Unequip();
            _inventory.AddItem(currentItem.itemData);
        }

        return slot.Equip(item);
    }

    public void Toggle() => _view.gameObject.SetActive(!_view.gameObject.activeSelf);
    public void Show() => _view.gameObject.SetActive(true);
    public void Hide() => _view.gameObject.SetActive(false);
}