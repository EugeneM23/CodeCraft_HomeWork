using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class WeaponPickUpInstaller : MonoEntityInstaller
    {
        [SerializeField] private WeaponID _id;
        [SerializeField] private Ammo _ammo;

        private GameContext _gameContext;

        public override void Install(IEntity entity)
        {
            _gameContext = GameContext.Instance;

            entity.AddWeaponId(_id);
            entity.AddAmmo(_ammo);

            entity.AddTransform(transform);
            entity.AddInteractableTag();
            entity.AddPickUpEvent(new Atomic.Elements.Event());
            entity.AddDropEvent(new Atomic.Elements.Event());

            entity.AddInteractAction(new InlineAction<IEntity>(character =>
            {
                entity.GetPickUpEvent().Invoke();
                PickUpWeaponUseCase.PickUpWeapon(character, entity, _gameContext);
            }));
        }
    }
}