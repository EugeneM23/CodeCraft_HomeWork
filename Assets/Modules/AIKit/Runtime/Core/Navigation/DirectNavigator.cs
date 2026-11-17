using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// The simplest navigator: holds the stick at the destination. No obstacles, no jumps — enough for flyers, for a
    /// top-down prototype and for tests; the platformer and NavMesh navigators replace it without touching the behavior
    /// that uses it. Two pieces of minimal steering (spec §14.2): the heading turns no faster than MaxTurnSpeed (an
    /// instant flip of direction reads as a glitch), and with a ground probe it does not step into a pit — no ground
    /// PitProbe ahead and the agent stands (Status Blocked).
    /// </summary>
    public sealed class DirectNavigator : INavigator
    {
        private readonly IAgentActuator _actuator;
        private readonly IGroundProbe _ground;
        private Vector3 _destination;
        private Vector3 _heading;

        public DirectNavigator(IAgentActuator actuator, IGroundProbe ground = null)
        {
            _actuator = actuator;
            _ground = ground;
        }

        /// <summary>Degrees per second the heading may turn; 0 — instantly.</summary>
        public float MaxTurnSpeed { get; set; } = 300f;

        /// <summary>How far ahead the ground probe looks for a pit (about one cell).</summary>
        public float PitProbe { get; set; } = 0.5f;

        /// <summary>A drop deeper than this below the feet is a pit.</summary>
        public float PitDepth { get; set; } = 1.5f;

        public NavStatus Status { get; private set; }

        public Vector3 Destination => _destination;

        public Vector3 SteeringTarget => _destination;

        public float RemainingDistance =>
            Status.IsMoving() ? Vector3.Distance(_actuator.Body.Position, _destination) : Mathf.Infinity;

        public NavEdgeKind CurrentEdge => NavEdgeKind.Walk;

        public NavVerdict SetDestination(Vector3 point)
        {
            _destination = point;
            if (_actuator == null)
            {
                Status = NavStatus.Idle;
                return NavVerdict.NoGraph;
            }

            if (!Status.IsMoving())
                Status = NavStatus.Following;
            return NavVerdict.Reachable;
        }

        public void Clear()
        {
            Status = NavStatus.Idle;
            _heading = Vector3.zero;
        }

        public void Step(float dt)
        {
            if (!Status.IsMoving())
                return;

            AgentBodyInfo body = _actuator.Body;
            Vector3 desired = _destination - body.Position;
            if (!body.CanFly)
                desired -= body.Up * Vector3.Dot(desired, body.Up);         // a walker steers along the ground

            _heading = Turn(_heading, desired, body.CanFly ? Vector3.zero : body.Up, dt);
            if (IsPitAhead(body, _heading))
            {
                _actuator.Move(Vector3.zero);
                Status = NavStatus.Blocked;
                return;
            }

            Status = NavStatus.Following;
            _actuator.Move(_heading);
        }

        /// <summary>
        /// The heading toward desired, turned at most MaxTurnSpeed·dt degrees; the length follows desired. A walker
        /// (up given) turns around its up axis, so even a full reversal stays on the ground plane.
        /// </summary>
        private Vector3 Turn(Vector3 heading, Vector3 desired, Vector3 up, float dt)
        {
            if (MaxTurnSpeed <= 0f || heading.sqrMagnitude < 1e-8f || desired.sqrMagnitude < 1e-8f)
                return desired;

            float maxDegrees = MaxTurnSpeed * dt;
            if (up.sqrMagnitude < 1e-8f)
            {
                Vector3 turned = Vector3.RotateTowards(heading.normalized, desired.normalized, maxDegrees * Mathf.Deg2Rad, 0f);
                return turned * desired.magnitude;
            }

            float angle = Vector3.SignedAngle(heading, desired, up);
            if (Mathf.Abs(Mathf.Abs(angle) - 180f) < 1e-3f)
                angle = 180f;                                           // a reversal: pick one way round, always the same
            float step = Mathf.Clamp(angle, -maxDegrees, maxDegrees);
            return Quaternion.AngleAxis(step, up) * heading.normalized * desired.magnitude;
        }

        private bool IsPitAhead(AgentBodyInfo body, Vector3 heading)
        {
            if (_ground == null || body.CanFly || !body.IsGrounded || heading.sqrMagnitude < 1e-8f)
                return false;

            Vector3 ahead = body.Position + heading.normalized * (body.Radius + PitProbe) + body.Up * 0.5f;
            return !_ground.FindGround(ahead, body.Up, 0.5f + PitDepth, out _);
        }
    }
}
