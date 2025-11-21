using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class PickUpWeaponUseCase
    {
        public static bool PickUpWeapon(IEntity character, IEntity sceneWeapon, GameContext gameContext)
        {
            WeaponID id = character.GetWeapon().Value.GetWeaponId();

            if (id == WeaponID.Hand)
                GameObject.Destroy(character.GetWeapon().Value.GetTransform().gameObject);

            if (id != WeaponID.Hand)
                DropWeaponUseCase.DropWeapon(character, gameContext);

            SceneEntity characterWeapon = gameContext.GetWeaponCatalog().GetWeapon(sceneWeapon.GetWeaponId());

            if (sceneWeapon.TryGetAmmo(out var ammo) && characterWeapon.TryGetAmmo(out var ammo2))
                characterWeapon.GetAmmo().Add(ammo.GetCount());

            Transform weaponTransform = characterWeapon.GetTransform();
            character.GetWeapon().Value = characterWeapon;

            Transform weaponRoot = character.GetWeaponRoot();
            weaponTransform.SetPositionAndRotation(weaponRoot.position, weaponRoot.rotation);
            weaponTransform.SetParent(weaponRoot);


            SceneEntity.Destroy(sceneWeapon);

            return true;
        }
    }
}