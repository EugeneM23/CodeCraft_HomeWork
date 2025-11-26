using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class AmmoPickUpInstaller : SceneEntityInstaller
    {
        [SerializeField] private int _ammoAmount;

        public override void Install(IEntity entity)
        {
            entity.AddShowUIAction(new BaseAction<bool>((show) => { entity.GetIsInteract().Value = show; }));
            entity.AddIsInteract(new ReactiveBool(false));

            entity.AddTransform(transform);
            entity.AddInteractableTag();
            entity.AddPickUpEvent(new BaseEvent());

            entity.AddInteractAction(
                new BaseAction<IEntity>((character =>
                {
                    IEntity weapon = character.GetWeapon().Value;
                    if (weapon == null || !weapon.TryGetAmmo(out var ammo)) return;

                    weapon.GetAmmo().Add(_ammoAmount);
                    entity.GetPickUpEvent().Invoke();
                    gameObject.SetActive(false);
                })));
        }
    }
}