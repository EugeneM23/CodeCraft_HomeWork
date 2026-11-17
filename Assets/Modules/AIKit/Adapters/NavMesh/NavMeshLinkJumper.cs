using UnityEngine;
using UnityEngine.AI;

namespace AIKit.NavMeshNavigation
{
    /// <summary>
    /// A body that jumps: its paths may run over the links of the "Jump" area (a NavMeshLink onto a wall, across a
    /// fence) — and when the agent comes to such a link, the body jumps it the way it jumps down a wall (handbook §13.3:
    /// an edge is executed, not driven to): it takes off where it stands (the jump's take-off, as its clip goes), flies to
    /// the link's far end under gravity (Physics.gravity; the arc rises Clearance over the higher end — the link's own
    /// geometry), and stands up where it landed (the landing, as its clip goes); the animator is told the phase (Jump).
    /// A body without it never takes a jump link (the Jump area is closed for it): it goes around. The graph does not
    /// know about jumps: Chase, Move To and the rest just follow the path.
    /// </summary>
    public sealed class NavMeshLinkJumper
    {
        /// <summary>The index of the built-in NavMesh area "Jump": the area of the links only jumpers take.</summary>
        public const int JUMP_AREA = 2;

        /// <summary>Metres the arc rises over the higher end of the link, at the least (a hop over a fence is a hop).</summary>
        public const float MIN_CLEARANCE = 0.4f;

        /// <summary>The arc rises this part of the link's length over its higher end, when that is more than the least.</summary>
        public const float CLEARANCE_PER_METRE = 0.2f;

        private readonly Transform _owner;
        private readonly NavMeshAgent _agent;
        private readonly NavMeshBody _settings;
        private Vector3 _from;
        private Vector3 _to;
        private float _upSpeed;
        private float _flight;
        private float _takeOff;
        private float _landing;
        private float _time;
        private bool _landed;

        public NavMeshLinkJumper(Transform owner, NavMeshAgent agent, NavMeshBody settings)
        {
            _owner = owner;
            _agent = agent;
            _settings = settings;
            _agent.autoTraverseOffMeshLink = false;     // the flight is ours
            _agent.areaMask |= 1 << JUMP_AREA;
        }

        /// <summary>The body is jumping now: taking off, in the air or standing up where it landed.</summary>
        public bool IsJumping { get; private set; }

        /// <summary>How many jumps the body has made (debugging and tests).</summary>
        public int Jumps { get; private set; }

        /// <summary>Seconds the last (or current) jump is in the air.</summary>
        public float FlightTime => _flight;

        /// <summary>Where in the jump the body is (the animator shows take-off, the air, the landing).</summary>
        public JumpPhase Phase
        {
            get
            {
                if (!IsJumping)
                    return JumpPhase.None;
                if (_time < _takeOff)
                    return JumpPhase.TakeOff;
                return _time < _takeOff + _flight ? JumpPhase.InAir : JumpPhase.Landing;
            }
        }

        /// <summary>Metres the arc of a jump this long rises over the higher end of the link.</summary>
        public static float ClearanceFor(Vector3 from, Vector3 to)
        {
            Vector3 flat = to - from;
            flat.y = 0f;
            return Mathf.Max(MIN_CLEARANCE, CLEARANCE_PER_METRE * flat.magnitude);
        }

        public void FrameTick(float dt)
        {
            if (_agent == null || !_agent.isActiveAndEnabled)
                return;

            if (!IsJumping)
            {
                if (!_agent.isOnOffMeshLink)
                    return;

                if (NavMeshLinkClimber.AreaOfCurrentLink(_agent) == NavMeshLinkClimber.CLIMB_AREA)
                    return;                             // a wall to climb, not a gap to jump: the climber takes it

                Begin();
            }

            _time += dt;
            if (_time < _takeOff)
            {
                _owner.position = _from;                // taking off: the feet are still on the ground
                return;
            }

            float flying = _time - _takeOff;
            if (flying < _flight)
            {
                Vector3 over = Vector3.Lerp(_from, _to, flying / _flight);
                over.y = Ballistics.HeightAt(_from.y, _upSpeed, NavMeshBody.Gravity, flying);
                _owner.position = over;
                return;
            }

            if (!_landed)
                Touch();                                // feet down: the agent is at the far end, standing up
            if (_time < _takeOff + _flight + _landing)
                return;

            End();
        }

        // On a jump link: from where the body stands to the link's end, at the height the body stands at
        private void Begin()
        {
            _from = _owner.position;
            _to = _agent.currentOffMeshLinkData.endPos + Vector3.up * _agent.baseOffset;
            Ballistics.Arc(_from, _to, ClearanceFor(_from, _to), NavMeshBody.Gravity, out _upSpeed, out _flight);
            _takeOff = _landing = 0f;
            if (_settings.Motion.TryJump(out JumpMotion jump))
            {
                _takeOff = Mathf.Max(0f, jump.TakeOff);
                _landing = Mathf.Max(0f, jump.Landing);
            }

            _time = 0f;
            _landed = false;
            IsJumping = true;
            Jumps++;
        }

        /// <summary>
        /// The character is switched off: a jump under way ends at once where it led, so that switching the character on
        /// again does not resume a stale arc.
        /// </summary>
        public void Abort()
        {
            if (!IsJumping)
                return;

            if (!_landed)
            {
                _owner.position = _to;
                if (_agent != null && _agent.isActiveAndEnabled)
                    Touch();
            }

            End();
        }

        // The feet touch the far end: the link is done (the agent's path goes on from there), the body stands meanwhile
        private void Touch()
        {
            _landed = true;
            _owner.position = _to;
            if (_agent.isOnOffMeshLink)
                _agent.CompleteOffMeshLink();
            else
                _agent.Warp(_to);                       // the path was dropped mid-air: land where the jump led
            if (_agent.isOnNavMesh)
                _agent.isStopped = true;                // it stands up where it landed: the landing clip plays there
        }

        private void End()
        {
            IsJumping = false;
            if (_agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh)
                _agent.isStopped = false;
        }
    }
}
