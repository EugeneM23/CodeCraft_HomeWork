using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Walks the entity along MoveDirection by writing its transform: enemies, which have no body
    /// of their own to drive (see <see cref="RigidbodyMoveBehaviour"/>).
    /// </summary>
    /// <remarks>
    /// Nothing is written on a frame with nothing to move. It used to add the step every frame, a
    /// zero step included, and a transform write marks the transform changed even when the value is
    /// the one it already has: the target dummies stand still with a Rigidbody on their root, and
    /// that write was synced into PhysX as a teleport every frame, so their bodies never slept.
    ///
    /// The speed and the transform are looked up once, and the condition is asked once a frame —
    /// it was asked here and again in MoveUseCase.Move. It is a pure read, so asking it after the
    /// direction, and not at all for a zero direction, changes nothing.
    /// </remarks>
    public class TransformMoveBehaviour : IEntityInit, IEntityTick
    {
        private IExpression<bool> _moveCondition;
        private IValue<Vector3> _moveDirection;
        private IValue<float> _moveSpeed;
        private Transform _transform;

        public void Init(IEntity entity)
        {
            _moveCondition = entity.GetMoveCondition();
            _moveDirection = entity.GetMoveDirection();
            _moveSpeed = entity.GetMoveSpeed();
            _transform = entity.GetTransform();
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            // Same product, in the same order, as MoveUseCase.Move: the position comes out bit for bit.
            Vector3 step = _moveDirection.Value * _moveSpeed.Value * deltaTime;
            if (step.x == 0f && step.y == 0f && step.z == 0f)
                return;

            if (!_moveCondition.Value)
                return;

            _transform.position += step;
        }
    }
}