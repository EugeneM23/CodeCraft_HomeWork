using System;
using System.Collections.Generic;
using Inventories;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class EquipmentView : MonoBehaviour
{
    [SerializeField] private Image _headSlot;
    [SerializeField] private Image _bodySlot;
    [SerializeField] private Image _handsSlot;
    [SerializeField] private Image _legsSlot;
    [SerializeField] private Image _bootsSlot;
    [SerializeField] private Image _weaponSlot;
    [SerializeField] private Image _shieldSlot;

    [Inject] private readonly EquipmentPresenter _presenter;

    private Dictionary<ItemType, Image> _slots;

    private void Awake()
    {
        _presenter.OnToggle += HandleToggle;
        _presenter.OnClose += HandleClose;

        _slots = new Dictionary<ItemType, Image>
        {
            { ItemType.Head, _headSlot },
            { ItemType.Body, _bodySlot },
            { ItemType.Hands, _handsSlot },
            { ItemType.Legs, _legsSlot },
            { ItemType.Boots, _bootsSlot },
            { ItemType.Weapon, _weaponSlot },
            { ItemType.Shield, _shieldSlot }
        };
    }

    private void HandleClose() => gameObject.SetActive(false);

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

    private void HandleToggle() => gameObject.SetActive(!gameObject.activeSelf);

    private void HandleEquipped(ItemType itemType, Item item)
    {
        if (_slots.TryGetValue(itemType, out var image))
        {
            image.sprite = item.Settings.Icon;
            image.enabled = true;
        }
    }

    private void HandleUnEquipped(ItemType itemType, Item item)
    {
        if (_slots.TryGetValue(itemType, out var image))
        {
            image.sprite = null;
            image.enabled = false;
        }
    }
}