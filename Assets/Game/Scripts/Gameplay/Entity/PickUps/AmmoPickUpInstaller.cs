using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public class AmmoPickUpInstaller : SceneEntityInstaller
    {
        [SerializeField] private int _ammoAmount;
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
                    IEntity weapon = character.GetWeapon().Value;
                    if (weapon == null) return;

                    if (!weapon.TryGetAmmo(out ReactiveInt ammo)) return;

                    ammo.Value += _ammoAmount;
                    gameObject.SetActive(false);
                })));

            entity.AddBehaviour<RotationUIBehaviour>();
        }
    }
}