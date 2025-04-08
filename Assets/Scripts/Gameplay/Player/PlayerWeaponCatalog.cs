using UnityEngine;

namespace Gameplay
{
    public class PlayerWeaponCatalog : MonoBehaviour
    {
        [SerializeField] private WeaponData[] _weapons;
        
        private Character _player;
        private int _currentWeapon = 0;

        public void SetNexWeapon()
        {
            _currentWeapon++;

            if (_currentWeapon >= _weapons.Length)
                _currentWeapon = 0;

            _player.SetWeaponData(_weapons[_currentWeapon]);
            
            PrintWeaponName();
        }

        public void PreviousWepon()
        {
            _currentWeapon--; 

            if (_currentWeapon < 0)
                _currentWeapon = _weapons.Length - 1;

            _player.SetWeaponData(_weapons[_currentWeapon]);
        }

        public void SetPlayer(Character player) => _player = player;

        private void PrintWeaponName() => Debug.Log(_weapons[_currentWeapon].name);
    }
}