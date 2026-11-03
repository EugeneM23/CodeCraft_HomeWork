using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
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

            if (FindTargetUseCase.TryGetTarget(_weapon.Value, out IEntity target))
            {
                DamageUseCase.TakeDamage(target, new TakeDamageArgs(_weapon.Value));
                _castTimer = 0f;
            }
        }
    }
}