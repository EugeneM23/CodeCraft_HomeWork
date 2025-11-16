using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class CurrentWeaponUseCase
    {
        public static bool DropWeapon(IEntity character)
        {
            IEntity weapon = character.GetWeapon().Value;

            if (weapon == null) return false;

            Transform weaponTransform = weapon.GetTransform();
 
            SceneEntity pickUpPrefab = weapon.GetPickUpPrefab();
            SceneEntity.Create(pickUpPrefab, weaponTransform.position, Quaternion.identity);
            GameObject.Destroy(weaponTransform.gameObject);
            character.SetWeapon(null);

            return true;
        }
    }
}