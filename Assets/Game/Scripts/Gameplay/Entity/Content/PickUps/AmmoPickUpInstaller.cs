using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class AmmoPickUpInstaller : MonoEntityInstaller
    {
        [SerializeField] private int _ammoAmount;

        public override void Install(IEntity entity)
        {
            entity.AddTransform(transform);
            entity.AddInteractableTag();
            entity.AddPickUpEvent(new Atomic.Elements.Event());

            entity.AddInteractAction(
                new InlineAction<IEntity>((character =>
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