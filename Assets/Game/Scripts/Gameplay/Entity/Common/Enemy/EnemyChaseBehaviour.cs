using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class EnemyChaseBehaviour : IEntityInit, IEntityUpdate
    {
        private IReactiveVariable<IEntity> _target;
        private IReactiveVariable<Vector3> _moveVector;
        private IReactiveVariable<Vector3> _rotateVector;
        private Transform _enemy;

        public void Init(in IEntity entity)
        {
            _target = entity.GetTarget();
            _moveVector = entity.GetMoveDirection();
            _rotateVector = entity.GetRotateDirection();
            _enemy = entity.GetTransform();
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            if (_target.Value != null)
            {
                _moveVector.Value = DirectionUseCase.Get(_enemy, _target.Value.GetTransform());
                _rotateVector.Value = DirectionUseCase.Get(_enemy, _target.Value.GetTransform());
            }
        }
    }
}