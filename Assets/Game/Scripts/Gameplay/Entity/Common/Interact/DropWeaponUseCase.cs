using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class DropWeaponUseCase
    {
        public static bool DropWeapon(IEntity character, GameContext gameContext)
        {
            if (!character.TryGetWeapon(out IReactiveVariable<IEntity> weapon) || weapon.Value == null) return false;

            Transform weaponTransform = weapon.Value.GetTransform();
            SceneEntity pickUpWeapon = gameContext.GetWeaponCatalog().GetPickUpWeapon(weapon.Value.GetWeaponId());
            pickUpWeapon.GetTransform().SetPositionAndRotation(weaponTransform.position, weaponTransform.rotation);

            pickUpWeapon.GetAmmo().SpendAll();
            pickUpWeapon.GetAmmo().Add(weapon.Value.GetAmmo().GetCount());

            GameObject.Destroy(weaponTransform.gameObject);

            character.GetWeapon().Value = null;

            return true;
        }
    }
}