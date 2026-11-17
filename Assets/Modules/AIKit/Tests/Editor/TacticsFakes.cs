using System.Collections.Generic;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// A walled yard for tests: the ground the body may stand on is a box (MinX…MaxX, MinZ…MaxZ). It answers "how far can I
    /// go straight" (the walk probe of a NavMesh body) and keeps a test body inside (TestActuator.Confine).
    /// </summary>
    internal sealed class BoxYard : IWalkProbe
    {
        public readonly float MinX, MaxX, MinZ, MaxZ;

        public BoxYard(float minX, float maxX, float minZ, float maxZ)
        {
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
        }

        /// <summary>A square yard round the origin, half this wide.</summary>
        public static BoxYard Square(float half) => new(-half, half, -half, half);

        public float FreeDistance(Vector3 from, Vector3 direction, float maxDistance)
        {
            var flat = new Vector3(direction.x, 0f, direction.z);
            if (maxDistance <= 0f || flat.sqrMagnitude < 1e-8f || !Inside(from))
                return 0f;

            flat.Normalize();
            float free = maxDistance;
            if (flat.x > 1e-6f) free = Mathf.Min(free, (MaxX - from.x) / flat.x);
            if (flat.x < -1e-6f) free = Mathf.Min(free, (MinX - from.x) / flat.x);
            if (flat.z > 1e-6f) free = Mathf.Min(free, (MaxZ - from.z) / flat.z);
            if (flat.z < -1e-6f) free = Mathf.Min(free, (MinZ - from.z) / flat.z);
            return Mathf.Max(0f, free);
        }

        public bool Inside(Vector3 point) =>
            point.x >= MinX - 1e-4f && point.x <= MaxX + 1e-4f && point.z >= MinZ - 1e-4f && point.z <= MaxZ + 1e-4f;

        /// <summary>The point, kept inside the yard (a body walking into a wall stays at it).</summary>
        public Vector3 Confine(Vector3 point) =>
            new(Mathf.Clamp(point.x, MinX, MaxX), point.y, Mathf.Clamp(point.z, MinZ, MaxZ));

        /// <summary>Metres from the point to the nearest wall of the yard.</summary>
        public float ToWall(Vector3 point) =>
            Mathf.Min(Mathf.Min(point.x - MinX, MaxX - point.x), Mathf.Min(point.z - MinZ, MaxZ - point.z));
    }

    /// <summary>
    /// A navigator of a body that climbs, on flat ground with perches standing on it (each a square block, its top
    /// reached only by climbing): walks straight on one level; to another level — with climbing allowed — it walks to the
    /// edge of the perch, climbs (ClimbDirection ±1 for Climb Time, the body still) and comes out on the other level.
    /// The climb goes on in the body's own step (TestActuator.Applying), as a real climber's does.
    /// </summary>
    internal sealed class FakeClimber : INavigator, IClimbingNavigator
    {
        private const float EDGE = 0.4f;

        private readonly TestActuator _body;
        private readonly List<Perch> _perches;
        private float _climbLeft;
        private Vector3 _comeOut;

        public FakeClimber(TestActuator body, params Perch[] perches)
        {
            _body = body;
            _perches = new List<Perch>(perches);
            body.Applying += Climb;
        }

        /// <summary>Seconds one climb (up or down) takes.</summary>
        public float ClimbTime { get; set; } = 1f;

        public bool CanClimb => true;

        public bool ClimbAllowed { get; set; }

        public int ClimbDirection { get; private set; }

        public int Climbs { get; private set; }

        public NavStatus Status { get; private set; }

        public Vector3 Destination { get; private set; }

        public Vector3 SteeringTarget => Destination;

        public float RemainingDistance => Status.IsMoving() ? Vector3.Distance(_body.Position, Destination) : Mathf.Infinity;

        public NavEdgeKind CurrentEdge => NavEdgeKind.Walk;

        /// <summary>The perch the point stands on; null — the ground.</summary>
        public Perch Under(Vector3 point) => _perches.Find(perch => perch.IsOn(point));

        public ClimbWay WayTo(Vector3 point) => Under(_body.Position) == Under(point) ? ClimbWay.OnFoot : ClimbWay.OnlyByClimbing;

        public NavVerdict SetDestination(Vector3 point)
        {
            Destination = point;
            bool can = WayTo(point) == ClimbWay.OnFoot || ClimbAllowed;
            if (ClimbDirection == 0)
                Status = can ? NavStatus.Following : NavStatus.Unreachable;
            return can ? NavVerdict.Reachable : NavVerdict.Unreachable;
        }

        public void Clear()
        {
            if (ClimbDirection == 0)
                Status = NavStatus.Idle;
        }

        public void Step(float dt)
        {
            if (ClimbDirection != 0 || !Status.IsMoving())
                return;

            Perch from = Under(_body.Position);
            Perch to = Under(Destination);
            if (from == to)
            {
                Walk(Destination);
                return;
            }

            if (!ClimbAllowed)
            {
                Status = NavStatus.Unreachable;
                return;
            }

            // Up onto the perch it goes to, or first down off the one it stands on
            Perch wall = from ?? to;
            bool up = from == null;
            Vector3 center = wall.TopCenter;
            float side = wall.HalfSize + (up ? EDGE : -EDGE);
            Vector3 aim = up ? _body.Position : Destination;
            var edge = new Vector3(Mathf.Clamp(aim.x, center.x - side, center.x + side), _body.Position.y,
                Mathf.Clamp(aim.z, center.z - side, center.z + side));
            if (up && Mathf.Abs(_body.Position.x - center.x) < side && Mathf.Abs(_body.Position.z - center.z) < side)
                edge.x = center.x + side;               // under the perch: out to its edge first
            if (Flat(edge - _body.Position).magnitude > 0.1f)
            {
                Walk(edge);
                return;
            }

            // At the wall: the climb, then out on the other level
            ClimbDirection = up ? 1 : -1;
            _climbLeft = ClimbTime;
            Vector3 inward = Flat(center - _body.Position).normalized;
            _comeOut = up
                ? new Vector3(_body.Position.x, center.y, _body.Position.z) + inward * (EDGE * 2f)
                : new Vector3(_body.Position.x, 0f, _body.Position.z) - inward * (EDGE * 2f);
            _body.Move(Vector3.zero);
        }

        private void Walk(Vector3 point)
        {
            Status = NavStatus.Following;
            Vector3 to = Flat(point - _body.Position);
            if (to.sqrMagnitude > 1e-4f)
                _body.Move(to);
        }

        private void Climb(float dt)
        {
            if (ClimbDirection == 0)
                return;

            _body.Move(Vector3.zero);
            _climbLeft -= dt;
            if (_climbLeft > 0f)
                return;

            _body.Position = _comeOut;
            ClimbDirection = 0;
            Climbs++;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }

    /// <summary>
    /// Sight in a test: walls are rectangles seen from above (as tall as anything), a line is blocked when it crosses one.
    /// Distance from a line to the walls tells how much room a shot has.
    /// </summary>
    internal sealed class WallsSight : ISightProbe
    {
        private readonly List<Rect> _walls = new();

        /// <summary>A wall from (minX, minZ) to (maxX, maxZ).</summary>
        public WallsSight Wall(float minX, float minZ, float maxX, float maxZ)
        {
            _walls.Add(Rect.MinMaxRect(minX, minZ, maxX, maxZ));
            return this;
        }

        public bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            foreach (Rect wall in _walls)
            {
                if (Crosses(new Vector2(from.x, from.z), new Vector2(to.x, to.z), wall))
                    return false;
            }

            return true;
        }

        /// <summary>The least distance between the line (flat) and any wall.</summary>
        public float Room(Vector3 from, Vector3 to)
        {
            float room = float.MaxValue;
            for (int i = 0; i <= 200; i++)
            {
                Vector3 point = Vector3.Lerp(from, to, i / 200f);
                foreach (Rect wall in _walls)
                {
                    float dx = Mathf.Max(wall.xMin - point.x, 0f, point.x - wall.xMax);
                    float dz = Mathf.Max(wall.yMin - point.z, 0f, point.z - wall.yMax);
                    room = Mathf.Min(room, Mathf.Sqrt(dx * dx + dz * dz));
                }
            }

            return room;
        }

        // Liang–Barsky: the segment from a to b enters the rectangle
        private static bool Crosses(Vector2 a, Vector2 b, Rect box)
        {
            float t0 = 0f, t1 = 1f;
            Vector2 d = b - a;
            float[] p = { -d.x, d.x, -d.y, d.y };
            float[] q = { a.x - box.xMin, box.xMax - a.x, a.y - box.yMin, box.yMax - a.y };
            for (int i = 0; i < 4; i++)
            {
                if (Mathf.Abs(p[i]) < 1e-9f)
                {
                    if (q[i] < 0f)
                        return false;
                    continue;
                }

                float r = q[i] / p[i];
                if (p[i] < 0f)
                    t0 = Mathf.Max(t0, r);
                else
                    t1 = Mathf.Min(t1, r);
                if (t0 > t1)
                    return false;
            }

            return true;
        }
    }

    /// <summary>The objects of a test by kind (no tags, no scene): what the agent's object finder answers.</summary>
    internal sealed class ListFinder : IObjectFinder
    {
        private readonly Dictionary<string, List<Transform>> _kinds = new();

        public void Add(string kind, Transform thing)
        {
            if (!_kinds.TryGetValue(kind, out List<Transform> list))
                _kinds[kind] = list = new List<Transform>();
            list.Add(thing);
        }

        public bool Has(string kind, Transform thing) => _kinds.TryGetValue(kind, out List<Transform> list) && list.Contains(thing);

        public void Remove(Transform thing)
        {
            foreach (List<Transform> list in _kinds.Values)
                list.Remove(thing);
        }

        public Transform FindNearest(string kind, Vector3 center, float radius, Transform self)
        {
            var all = new List<Transform>();
            FindAll(kind, center, radius, self, all);
            Transform nearest = null;
            float best = float.MaxValue;
            foreach (Transform thing in all)
            {
                float distance = (thing.position - center).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = thing;
                }
            }

            return nearest;
        }

        public void FindAll(string kind, Vector3 center, float radius, Transform self, List<Transform> results)
        {
            results.Clear();
            if (kind == null || !_kinds.TryGetValue(kind, out List<Transform> list))
                return;

            foreach (Transform thing in list)
            {
                if (thing != null && thing != self && thing.gameObject.activeInHierarchy &&
                    (thing.position - center).sqrMagnitude <= radius * radius)
                    results.Add(thing);
            }
        }
    }
}
