using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class DropWeaponUseCase
    {
        public static bool DropWeapon(IEntity character, GameContext gameContext)
        {
            if (!character.TryGetWeapon(out IReactiveVariable<IEntity> weapon) || weapon == null) return false;

            Transform weaponTransform = weapon.Value.GetTransform();
            SceneEntity pickUpWeapon = gameContext.GetWeaponCatalog().GetPickUpWeapon(weapon.Value.GetWeaponId());
            
            pickUpWeapon.GetAmmo().SpendAll();
            pickUpWeapon.GetAmmo().Add(weapon.Value.GetAmmo().GetCount());

            SceneEntity.Create(pickUpWeapon, weaponTransform.position, Quaternion.identity);
            GameObject.Destroy(weaponTransform.gameObject);

            character.GetWeapon().Value = null;

            return true;
        }
    }
}