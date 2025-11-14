using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public class AmmoPickUpInstaller : SceneEntityInstaller
    {
        [SerializeField] private int _ammoAmount;
        [SerializeField] private TriggerEventReceiver _receiver;

        public override void Install(IEntity entity)
        {
            entity.AddInteractableTag();

            entity.AddInteractAction(
                new BaseAction<IEntity>((character =>
                {
                    IEntity weapon = character.GetWeapon();
                    if (weapon == null) return;

                    if (!weapon.TryGetAmmo(out ReactiveInt ammo)) return;

                    ammo.Value += _ammoAmount;
                    gameObject.SetActive(false);
                })));
        }
    }
}