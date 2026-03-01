using System;
using Inventories;
using UnityEngine;
using Zenject;

public class AnimSetController : IInitializable, IDisposable
{
    private readonly EquipmentModel _equipmentModel;
    private readonly Animator _animator;
    private readonly RuntimeAnimatorController _defaultController;

    public AnimSetController(EquipmentModel equipmentModel, Animator animator)
    {
        _equipmentModel = equipmentModel;
        _animator = animator;
        _defaultController = animator.runtimeAnimatorController;
    }

    public void Initialize()
    {
        _equipmentModel.OnEquipped += HandleEquip;
        _equipmentModel.OnUnEquipped += HandleUnequip;
    }

    private void HandleEquip(ItemType itemType, Item item)
    {
        if (itemType == ItemType.Weapon)
            _animator.runtimeAnimatorController = item.Settings.AnimatorController;
    }

    private void HandleUnequip(ItemType itemType, Item _)
    {
        if (itemType == ItemType.Weapon)
            _animator.runtimeAnimatorController = _defaultController;
    }

    public void Dispose()
    {
        _equipmentModel.OnEquipped -= HandleEquip;
        _equipmentModel.OnUnEquipped -= HandleUnequip;
    }
}