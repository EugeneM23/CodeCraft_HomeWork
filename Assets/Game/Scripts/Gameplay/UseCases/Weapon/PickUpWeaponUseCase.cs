using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class PickUpWeaponUseCase
    {
        public static bool DropWeapon(IEntity character, GameContext gameContext)
        {
            if (!character.TryGetWeapon(out var weapon) || weapon == null) return false;

            Transform weaponTransform = weapon.Value.GetTransform();
            SceneEntity pickUpWeapon = gameContext.GetWeaponCatalog().GetPickUpWeapon(weapon.Value.GetWeaponId());
            SceneEntity.Create(pickUpWeapon, weaponTransform.position, Quaternion.identity);
            GameObject.Destroy(weaponTransform.gameObject);
            character.SetWeapon(null);

            return true;
        }
    }
}