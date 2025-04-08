using System;
using UnityEngine;

namespace Gameplay
{
    public class PlayerWeaponSwitchController : MonoBehaviour
    {
        [SerializeField] private PlayerInput _playerInput;
        [SerializeField] private PlayerWeaponCatalog playerWeaponCatalog;

        private void OnEnable()
        {
            _playerInput.OnNextWeapon += this.playerWeaponCatalog.SetNexWeapon;
            _playerInput.OnPreviousWepon += this.playerWeaponCatalog.PreviousWepon;
        }

        private void OnDisable()
        {
            _playerInput.OnNextWeapon -= this.playerWeaponCatalog.SetNexWeapon;
            _playerInput.OnPreviousWepon -= this.playerWeaponCatalog.PreviousWepon;
        }
    }
}