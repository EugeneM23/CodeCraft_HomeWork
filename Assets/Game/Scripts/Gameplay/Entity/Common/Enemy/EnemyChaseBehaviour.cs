using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class EnemyChaseBehaviour : IEntityInit, IEntityUpdate
    {
        private IReactiveVariable<IEntity> _target;
        private IReactiveVariable<Vector3> _moveDirection;
        private Transform _enemyTransfrom;

        public void Init(in IEntity entity)
        {
            _target = entity.GetTarget();
            _moveDirection = entity.GetMoveDirection();
            _enemyTransfrom = entity.GetTransform();
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            if (_target.Value != null)
                _moveDirection.Value = DiractionUseCase.Get(_enemyTransfrom, _target.Value.GetTransform());
        }
    }
}