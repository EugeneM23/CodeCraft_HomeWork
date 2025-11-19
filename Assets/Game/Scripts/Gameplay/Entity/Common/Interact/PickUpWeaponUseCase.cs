using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class PickUpWeaponUseCase
    {
        public static bool PickUpWeapon(IEntity character, IEntity sceneWeapon, GameContext gameContext)
        {
            if (character.GetWeapon().Value.GetWeaponId() != WeaponID.Hand)
                DropWeaponUseCase.DropWeapon(character, gameContext);

            SceneEntity characterWeapon = gameContext.GetWeaponCatalog().GetWeapon(sceneWeapon.GetWeaponId());
            characterWeapon.GetAmmo().Add(sceneWeapon.GetAmmo().GetCount());
            Transform weaponTransform = characterWeapon.GetTransform();
            character.GetWeapon().Value = characterWeapon;

            Transform weaponRoot = character.GetWeaponRoot();
            weaponTransform.SetPositionAndRotation(weaponRoot.position, weaponRoot.rotation);
            weaponTransform.SetParent(weaponRoot);

            var animator = character.GetAnimator();
            animator.Play("Idle");
            animator.runtimeAnimatorController = characterWeapon.GetAnimationController();
            animator.Rebind();
            animator.Update(0f);

            SceneEntity.Destroy(sceneWeapon);

            return true;
        }
    }
}