using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class DropWeaponUseCase
    {
        public static bool DropWeapon(IEntity character, GameContext gameContext)
        {
            if (!character.TryGetWeapon(out IReactiveVariable<IEntity> weapon) || weapon.Value.GetWeaponId() == WeaponID.Hand)
                return false;

            Transform weaponTransform = weapon.Value.GetTransform();
            SceneEntity pickUpWeapon = gameContext.GetWeaponCatalog().GetPickUpWeapon(weapon.Value.GetWeaponId());
            pickUpWeapon.GetTransform().SetPositionAndRotation(weaponTransform.position, weaponTransform.rotation);

            if (pickUpWeapon.TryGetAmmo(out var ammo))
            {
                ammo.SpendAll();
                ammo.Add(weapon.Value.GetAmmo().GetCount());
            }

            GameObject.Destroy(weaponTransform.gameObject);

            character.GetWeapon().Value = gameContext.GetWeaponCatalog().GetWeapon(WeaponID.Hand);
            var animator = character.GetAnimator();
            animator.Play("Idle");
            animator.runtimeAnimatorController = character.GetWeapon().Value.GetAnimationController();
            animator.Rebind();
            animator.Update(0f);


            return true;
        }
    }
}