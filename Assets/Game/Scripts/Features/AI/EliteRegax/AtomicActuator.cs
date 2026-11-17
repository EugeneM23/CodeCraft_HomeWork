using AIKit;
using Atomic.Elements;
using Atomic.Entities;
using Game.Horde;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// AIKit's "virtual gamepad" on an Atomic walker (the values <c>WalkingUseCase</c> gives it): the presses of a step
    /// are buffered and <see cref="Apply"/> writes them once — <c>Move</c> into <c>MoveDirection</c>, <c>Face</c> (or,
    /// without one, the way it walks) into <c>RotateDirection</c>; the entity's own behaviours then move and turn it at
    /// its <c>MoveSpeed</c> and <c>RotationSpeed</c>. Nothing pressed in a step — it stands. No jumps, no ladders. A leap
    /// goes to the body's <see cref="EliteMotor"/>, which holds the stick at zero while in the air.
    /// </summary>
    public sealed class AtomicActuator : IAgentActuator, ILeapActuator
    {
        public const float BODY_RADIUS = 0.5f;
        public const float BODY_HEIGHT = 4.28f;

        private Transform _transform;
        private readonly EliteMotor _motor;
        private IReactiveVariable<Vector3> _moveDirection;
        private IReactiveVariable<Vector3> _rotateDirection;
        private IReactiveVariable<float> _moveSpeed;
        private Vector3 _move;
        private Vector3 _face;

        public AtomicActuator(Transform transform, EliteMotor motor = null)
        {
            _transform = transform;
            _motor = motor;
        }

        public EliteMotor Motor => _motor;

        public bool IsBound => _moveDirection != null;

        /// <summary>Takes the walker's values (call once the entity is installed).</summary>
        public void Bind(IEntity entity)
        {
            _moveDirection = entity.GetMoveDirection();
            _rotateDirection = entity.GetRotateDirection();
            _moveSpeed = entity.TryGetMoveSpeed(out IReactiveVariable<float> speed) ? speed : null;
            if (entity.TryGetTransform(out Transform transform) && transform != null)
                _transform = transform;
        }

        public AgentBodyInfo Body
        {
            get
            {
                Vector3 forward = _transform != null ? _transform.forward : Vector3.forward;
                forward.y = 0f;
                return new AgentBodyInfo
                {
                    Position = _transform != null ? _transform.position : Vector3.zero,
                    Velocity = _motor != null ? _motor.Velocity : Vector3.zero,
                    Up = Vector3.up,
                    IsGrounded = _motor == null || !_motor.IsLeaping,
                    Forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward,
                    Radius = BODY_RADIUS,
                    Height = BODY_HEIGHT,
                    MaxSpeed = _moveSpeed != null ? _moveSpeed.Value : 0f,
                    StepHeight = HordeNavGridSettings.DEFAULT_MAX_STEP
                };
            }
        }

        public void Move(Vector3 direction, float speedScale = 1f)
        {
            direction.y = 0f;
            float length = direction.magnitude;
            if (length > 1f)
                direction /= length;
            _move = direction * Mathf.Clamp01(speedScale);
        }

        public void SetVertical(float axis)
        {
        }

        public void Face(Vector3 direction)
        {
            direction.y = 0f;
            _face = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.zero;
        }

        public void Jump(float holdTime)
        {
        }

        public float HoldTimeForHeight(float rise) => 0f;

        public void Crouch(bool crouch)
        {
        }

        public void DropThrough()
        {
        }

        public void Stop()
        {
            _move = Vector3.zero;
            _face = Vector3.zero;
            Write(Vector3.zero, Vector3.zero);
        }

        public void Apply(float dt)
        {
            Vector3 move = _move;
            Vector3 face = _face;
            _move = Vector3.zero;
            _face = Vector3.zero;
            if (_motor != null && _motor.IsLeaping)
            {
                Write(Vector3.zero, Vector3.zero);
                return;
            }

            if (face == Vector3.zero && move.sqrMagnitude > 1e-6f)
                face = move.normalized;
            Write(move, face);
        }

        public bool IsLeaping => _motor != null && _motor.IsLeaping;

        public bool TryLeapAway(Vector3 from, float minDistance, float maxDistance, float duration)
        {
            if (_motor == null || !_motor.TryLeapAway(from, minDistance, maxDistance, duration))
                return false;
            Stop();
            return true;
        }

        // Reactive values tell their watchers (the animator) on every write: written only when they change
        private void Write(Vector3 move, Vector3 face)
        {
            if (_moveDirection != null && _moveDirection.Value != move)
                _moveDirection.Value = move;
            if (_rotateDirection != null && _rotateDirection.Value != face)
                _rotateDirection.Value = face;
        }
    }
}
