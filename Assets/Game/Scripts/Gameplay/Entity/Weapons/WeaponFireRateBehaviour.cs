using Atomic.Entities;
using Modules.Gameplay;

namespace Game
{
    public class WeaponFireRateBehaviour : IEntityUpdate, IEntityInit
    {
        private Cooldown _weaponRpm;

        public void Init(in IEntity entity)
        {
            _weaponRpm = entity.GetWeaponFireRate();
        }

        public void OnUpdate(in IEntity entity, in float deltaTime) => _weaponRpm.Tick(deltaTime);
    }
}