using Atomic.Elements;
using Atomic.Entities;
using TMPro;
using UnityEngine;

namespace Game.Gameplay
{
    public class EnemyAttackBehaviour : IEntityInit, IEntityUpdate
    {
        private IReactiveVariable<IEntity> _target;
        private Transform _enemyTransfrom;
        private bool _isAttaking;
        private Animator _animator;

        public void Init(in IEntity entity)
        {
            _animator = entity.GetAnimator();
            _target = entity.GetTarget();
            _enemyTransfrom = entity.GetTransform();
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            if (_target.Value == null) return;

            float distance = Vector3.Distance(_target.Value.GetTransform().position, _enemyTransfrom.position);

            if (distance < 1)
            {
                if (entity.GetWeapon().Value.GetFireCondition().Invoke())
                {
                    entity.GetFireAction().Invoke();
                    entity.GetFireEvent().Invoke();
                }

                _isAttaking = true;
            }
            else
            {
                _isAttaking = false;
            }
        }
    }
}