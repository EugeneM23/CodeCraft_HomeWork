using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class EnemyAttackBehaviour : IEntityInit, IEntityUpdate
    {
        private IReactiveVariable<IEntity> _target;
        private Transform _enemyTransfrom;
        private Animator _animator;

        public void Init(in IEntity entity)
        {
            _target = entity.GetTarget();
            _enemyTransfrom = entity.GetTransform();
            _animator = entity.GetAnimator();
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            if (_target.Value == null) return;

            float distance = Vector3.Distance(_target.Value.GetTransform().position, _enemyTransfrom.position);

            if (distance < 1)
            {
                _animator.SetTrigger("Attack");
            }
        }
    }
}