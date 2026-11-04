using Atomic.Elements;
using Atomic.Entities;
using TMPro;
using UnityEngine;

namespace Game
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
            // Same guard as the chase: a dead target is disposed but still referenced, and asking
            // it for a transform throws.
            if (!TargetUseCase.TryResolve(_target, out Transform targetTransform)) return;

            float distance = Vector3.Distance(targetTransform.position, _enemy.position);

            if (distance < ATTACK_DISTANCE)
            {
                if (entity.GetWeapon().Value.GetFireCondition().Invoke())
                    entity.GetFireAction().Invoke();

            }
        }
    }
}