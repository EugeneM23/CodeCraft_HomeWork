using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// The "gamepad" of a body without physics: moves the owner's transform where the AI holds the stick. Reads its
    /// numbers from the SimpleBody live, so a change in the inspector or on the blackboard takes effect at once.
    /// </summary>
    public sealed class TransformMover : IAgentActuator
    {
        private readonly Transform _owner;
        private readonly SimpleBody _settings;
        private readonly AxisWriteDetector _axes = new();
        private Vector3 _move;
        private Vector3 _facing = Vector3.right;
        private float _speedScale = 1f;
        private Vector3 _went;              // how fast it went in its last step (the step clears the stick)

        public TransformMover(Transform owner, SimpleBody settings)
        {
            _owner = owner;
            _settings = settings;
        }

        // The stick pressed now — or, between steps (another character reads it after this one's step), the speed it went
        private Vector3 Velocity =>
            _move.sqrMagnitude > 1e-6f ? _move.normalized * (_settings.Speed * _speedScale) : _went;

        public AgentBodyInfo Body =>
            new()
            {
                Position = _owner.position,
                Velocity = Velocity,
                Up = Vector3.up,
                IsGrounded = true,
                Facing = _facing.x >= 0f ? 1 : -1,
                Forward = _settings.PlanarXY ? Vector3.zero : _facing,     // on the ground (XZ) it looks where it turned
                Radius = _settings.Radius,
                Height = _settings.Height,
                MaxSpeed = _settings.Speed,
                JumpHeight = 0f,
                JumpReach = 0f,
                CanJump = false,
                CanFly = true
            };

        public void Move(Vector3 direction, float speedScale = 1f)
        {
            _axes.Write(AxisWriteDetector.Axis.Move, this);
            _move = Flatten(direction);
            _speedScale = Mathf.Max(0f, speedScale);
        }

        public void Face(Vector3 direction)
        {
            _axes.Write(AxisWriteDetector.Axis.Face, this);
            Vector3 flat = Flatten(direction);
            if (flat.sqrMagnitude > 1e-6f)
                _facing = flat.normalized;
        }

        public void SetVertical(float axis)
        {
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
            _speedScale = 1f;
        }

        public void Apply(float dt)
        {
            _axes.EndStep();
            if (_move.sqrMagnitude <= 1e-6f)
            {
                _went = Vector3.zero;              // nothing pressed: it stood this step
                return;
            }

            _went = _move.normalized * (_settings.Speed * _speedScale);
            Vector3 step = _went * dt;
            _owner.position += step;
            Face(_move);
            _move = Vector3.zero;                  // the stick is pressed anew every step
        }

        private Vector3 Flatten(Vector3 direction) =>
            _settings.PlanarXY ? new Vector3(direction.x, direction.y, 0f) : direction;
    }
}
