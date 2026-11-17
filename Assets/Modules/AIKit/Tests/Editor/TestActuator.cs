using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// A body for tests: no physics, no GameObject. Moves where the AI holds the stick and records what it was told,
    /// so a test can check behavior (it closed in, it faced the target, it jumped) without a scene.
    /// </summary>
    internal sealed class TestActuator : IAgentActuator, IDashActuator, IClipAimActuator
    {
        private Vector3 _move;
        private float _speedScale = 1f;

        public Vector3 Position { get; set; }

        /// <summary>Against gravity; turn it to test a body standing on a wall or a planet.</summary>
        public Vector3 UpDirection { get; set; } = Vector3.up;

        public float Speed { get; set; } = 4f;

        public Vector3 Facing { get; private set; } = Vector3.right;

        public bool IsGrounded { get; set; } = true;

        /// <summary>A body in 3D: it says where it looks (AgentBodyInfo.Forward); off — a side-view body (Facing ±1).</summary>
        public bool LooksIn3D { get; set; }

        public int Jumps { get; private set; }

        public int Drops { get; private set; }

        public int Stops { get; private set; }

        public float LastJumpHold { get; private set; }

        public bool IsCrouching { get; private set; }

        public AgentBodyInfo Body =>
            new()
            {
                Position = Position,
                Velocity = _move.normalized * (Speed * _speedScale),
                Up = UpDirection,
                IsGrounded = IsGrounded,
                Facing = Facing.x >= 0f ? 1 : -1,
                Forward = LooksIn3D ? Facing : Vector3.zero,
                Radius = 0.3f,
                Height = 1.6f,
                MaxSpeed = Speed,
                JumpHeight = 3f,
                JumpReach = 5f,
                CanJump = true,
                CanFly = false
            };

        public void Move(Vector3 direction, float speedScale = 1f)
        {
            _move = direction;
            _speedScale = speedScale;
        }

        public void Face(Vector3 direction)
        {
            if (direction.sqrMagnitude > 1e-6f)
                Facing = direction.normalized;
        }

        public void Jump(float holdTime)
        {
            Jumps++;
            LastJumpHold = holdTime;
        }

        public void Crouch(bool crouch) => IsCrouching = crouch;

        public float Vertical { get; private set; }

        public void SetVertical(float axis) => Vertical = axis;

        /// <summary>A body that rises 3 units in 0.4 s of hold, linearly — enough to see that the hold follows the rise.</summary>
        public float HoldTimeForHeight(float rise) => Mathf.Clamp01(rise / 3f) * 0.4f;

        public void DropThrough() => Drops++;

        public void Stop()
        {
            Stops++;
            _move = Vector3.zero;
        }

        // ---------- the turn of an action's clip (root motion of actions) ----------

        /// <summary>On — the clips of actions turn this body (a turn in place is the clip's, aimed by the node).</summary>
        public bool ActionsByClip { get; set; }

        public Transform ClipTurnTarget { get; private set; }

        public float ClipTurnAngle { get; private set; }

        public int ClipTurnsEnded { get; private set; }

        public void TurnByClipTo(Transform target, float clipAngle)
        {
            ClipTurnTarget = target;
            ClipTurnAngle = clipAngle;
        }

        public Transform ClipKeptOff { get; private set; }

        public void KeepClipOff(Transform target) => ClipKeptOff = target;

        public void EndClipTurn()
        {
            ClipTurnTarget = null;
            ClipKeptOff = null;
            ClipTurnsEnded++;
        }

        // ---------- the dash ----------

        private Vector3 _dashVelocity;
        private float _dashLeft;

        /// <summary>What is in the way: a dash whose displacement it answers true to cannot be made.</summary>
        public System.Func<Vector3, bool> DashBlocked { get; set; }

        public int Dashes { get; private set; }

        public bool IsDashing => _dashLeft > 0f;

        public bool CanDash(Vector3 displacement) => DashBlocked == null || !DashBlocked(displacement);

        public void Dash(Vector3 displacement, float time)
        {
            Dashes++;
            _dashVelocity = displacement / time;
            _dashLeft = time;
        }

        /// <summary>Walls the body cannot pass: where a step would take it, the place it ends at instead (null — none).</summary>
        public System.Func<Vector3, Vector3> Confine { get; set; }

        /// <summary>Once per step, before the body moves (a fake climber advances its climb here).</summary>
        public event System.Action<float> Applying;

        public void Apply(float dt)
        {
            Applying?.Invoke(dt);
            if (_dashLeft > 0f)
            {
                Position += _dashVelocity * Mathf.Min(dt, _dashLeft);
                if (Confine != null)
                    Position = Confine(Position);
                _dashLeft -= dt;
                _move = Vector3.zero;
                return;
            }

            if (_move.sqrMagnitude > 1e-6f)
            {
                Position += _move.normalized * (Speed * _speedScale * dt);
                if (Confine != null)
                    Position = Confine(Position);
                Face(_move);
            }

            _move = Vector3.zero;
        }
    }
}
