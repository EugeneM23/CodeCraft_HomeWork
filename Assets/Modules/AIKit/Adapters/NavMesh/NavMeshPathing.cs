using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AIKit.NavMeshNavigation
{
    /// <summary>
    /// Navigation over Unity's NavMesh: the destination is sampled onto the mesh, the agent walks the path with its
    /// own steering and local avoidance, and the graph only asks "am I there yet?". Off-mesh links are reported as
    /// Jump edges, so behavior written for the platformer graph reads the same here. Reads its numbers from the
    /// NavMeshBody live.
    /// </summary>
    public sealed class NavMeshPathing : INavigator, INavigationPace, IAgentNumbers, IClimbingNavigator, IAgentBound
    {
        /// <summary>The body's full speed on the blackboard: a node of the graph may read it and change it.</summary>
        public const string SPEED = "Speed";

        private readonly Transform _owner;
        private readonly NavMeshAgent _agent;
        private readonly NavMeshBody _settings;
        private float _fullSpeed = -1f;
        private float _pace = 1f;
        private Vector3 _destination;

        // Where the path the agent walks now leads: the repath threshold is measured from here, not from the last
        // asked point — a target that creeps away in small steps still adds up to a new path
        private Vector3 _pathDestination;
        private bool _hasDestination;

        // A destination asked while the body was on a link (in the air, on a wall, standing up from a landing): the body
        // is on its way to it, and the way on is planned the moment the link is done
        private bool _afterLink;

        // The last destination asked could not be put on the mesh: no way there (not the path walked before)
        private bool _unsampled;

        // The agent whose body this is (its own clock times the cached climbing answer — invariant 3: no engine time in what
        // a decision asks); bound by the character; null — not bound yet: nothing is cached
        private AgentContext _context;

        /// <summary>The character's context: "on foot or by climbing" is kept by the agent's own clock.</summary>
        public void Bind(AgentContext context) => _context = context;

        public NavMeshPathing(Transform owner, NavMeshAgent agent, NavMeshBody settings)
        {
            _owner = owner;
            _agent = agent;
            _settings = settings;
        }

        /// <summary>
        /// A part of the agent's speed the path is walked at (1 — its full speed, as set on the NavMeshAgent). The
        /// full speed is read when the pace is first changed.
        /// </summary>
        public float Pace
        {
            get => _pace;
            set
            {
                float pace = Mathf.Clamp(value, 0.05f, 1f);
                if (_agent == null || Mathf.Approximately(pace, _pace))
                    return;

                if (_fullSpeed < 0f)
                    _fullSpeed = _agent.speed;

                _pace = pace;
                _agent.speed = _fullSpeed * pace;
                _settings.ApplyAcceleration(_agent);        // a slower pace starts and stops over the same distance
            }
        }

        void IAgentNumbers.NamesOf(List<string> names) => names.Add(SPEED);

        // The full speed, whatever part of it the path is walked at now (Pace)
        float IAgentNumbers.GetNumber(int index) =>
            _fullSpeed >= 0f ? _fullSpeed : _agent != null ? _agent.speed : 0f;

        void IAgentNumbers.SetNumber(int index, float value)
        {
            if (_agent == null)
                return;

            _fullSpeed = Mathf.Max(0f, value);
            _agent.speed = _fullSpeed * _pace;
            _settings.ApplyAcceleration(_agent);
        }

        public NavMeshAgent Agent => _agent;

        /// <summary>
        /// Planning while the agent calculates; a path that is not complete (partial or invalid) is Unreachable — the
        /// agent would walk to the nearest point and stand there, and the graph should pick another plan instead.
        /// </summary>
        public NavStatus Status
        {
            get
            {
                if (_unsampled)
                    return NavStatus.Unreachable;       // asked a point off the mesh: no way there

                if (!_hasDestination)
                    return NavStatus.Idle;

                if (_afterLink)
                    return NavStatus.Following;         // over a link now: on its way, the way on planned once it is done

                if (!IsUsable)
                    return NavStatus.Idle;

                if (_agent.pathPending)
                    return NavStatus.Planning;

                return _agent.pathStatus == NavMeshPathStatus.PathComplete
                    ? NavStatus.Following
                    : NavStatus.Unreachable;
            }
        }

        public Vector3 Destination => _destination;

        public Vector3 SteeringTarget =>
            IsUsable && _agent.hasPath ? _agent.steeringTarget : _owner.position;

        public float RemainingDistance
        {
            get
            {
                if (_afterLink && _hasDestination)
                    return Vector3.Distance(_owner.position, _destination);

                if (!IsUsable || !_hasDestination)
                    return Mathf.Infinity;

                if (_agent.pathPending)
                    return Vector3.Distance(_owner.position, _destination);

                return _agent.pathStatus == NavMeshPathStatus.PathInvalid
                    ? Mathf.Infinity
                    : _agent.remainingDistance;
            }
        }

        public NavEdgeKind CurrentEdge =>
            ClimbDirection != 0 ? (_settings.Climber.IsDropping ? NavEdgeKind.Fall : NavEdgeKind.Ladder)
            : IsUsable && _agent.isOnOffMeshLink ? NavEdgeKind.Jump
            : NavEdgeKind.Walk;

        // ---------- climbing (the body's Climber; none — this body never climbs) ----------

        private NavMeshPath _footPath;
        private NavMeshPath _climbPath;
        private Vector3 _askedFrom;
        private Vector3 _askedTo;
        private float _askedAt = float.NegativeInfinity;
        private ClimbWay _answer;

        /// <summary>How near the asked point and the body must stay for the last answer to "on foot or by climbing?" to hold.</summary>
        private const float SAME_QUESTION = 0.75f;

        public bool CanClimb => _settings.Climber != null;

        public bool ClimbAllowed
        {
            get => _settings.Climber is { Allowed: true };
            set
            {
                NavMeshLinkClimber climber = _settings.Climber;
                if (climber == null || climber.Allowed == value)
                    return;

                climber.Allowed = value;            // shut: a body waiting at the foot of a wall steps off its link
                // The path walked now was planned with the walls open or shut: it is planned anew by the next request
                // (a body on a wall goes on: the link it is on is not taken away)
                if (IsUsable && !_agent.isOnOffMeshLink && (_agent.hasPath || _agent.pathPending))
                    _agent.ResetPath();
                _hasDestination = false;
                _afterLink = false;
            }
        }

        public int ClimbDirection => _settings.Climber?.Direction ?? 0;

        public int Climbs => _settings.Climber?.Climbs ?? 0;

        /// <summary>
        /// Two paths to the point, planned at once (NavMesh.CalculatePath): with the walls shut and with them open. The
        /// answer is kept for half a second (NavMeshBody.CLIMB_CHECK_INTERVAL) of the agent's own time (its context's
        /// clock — never UnityEngine.Time; a pathing no character is bound to asks every time) while neither the body
        /// nor the point has moved much — a decision that asks every think does not plan two paths every think. Asked
        /// from where the body stands — from a link's far end only while it is carried over the link (in the air): a body
        /// waiting at the foot of a wall is not over it yet.
        /// </summary>
        public ClimbWay WayTo(Vector3 point)
        {
            if (!IsUsable || _settings.Climber == null)
                return ClimbWay.NoWay;

            Vector3 from = _agent.isOnOffMeshLink && _settings.IsTraversing
                ? _agent.currentOffMeshLinkData.endPos
                : _owner.position;
            bool timed = _context != null;
            float now = timed ? _context.LocalTime : 0f;
            if (timed && now - _askedAt < NavMeshBody.CLIMB_CHECK_INTERVAL &&
                (from - _askedFrom).sqrMagnitude < SAME_QUESTION * SAME_QUESTION &&
                (point - _askedTo).sqrMagnitude < SAME_QUESTION * SAME_QUESTION)
                return _answer;

            _askedAt = now;
            _askedFrom = from;
            _askedTo = point;
            _answer = Ask(from, point);
            return _answer;
        }

        private ClimbWay Ask(Vector3 from, Vector3 point)
        {
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, _settings.SampleRadius, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(from, out NavMeshHit start, 1f, NavMesh.AllAreas))
                return ClimbWay.NoWay;

            from = start.position;

            _footPath ??= new NavMeshPath();
            _climbPath ??= new NavMeshPath();
            int foot = _agent.areaMask & ~(1 << NavMeshLinkClimber.CLIMB_AREA);
            if (NavMesh.CalculatePath(from, hit.position, foot, _footPath) &&
                _footPath.status == NavMeshPathStatus.PathComplete)
                return ClimbWay.OnFoot;

            int climbing = foot | 1 << NavMeshLinkClimber.CLIMB_AREA;
            return NavMesh.CalculatePath(from, hit.position, climbing, _climbPath) &&
                   _climbPath.status == NavMeshPathStatus.PathComplete
                ? ClimbWay.OnlyByClimbing
                : ClimbWay.NoWay;
        }

        /// <summary>
        /// The path is planned asynchronously: an accepted request is Reachable (Status says Planning, then the truth);
        /// a point that does not sample onto the mesh is Unreachable — and the way walked before is dropped with it (Status
        /// says Unreachable, never the old path); an agent off the mesh is OffGraph. Asked while the body is on a link (in
        /// the air, on a wall, standing up from a landing) it is Reachable: the body is on its way (Status Following) and
        /// the way on is planned the moment the link is done.
        /// </summary>
        public NavVerdict SetDestination(Vector3 point)
        {
            if (_agent == null || !_agent.isActiveAndEnabled)
                return NavVerdict.NoGraph;

            if (!_agent.isOnNavMesh)
                return NavVerdict.OffGraph;

            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, _settings.SampleRadius, NavMesh.AllAreas))
            {
                _destination = point;
                _hasDestination = false;
                _afterLink = false;
                _unsampled = true;
                if (IsUsable && !_agent.isOnOffMeshLink && (_agent.hasPath || _agent.pathPending))
                    _agent.ResetPath();             // not the old point either: the body brakes where it is
                return NavVerdict.Unreachable;
            }

            _unsampled = false;
            point = hit.position;

            float repath = _settings.RepathDistance;
            bool near = (point - _pathDestination).sqrMagnitude < repath * repath;
            if (_agent.isOnOffMeshLink || _settings.IsTraversing)
            {
                // On a link the path is not touched (a new one throws the agent to the link's far end): the way there is
                // planned once the link is done. Landing from a jump the agent is back (the others walk round it), but
                // the body stands up where it landed — it walks on once the landing is over
                _destination = point;
                if (!_hasDestination || !near)
                    _afterLink = true;
                _hasDestination = true;
                return NavVerdict.Reachable;
            }

            if (!_afterLink && _hasDestination && (_agent.hasPath || _agent.pathPending) && near)
            {
                _destination = point;
                return Status == NavStatus.Unreachable ? NavVerdict.Unreachable : NavVerdict.Reachable;   // barely moved: keep walking the path we already have
            }

            return Plan(point);
        }

        // A path to the point, planned now
        private NavVerdict Plan(Vector3 point)
        {
            _afterLink = false;
            _destination = point;
            _pathDestination = point;
            _agent.isStopped = false;
            _hasDestination = _agent.SetDestination(point);
            return _hasDestination ? NavVerdict.Reachable : NavVerdict.Unreachable;
        }

        /// <summary>
        /// The body is off its link (once a frame, after the jumper and the climber): a destination asked while it was
        /// on it is planned now — whether or not anybody asks again.
        /// </summary>
        internal void AfterLinks()
        {
            if (!_afterLink || !IsUsable || _agent.isOnOffMeshLink || _settings.IsTraversing)
                return;

            Plan(_destination);
        }

        public void Clear()
        {
            _hasDestination = false;
            _afterLink = false;
            _unsampled = false;
            // A path still being planned is dropped too: asked and left in one step, it was walked once it came (on a
            // link: see SetDestination)
            if (IsUsable && (_agent.hasPath || _agent.pathPending) && !_agent.isOnOffMeshLink)
                _agent.ResetPath();
        }

        /// <summary>The agent walks the path itself; a step only keeps the body looking along it.</summary>
        public void Step(float dt) => AfterLinks();

        private bool IsUsable => _agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh;
    }
}
