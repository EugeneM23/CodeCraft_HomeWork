using AIKit;
using Game.Horde;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// AIKit's navigator over the level's voxel height grid (<see cref="HordeNavGrid"/>): paths come from the level's A*
    /// (<see cref="LevelPathService"/>, a frame after the ask), are followed waypoint by waypoint, and the stick goes to
    /// the actuator (<c>Move</c>). Honest answers:
    /// <list type="bullet">
    /// <item>no grid — <see cref="NavVerdict.NoGraph"/>; the agent on no ground (off the map, over a hole) —
    /// <see cref="NavVerdict.OffGraph"/>, and the search failed the same way;</item>
    /// <item>a path to the goal — <see cref="NavVerdict.Reachable"/>, <see cref="NavStatus.Following"/>;</item>
    /// <item>the goal cut off (a roof, an island) — <see cref="NavVerdict.Unreachable"/>: the path leads to the nearest
    /// reachable point, and once it is walked the status turns <see cref="NavStatus.Unreachable"/>.</item>
    /// </list>
    /// Replans when the goal moved more than <see cref="GOAL_MOVED"/>, every <see cref="REPLAN_INTERVAL"/> while it has a
    /// goal, and at once when the grid changed under the rest of the path (a step along it is no longer walkable).
    /// No managed allocations after construction (the waypoints are kept in a fixed buffer).
    /// </summary>
    public sealed class VoxelGridNavigator : INavigator, IPathClient
    {
        /// <summary>Most waypoints kept from a path; a longer one is walked to here and planned again.</summary>
        public const int MAX_WAYPOINTS = 128;

        /// <summary>Metres from a waypoint at which it counts as passed.</summary>
        public const float ARRIVE = 0.35f;

        /// <summary>Seconds between plans while a goal is held (a moving target, a grid that changed elsewhere).</summary>
        public const float REPLAN_INTERVAL = 1f;

        /// <summary>Metres the goal moves before a new plan is asked for.</summary>
        public const float GOAL_MOVED = 1f;

        /// <summary>Seconds at least between two plans asked for by a moving goal.</summary>
        public const float MIN_REPLAN_GAP = 0.25f;

        /// <summary>A path whose end is this close to the goal (flat) reaches it, whatever the search said.</summary>
        public const float CLOSE_ENOUGH = 1f;

        private readonly Vector3[] _points = new Vector3[MAX_WAYPOINTS];
        private IAgentActuator _actuator;
        private LevelNav _nav;
        private int _count;
        private int _index;
        private Vector3 _destination;
        private Vector3 _plannedGoal;
        private bool _hasDestination;
        private bool _pending;
        private bool _partial;
        private NavStatus _status = NavStatus.Idle;
        private NavVerdict _verdict = NavVerdict.Reachable;
        private float _sincePlan;
        private int _pathVersion;
        private int _plans;

        public VoxelGridNavigator(IAgentActuator actuator, LevelNav nav = null)
        {
            _actuator = actuator;
            _nav = nav;
        }

        public void Bind(LevelNav nav) => _nav = nav;

        public NavStatus Status => _status;

        public Vector3 Destination => _destination;

        public Vector3 SteeringTarget => _count > 0 ? _points[Mathf.Min(_index, _count - 1)] : _destination;

        public NavEdgeKind CurrentEdge => NavEdgeKind.Walk;

        /// <summary>A plan was asked for and has not come yet.</summary>
        public bool IsPlanning => _pending;

        /// <summary>The path in hand leads only as near the goal as the ground allows.</summary>
        public bool IsPartial => _partial;

        /// <summary>Waypoints of the path in hand (the first is where it started).</summary>
        public int WaypointCount => _count;

        /// <summary>How many plans this navigator has asked for (tests, the debugger).</summary>
        public int Plans => _plans;

        public Vector3 Waypoint(int index) => _points[index];

        public float RemainingDistance
        {
            get
            {
                if (!_hasDestination)
                    return float.PositiveInfinity;
                Vector3 position = Position;
                if (_count == 0)
                    return _pending ? Flat(_destination - position).magnitude : float.PositiveInfinity;

                int next = Mathf.Min(_index, _count - 1);
                float distance = Flat(_points[next] - position).magnitude;
                for (int i = next + 1; i < _count; i++)
                    distance += Flat(_points[i] - _points[i - 1]).magnitude;
                return distance;
            }
        }

        public NavVerdict SetDestination(Vector3 point)
        {
            HordeNavGrid grid = _nav?.Grid;
            if (grid == null || _actuator == null)
            {
                _status = NavStatus.Idle;
                return NavVerdict.NoGraph;
            }

            if (!grid.HasGround(grid.ClampCell(Position)) || !grid.TryCell(Position, out _))
            {
                Forget();
                _hasDestination = true;
                _destination = point;
                _status = NavStatus.Unreachable;
                _verdict = NavVerdict.OffGraph;
                return _verdict;
            }

            bool fresh = !_hasDestination;
            _destination = point;
            _hasDestination = true;
            if (fresh || (_count == 0 && !_pending && _status != NavStatus.Unreachable))
            {
                Plan();
                return NavVerdict.Reachable;
            }

            if (Flat(point - _plannedGoal).magnitude > GOAL_MOVED && _sincePlan >= MIN_REPLAN_GAP)
                Plan();

            return _pending && _count == 0 ? NavVerdict.Reachable : _verdict;
        }

        public void Clear()
        {
            Forget();
            _hasDestination = false;
            _status = NavStatus.Idle;
            _verdict = NavVerdict.Reachable;
        }

        public void Step(float dt)
        {
            if (!_hasDestination || _nav == null)
                return;

            HordeNavGrid grid = _nav.Grid;
            _sincePlan += dt;
            if (!_pending && (_sincePlan >= REPLAN_INTERVAL || (_count > 0 && grid.Version != _pathVersion && PathBroken(grid))))
                Plan();

            if (_count == 0)
                return;

            Vector3 position = Position;
            while (_index < _count - 1 && Flat(_points[_index] - position).magnitude <= ARRIVE)
                _index++;

            Vector3 to = Flat(_points[_index] - position);
            if (_index == _count - 1 && to.magnitude <= ARRIVE)
            {
                _actuator.Move(Vector3.zero);
                if (_partial)
                    _status = NavStatus.Unreachable;      // walked as near as the ground allows: no way on
                return;
            }

            _actuator.Move(to.normalized);
        }

        public void OnPathFound(HordePathStatus status, NativeList<float3> path, int gridVersion)
        {
            _pending = false;
            _pathVersion = gridVersion;
            _count = 0;
            _index = 1;
            _partial = false;
            if (!_hasDestination)
                return;

            if (status == HordePathStatus.Failed || path.Length == 0)
            {
                _status = NavStatus.Unreachable;
                _verdict = NavVerdict.OffGraph;
                return;
            }

            _count = Mathf.Min(path.Length, MAX_WAYPOINTS);
            for (int i = 0; i < _count; i++)
                _points[i] = path[i];
            _index = Mathf.Min(1, _count - 1);

            bool reaches = status == HordePathStatus.Found ||
                           Flat(_points[_count - 1] - _plannedGoal).magnitude <= CLOSE_ENOUGH;
            if (reaches)
            {
                // A way longer than the buffer is walked to its last kept waypoint and planned on from there (replan)
                _verdict = NavVerdict.Reachable;
                _status = NavStatus.Following;
                return;
            }

            _partial = true;
            _verdict = NavVerdict.Unreachable;
            // Already as near as the ground allows (boxed in): no way on at once
            _status = Flat(_points[_count - 1] - Position).magnitude > ARRIVE ? NavStatus.Following : NavStatus.Unreachable;
        }

        private Vector3 Position => _actuator.Body.Position;

        private void Plan()
        {
            LevelPathService paths = _nav.Paths;
            _plannedGoal = _destination;
            _sincePlan = 0f;
            _plans++;
            if (_status != NavStatus.Following)
                _status = NavStatus.Planning;
            _pending = true;
            if (!paths.Request(this, Position, _destination))
                _pending = false;                    // the ring is full: asked again on the next replan
        }

        private void Forget()
        {
            if (_pending)
                _nav?.Paths?.Cancel(this); // the level's grid may be gone already (the level unloads first)
            _pending = false;
            _count = 0;
            _index = 0;
            _partial = false;
        }

        // The grid changed: is a step along the rest of the path no longer walkable (a new crater wall, a fallen house)?
        private bool PathBroken(HordeNavGrid grid)
        {
            _pathVersion = grid.Version;
            float step = grid.CellSize * 0.5f;
            int2 previous = grid.ClampCell(Position);
            Vector3 from = Position;
            for (int i = _index; i < _count; i++)
            {
                Vector3 to = _points[i];
                Vector3 delta = Flat(to - from);
                int samples = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / step));
                for (int s = 1; s <= samples; s++)
                {
                    int2 cell = grid.ClampCell(from + delta * ((float)s / samples));
                    if (math.all(cell == previous))
                        continue;
                    if (math.all(math.abs(cell - previous) <= 1) && !grid.CanStep(previous, cell))
                        return true;
                    previous = cell;
                }

                from = to;
            }

            return false;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
