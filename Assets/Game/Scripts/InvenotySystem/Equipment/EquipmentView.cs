using System;
using System.Collections.Generic;
using System.ComponentModel;
using Inventories;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class EquipmentView : MonoBehaviour
{
    [SerializeField] private EquipmentSlot _headSlot;
    [SerializeField] private EquipmentSlot _bodySlot;
    [SerializeField] private EquipmentSlot _handsSlot;
    [SerializeField] private EquipmentSlot _legsSlot;
    [SerializeField] private EquipmentSlot _bootsSlot;
    [SerializeField] private EquipmentSlot _weaponSlot;
    [SerializeField] private EquipmentSlot _shieldSlot;

    private EquipmentPresenter _presenter;

    private Dictionary<ItemType, EquipmentSlot> _slots;
    public int ID => gameObject.GetInstanceID();

    [Inject]
    public void Construct(EquipmentPresenter presenter)
    {
        _presenter = presenter;

        _slots = new Dictionary<ItemType, EquipmentSlot>
        {
            { ItemType.Head, _headSlot },
            { ItemType.Body, _bodySlot },
            { ItemType.Hands, _handsSlot },
            { ItemType.Legs, _legsSlot },
            { ItemType.Boots, _bootsSlot },
            { ItemType.Weapon, _weaponSlot },
            { ItemType.Shield, _shieldSlot }
        };

        foreach (var item in _slots)
        {
            var slot = item.Value.gameObject.GetComponent<EquipmentSlot>();
            slot.Construct(presenter);
        }
    }

    private void OnEnable()
    {
        _presenter.OnEquipped += HandleEquipped;
        _presenter.OnUnEquipped += HandleUnEquipped;
    }

    private void OnDisable()
    {
        _presenter.OnEquipped -= HandleEquipped;
        _presenter.OnUnEquipped -= HandleUnEquipped;
    }

    public void HandleToggle(bool isEnable) => gameObject.SetActive(isEnable);

    private void HandleEquipped(ItemType itemType, Item item)
    {
        if (_slots.TryGetValue(itemType, out var slot))
        {
            slot.ItemIcon.sprite = item.Settings.Icon;
            slot.ItemIcon.enabled = true;
        }
    }

    private void HandleUnEquipped(ItemType itemType, Item item)
    {
        if (_slots.TryGetValue(itemType, out var slot))
            slot.ItemIcon.enabled = false;
    }
}