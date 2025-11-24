using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class PickUpWeaponUseCase
    {
        public static bool PickUpWeapon(IEntity character, IEntity sceneWeapon, GameContext gameContext)
        {
            DropWeaponUseCase.DropWeapon(character, gameContext);
            character.GetTargetInteractable().Value = null;

            SceneEntity characterWeapon = gameContext.GetWeaponCatalog().GetWeapon(sceneWeapon.GetWeaponId());

            if (sceneWeapon.TryGetAmmo(out var pickUpValue) && characterWeapon.TryGetAmmo(out var ammo))
            {
                ammo.SpendAll();
                ammo.Add(pickUpValue.GetCount());
            }

            Transform weaponTransform = characterWeapon.GetTransform();
            character.GetWeapon().Value = characterWeapon;

            Transform weaponRoot = character.GetWeaponRoot();
            weaponTransform.SetPositionAndRotation(weaponRoot.position, weaponRoot.rotation);
            weaponTransform.SetParent(weaponRoot);

            SceneEntity.Destroy(sceneWeapon);

            character.GetHandWeapon().Value.GetGameObject().SetActive(false);
            return true;
        }
    }
}