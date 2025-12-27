using System;
using Game.Scripts.UI.Equipment.Game.Equipment.View;
using Inventories;
using UnityEngine;

public class EquipmentController : MonoBehaviour
{
    [SerializeField] private EquipmentPanelView _view;
    [SerializeField] private CharacterEquipment _character;

    private void OnEnable()
    {
        _view.WeaponSlot01.OnEquipped += Equip;
        _view.WeaponSlot01.OnUnEquipped += UnEquip;

        _view.WeaponSlot02.OnEquipped += Equip;
        _view.WeaponSlot02.OnUnEquipped += UnEquip;

        _view.BodySlot.OnEquipped += Equip;
        _view.BodySlot.OnUnEquipped += UnEquip;

        _view.HeadSlot.OnEquipped += Equip;
        _view.HeadSlot.OnUnEquipped += UnEquip;

        _view.LegsSlot.OnEquipped += Equip;
        _view.LegsSlot.OnUnEquipped += UnEquip;

        _view.BootsSlot.OnEquipped += Equip;
        _view.BootsSlot.OnUnEquipped += UnEquip;

        _view.HandsSlot.OnEquipped += Equip;
        _view.HandsSlot.OnUnEquipped += UnEquip;
    }

    private void OnDisable()
    {
        _view.WeaponSlot01.OnEquipped -= Equip;
        _view.WeaponSlot01.OnUnEquipped -= UnEquip;

        _view.WeaponSlot02.OnEquipped -= Equip;
        _view.WeaponSlot02.OnUnEquipped -= UnEquip;

        _view.BodySlot.OnEquipped -= Equip;
        _view.BodySlot.OnUnEquipped -= UnEquip;

        _view.HeadSlot.OnEquipped -= Equip;
        _view.HeadSlot.OnUnEquipped -= UnEquip;

        _view.LegsSlot.OnEquipped -= Equip;
        _view.LegsSlot.OnUnEquipped -= UnEquip;

        _view.BootsSlot.OnEquipped -= Equip;
        _view.BootsSlot.OnUnEquipped -= UnEquip;

        _view.HandsSlot.OnEquipped -= Equip;
        _view.HandsSlot.OnUnEquipped -= UnEquip;
    }

    private void UnEquip(ItemInstance item, EquipmentSlotView slot = null) => _character.UnEquip(item, slot);

    private void Equip(ItemInstance item, EquipmentSlotView slot = null) => _character.Equip(item, slot);
}