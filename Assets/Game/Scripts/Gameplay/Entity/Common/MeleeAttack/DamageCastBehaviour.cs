using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public class DamageCastBehaviour : IEntityInit, IEntityDispose, IEntityUpdate
    {
        private const float CAST_TIME = 0.1f;
        private IReactiveVariable<IEntity> _weapon;
        private AnimationEventReceiver _receiver;
        private readonly GameContext _gameContext;

        private bool _castEnabled;
        private float _castTimer;

        public DamageCastBehaviour(GameContext gameContext)
        {
            _gameContext = gameContext;
        }

        public void Init(in IEntity entity)
        {
            _receiver = entity.GetAnimationEventReceiver();
            _receiver.OnEvent += OnDamageCast;
            _weapon = entity.GetWeapon();

            _castTimer = 0f;
        }

        public void Dispose(in IEntity entity)
        {
            _receiver.OnEvent -= OnDamageCast;
        }

        private void OnDamageCast(string eventName)
        {
            if (eventName == "damage_cast_event") _castEnabled = true;
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            if (!_castEnabled) return;

            _castTimer += deltaTime;

            if (_castTimer >= CAST_TIME)
            {
                _castEnabled = false;
                _castTimer = 0f;
            }

            bool success =
                DamageCastUseCase.Cast(_weapon.Value);

            if (success)
            {
                _castEnabled = false;
                _castTimer = 0f;
            }
        }
    }
}