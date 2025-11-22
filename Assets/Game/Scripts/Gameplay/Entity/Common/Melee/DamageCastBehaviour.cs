using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class DamageCastBehaviour : IEntityInit, IEntityUpdate
    {
        private const float CAST_TIME = 0.3f;
        private IValue<int> _damage;
        private IValue<float> _damageRadius;
        private LayerMask _damageLayer;

        private IReactiveVariable<bool> _castEnabled;
        private float _castTimer;
        private bool _isCasting;

        public void Init(in IEntity entity)
        {
            _castEnabled = entity.GetDamageCastEnabled();
            _damage = entity.GetDamage();
            _damageRadius = entity.GetDamageRadius();
            _damageLayer = entity.GetDamageLayer();

            _castTimer = 0f;
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            if (!_castEnabled.Value) return;

            _castTimer += deltaTime;

            if (_castTimer >= CAST_TIME)
            {
                _castEnabled.Value = false;
                _castTimer = 0f;
            }

            bool success =
                DamageCastUseCase.Cast(entity.GetTransform(), _damageRadius.Value, _damage.Value, _damageLayer);

            if (success)
            {
                _castEnabled.Value = false;
                _castTimer = 0f;
            }
        }
    }
}