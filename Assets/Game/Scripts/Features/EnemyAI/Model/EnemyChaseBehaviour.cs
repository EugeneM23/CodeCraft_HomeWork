using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class EnemyChaseBehaviour : IEntityInit, IEntityTick
    {
        private IReactiveVariable<IEntity> _target;
        private IReactiveVariable<Vector3> _moveVector;
        private IReactiveVariable<Vector3> _rotateVector;
        private Transform _enemy;

        public void Init(IEntity entity)
        {
            _target = entity.GetTarget();
            _moveVector = entity.GetMoveDirection();
            _rotateVector = entity.GetRotateDirection();
            _enemy = entity.GetTransform();
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            // Not just a null check: a target that died is disposed but still referenced, and
            // asking a disposed entity for its transform throws. TargetUseCase forgets it.
            if (!TargetUseCase.TryResolve(_target, out Transform targetTransform))
                return;

            // DirectionUseCase.Get returns the raw offset, and MoveUseCase multiplies it by speed
            // and deltaTime: unnormalized, chase speed scaled with distance, so an enemy 20m away
            // moved at 20x its MoveSpeed and tunnelled through geometry while a close one crawled.
            // EnemyPatrolBehaviour normalizes for the same reason.
            Vector3 direction = DirectionUseCase.Get(_enemy, targetTransform);
            direction.y = 0f;

            Vector3 normalized = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.zero;

            _moveVector.Value = normalized;
            _rotateVector.Value = normalized;
        }
    }
}