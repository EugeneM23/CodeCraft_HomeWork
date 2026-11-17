using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Rotates the entity through its Rigidbody, in step with the physics pose.
    ///
    /// The transform-writing <see cref="TransformRotateBehaviour"/> stays correct for entities
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
        /// <summary>
        /// A turn whose every quaternion component moves less than this, about a ten-thousandth of
        /// a degree, is not made. MoveRotation wakes the body: a character standing still, and the
        /// unridden mount facing where it was left, were kept awake every step by turns that rounding
        /// alone kept from ever reaching nothing.
        /// </summary>
        private const float NO_TURN = 1e-6f;

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
            Quaternion current = _body.rotation;
            Quaternion turned = Quaternion.Slerp(current, target, t);

            if (Mathf.Abs(turned.x - current.x) <= NO_TURN && Mathf.Abs(turned.y - current.y) <= NO_TURN &&
                Mathf.Abs(turned.z - current.z) <= NO_TURN && Mathf.Abs(turned.w - current.w) <= NO_TURN)
                return;

            _body.MoveRotation(turned);
        }
    }
}
