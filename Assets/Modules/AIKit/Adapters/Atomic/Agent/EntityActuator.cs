using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace AIKit.Entities
{
    /// <summary>
    /// AIKit's "virtual gamepad" on an Atomic entity: the presses of a step are buffered and <see cref="Apply"/> writes
    /// them once — <c>Move</c> into the entity's <c>AgentMove</c>, <c>Face</c> (or, without one, the way it walks) into
    /// <c>AgentFace</c>. Whatever walks the entity (the game's walker, whose own variables a game puts under these keys)
    /// moves and turns it. Nothing pressed in a step — it stands. Values are written only when they change: reactive
    /// values tell their watchers (an animator) on every write. No jumps, no ladders, no crouch.
    /// <para>Before <see cref="Bind"/> (the entity is not installed yet) presses go nowhere.</para>
    /// </summary>
    public sealed class EntityActuator : IAgentActuator
    {
        private readonly EntityBody _settings;
        private Transform _transform;
        private IReactiveVariable<Vector3> _moveOut;
        private IReactiveVariable<Vector3> _faceOut;
        private IValue<float> _speed;
        private Vector3 _move;
        private Vector3 _face;

        public EntityActuator(Transform transform, EntityBody settings = null)
        {
            _transform = transform;
            _settings = settings ?? new EntityBody();
        }

        /// <summary>The entity's walk values are taken: presses reach it.</summary>
        public bool IsBound => _moveOut != null;

        /// <summary>Takes the entity's <c>AgentMove</c>, <c>AgentFace</c> and (if it has one) <c>AgentSpeed</c>.</summary>
        public void Bind(IEntity entity)
        {
            _moveOut = entity.GetAgentMove();
            _faceOut = entity.GetAgentFace();
            _speed = entity.TryGetAgentSpeed(out IValue<float> speed) ? speed : null;
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
                    Velocity = Vector3.zero,
                    Up = Vector3.up,
                    IsGrounded = true,
                    Forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward,
                    Radius = _settings.Radius,
                    Height = _settings.Height,
                    MaxSpeed = _speed != null ? _speed.Value : _settings.Speed,
                    StepHeight = _settings.StepHeight
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
            if (face == Vector3.zero && move.sqrMagnitude > 1e-6f)
                face = move.normalized;
            Write(move, face);
        }

        private void Write(Vector3 move, Vector3 face)
        {
            if (_moveOut != null && _moveOut.Value != move)
                _moveOut.Value = move;
            if (_faceOut != null && _faceOut.Value != face)
                _faceOut.Value = face;
        }
    }
}
