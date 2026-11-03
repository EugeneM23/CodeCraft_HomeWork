using Atomic.Elements;
using Atomic.Entities;
using TMPro;
using UnityEngine;

namespace Game.Gameplay
{
    public class EnemyAttackBehaviour : IEntityInit, IEntityTick
    {
        private const float ATTACK_DISTANCE = 1;
        private IReactiveVariable<IEntity> _target;
        private Transform _enemy;

        public void Init(IEntity entity)
        {
            _target = entity.GetTarget();
            _enemy = entity.GetTransform();
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            if (_target.Value == null) return;

            float distance = Vector3.Distance(_target.Value.GetTransform().position, _enemy.position);

            if (distance < ATTACK_DISTANCE)
            {
                if (entity.GetWeapon().Value.GetFireCondition().Invoke())
                    entity.GetFireAction().Invoke();

            }
        }
    }
}