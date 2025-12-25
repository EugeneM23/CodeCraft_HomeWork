using System;
using Game.Scripts.UI.Equipment.Game.Equipment.View;
using Inventories;
using UnityEngine;

public class EquipmentDisplay : MonoBehaviour
{
    [SerializeField] private EquipmentPanelView _view;
    [SerializeField] private CharacterEquipment _characterPrefab;

    private CharacterEquipment _character;

    private void OnEnable()
    {
        _character = Instantiate(_characterPrefab);

        _view.BodySlot.OnEquipped += OnBodyEquipped;
        _view.BodySlot.OnUnEquipped += OnBodyUnEquipped;

        _view.HeadSlot.OnEquipped += OnHeadEquipped;
        _view.HeadSlot.OnUnEquipped += OnHeadUnEquipped;

        _view.LegsSlot.OnEquipped += OnLegEquipped;
        _view.LegsSlot.OnUnEquipped += OnLegUnEquipped;
    }

    private void OnDisable()
    {
        Destroy(_character.gameObject);

        _view.BodySlot.OnEquipped -= OnBodyEquipped;
        _view.BodySlot.OnUnEquipped -= OnBodyUnEquipped;

        _view.HeadSlot.OnEquipped -= OnHeadEquipped;
        _view.HeadSlot.OnUnEquipped -= OnHeadUnEquipped;

        _view.LegsSlot.OnEquipped -= OnLegEquipped;
        _view.LegsSlot.OnUnEquipped -= OnLegUnEquipped;
    }

    private void OnBodyEquipped(ItemInstance item, EquipmentSlotView slot) => _character.EquipArmor();

    private void OnBodyUnEquipped(ItemInstance item, EquipmentSlotView slot) => _character.UnEquipArmor();

    private void OnHeadUnEquipped(ItemInstance arg1, EquipmentSlotView arg2) => _character.UnEquipHead();

    private void OnHeadEquipped(ItemInstance arg1, EquipmentSlotView arg2) => _character.EquipHead();
    private void OnLegUnEquipped(ItemInstance arg1, EquipmentSlotView arg2) => _character.UnEquipLegs();

    private void OnLegEquipped(ItemInstance arg1, EquipmentSlotView arg2) => _character.EquipLegs();
}