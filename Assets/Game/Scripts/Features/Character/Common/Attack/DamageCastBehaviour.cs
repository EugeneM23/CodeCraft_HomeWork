using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class DamageCastBehaviour : IEntityInit, IEntityDispose, IEntityTick
    {
        private const float CAST_TIME = 0.1f;
        private IReactiveVariable<IEntity> _weapon;
        private AnimationEventReceiver _receiver;

        private float _castTimer;

        public void Init(IEntity entity)
        {
            _receiver = entity.GetAnimationEventReceiver();
            _receiver.OnEvent += OnDamageCast;
            _weapon = entity.GetWeapon();

            _castTimer = 0f;
        }

        public void Dispose(IEntity entity) => _receiver.OnEvent -= OnDamageCast;

        private void OnDamageCast(string eventName)
        {
            if (eventName == "damage_cast_event")
                _castTimer = CAST_TIME;
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            if (_castTimer <= 0f) return;

            _castTimer -= deltaTime;

            // This behaviour sits on every character, not just the melee ones, and the weapon can
            // be swapped inside the 0.1s cast window. FindTargetUseCase reads DamageRadius and
            // DamageLayer unconditionally, and only a melee weapon carries them: without this gate
            // swinging and then switching to a firearm threw KeyNotFoundException from the tick.
            IEntity weapon = _weapon.Value;
            if (weapon == null || !weapon.HasDamageRadius() || !weapon.HasDamageLayer())
            {
                _castTimer = 0f;
                return;
            }

            if (FindTargetUseCase.TryGetTarget(weapon, out IEntity target))
            {
                DamageUseCase.TakeDamage(target, new TakeDamageArgs(weapon));
                _castTimer = 0f;
            }
        }
    }
}