using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class DropWeaponUseCase
    {
        public static bool DropWeapon(IEntity character, GameContext gameContext)
        {
            IEntity weapon = character.GetWeapon().Value;

            if (weapon.GetWeaponId() == WeaponID.Hand) return false;

            Transform weaponTransform = weapon.GetTransform();

            SpawnPickUpWeapon(gameContext, weapon, weaponTransform);

            GameObject.Destroy(weaponTransform.gameObject);

            character.GetHandWeapon().Value.GetGameObject().SetActive(true);
            character.GetWeapon().Value = character.GetHandWeapon().Value;

            return true;
        }

        private static void SpawnPickUpWeapon(GameContext gameContext, IEntity weapon,
            Transform weaponTransform)
        {
            SceneEntity pickUpWeapon = gameContext.GetWeaponCatalog().GetPickUpWeapon(weapon.GetWeaponId());
            pickUpWeapon.GetTransform().SetPositionAndRotation(weaponTransform.position, Quaternion.identity);

            if (!weapon.TryGetAmmo(out var currentAmmo))
                return;

            if (pickUpWeapon.TryGetAmmo(out var ammo))
            {
                ammo.SpendAll();
                ammo.Add(currentAmmo.GetCount());
            }

            if (pickUpWeapon.TryGetDropEvent(out var @event))
                @event.Invoke();
        }
    }
}