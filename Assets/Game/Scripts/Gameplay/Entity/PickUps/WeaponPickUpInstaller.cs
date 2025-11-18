using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public class WeaponPickUpInstaller : SceneEntityInstaller
    {
        [SerializeField] private WeaponID _id;
        [SerializeField] private GameObject _interactUI;
        [SerializeField] private Ammo _ammo;

        public override void Install(IEntity entity)
        {
            entity.AddAmmo(_ammo);
            entity.AddShowUIAction(new BaseAction<bool>((show) =>
            {
                _interactUI.SetActive(show);
                entity.GetIsInteract().Value = show;
            }));

            entity.AddIsInteract(new ReactiveBool(false));
            entity.AddUITransform(_interactUI.transform);

            entity.AddTransform(transform);
            entity.AddInteractableTag();

            entity.AddInteractAction(
                new BaseAction<IEntity>((character =>
                {
                    Transform weaponRoot = character.GetWeaponRoot();
                    SceneEntity _weapon = GameContext.Instance.GetWeaponCatalog().GetWeapon(_id);

                    SceneEntity weapon = SceneEntity.Create(_weapon, weaponRoot.position, weaponRoot.rotation,
                        weaponRoot);
                    weapon.GetAmmo().Add(_ammo.GetCount());
                    character.GetWeapon().Value = weapon;
                    gameObject.SetActive(false);
                })));

            entity.AddBehaviour<RotationUIBehaviour>();
        }
    }
}