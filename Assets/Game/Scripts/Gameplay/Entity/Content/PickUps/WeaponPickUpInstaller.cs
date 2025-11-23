using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public class WeaponPickUpInstaller : SceneEntityInstaller
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
            entity.AddPickUpEvent(new BaseEvent());
            
            entity.AddInteractAction(new BaseAction<IEntity>(character =>
            {
                entity.GetPickUpEvent().Invoke();
                PickUpWeaponUseCase.PickUpWeapon(character, entity, _gameContext);
            }));
        }
    }
}