using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Rotates the entity through its Rigidbody, in step with the physics pose.
    ///
    /// The transform-writing <see cref="CharacterRotateBehaviour"/> stays correct for entities
    /// without a Rigidbody and is still used by enemies. It cannot be used on an interpolated
    /// body, though: Rigidbody interpolation works by placing the transform between the last two
    /// physics poses, so a script that writes transform.rotation every Update overwrites that
    /// placement and dirties the transform, which forces a pose sync and makes the rendered motion
    /// strobe. Authoring rotation on the body instead leaves interpolation the sole author of the
    /// transform.
    ///
    /// Rotation axes are frozen by constraints on this body, so the solver never rotates it and
    /// there is no contest over the pose.
    /// </summary>
    public sealed class RigidbodyRotateBehaviour : IEntityInit, IEntityFixedTick
    {
        private Rigidbody _body;
        private IValue<Vector3> _rotateDirection;
        private IValue<float> _rotationSpeed;

        public void Init(IEntity entity)
        {
            _body = entity.GetRiggedBody();
            _rotateDirection = entity.GetRotateDirection();
            _rotationSpeed = entity.GetRotationSpeed();
        }

        public void FixedTick(IEntity entity, float deltaTime)
        {
            Vector3 direction = _rotateDirection.Value;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.0001f) return;

            Quaternion target = Quaternion.LookRotation(direction, Vector3.up);

            // Frame-rate independent smoothing, same shape as RotateUseCase.
            float t = 1f - Mathf.Exp(-_rotationSpeed.Value * deltaTime);
            _body.MoveRotation(Quaternion.Slerp(_body.rotation, target, t));
        }
    }
}
