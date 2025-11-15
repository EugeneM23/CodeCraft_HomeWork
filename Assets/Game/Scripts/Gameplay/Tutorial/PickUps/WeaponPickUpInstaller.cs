using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class WeaponPickUpInstaller : SceneEntityInstaller
    {
        [SerializeField] private SceneEntity _weapon;
        [SerializeField] private GameObject _interactUI;

        public override void Install(IEntity entity)
        {
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
                    var weapon = SceneEntity.Create(_weapon, weaponRoot.position, weaponRoot.rotation,
                        weaponRoot);
                    character.SetWeapon(weapon);
                    gameObject.SetActive(false);
                })));

            entity.AddBehaviour<RotationUIBehaviour>();
        }
    }
}