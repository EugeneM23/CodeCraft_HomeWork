using System.Collections.Generic;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Prototype (the user): the spider boss — a body and six legs out of cubes, walking about the sandbox and over the
    /// voxel houses (up a wall, over the roof, down the far side). No fighting yet: only the walk and the legs.
    /// <para>The body follows the surface under it: a few rays (down, ahead, down-ahead and one wrapping round an outer
    /// edge) plus the planted feet give the mean normal its up turns to; it moves along its forward on that plane. The
    /// legs walk in a tripod gait (<see cref="SpiderLeg"/>): three planted while the other three swing.</para>
    /// P in the sandbox (EnemyTankSpawner).
    /// </summary>
    public sealed class SpiderBoss : MonoBehaviour
    {
        private const float BODY_HEIGHT = 1.25f;    // m, body centre above the surface it walks on
        private const float SPEED = 3.4f;           // m/s
        private const float TURN_SPEED = 110f;      // deg/s
        private const float UP_RATE = 5f;           // how fast the body's up follows the surface, 1/s
        private const float HEIGHT_RATE = 8f;       // how fast it keeps its height over the surface, 1/s
        private const float PROBE_AHEAD = 2.2f;     // m, the ray that finds a wall in front
        private const float PROBE_FORWARD = 1.5f;   // m, how far ahead the down-ahead and wrap rays start
        private const float STEP_TIME = 0.2f;       // s a foot is in the air
        private const float STEP_DISTANCE = 0.85f;  // m a planted foot may lag behind its ideal spot before it steps
        private const float STEP_HEIGHT = 0.55f;    // m, the arc
        private const float REST_SETTLE = 0.25f;    // m, standing still it tidies its feet past this
        private const float GRAVITY = 18f;

        private const float ARRIVE = 3f;            // m, a wander point is reached
        private const float CHASE_STOP = 7f;        // m off the player it stops and stares
        private const float CHASE_TIME = 18f;       // s it follows the player before wandering again
        private const float STUCK_TIME = 7f;        // s with no headway: a new goal

        private static readonly Color SKIN = new(0.13f, 0.11f, 0.12f);
        private static readonly Color MARKS = new(0.55f, 0.08f, 0.05f);
        private static readonly Color CLAWS = new(0.05f, 0.05f, 0.05f);
        private static readonly Color EYES = new(1f, 0.15f, 0.05f);

        private static readonly RaycastHit[] s_hits = new RaycastHit[24];
        private static readonly List<SpiderBoss> s_alive = new();

        /// <summary>XZ corners of the map it keeps inside (the sandbox's map, a margin in).</summary>
        public static Vector2 MapMin { get; set; } = new(-36f, -36f);

        public static Vector2 MapMax { get; set; } = new(36f, 36f);

        public static int Alive => s_alive.Count;

        private readonly PlayerTank _player = new();
        private readonly List<Material> _materials = new();
        private SpiderLeg[] _legs;
        private Transform _look;    // the cubes of the body (bobs a little inside the root)

        private Vector3 _up = Vector3.up;
        private Vector3 _forward = Vector3.forward;
        private Vector3 _velocity;
        private Vector3 _fall;
        private int _lastGroup = 1;
        private float _restTimer;

        private Vector3 _goal;
        private bool _chasing;
        private float _goalTime;
        private float _bestDistance;
        private float _bestTime;

        /// <summary>Where the spider is going now (debug).</summary>
        public Vector3 Goal => _goal;

        /// <summary>The body's up: world up on the ground, the wall's normal on a wall.</summary>
        public Vector3 Up => _up;

        /// <summary>A spider standing on the surface at <paramref name="at"/>, facing along <paramref name="facing"/>.</summary>
        public static SpiderBoss Spawn(Vector3 at, Vector3 facing)
        {
            facing.y = 0f;
            var go = new GameObject("[SpiderBoss]");
            go.transform.SetPositionAndRotation(at + Vector3.up * BODY_HEIGHT,
                Quaternion.LookRotation(facing.sqrMagnitude > 0.01f ? facing.normalized : Vector3.forward));
            return go.AddComponent<SpiderBoss>();
        }

        /// <summary>Removes every spider (the debug panel).</summary>
        public static void KillAll()
        {
            for (int i = s_alive.Count - 1; i >= 0; i--)
                if (s_alive[i] != null)
                    Destroy(s_alive[i].gameObject);
            s_alive.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_alive.Clear();

        private void Awake()
        {
            s_alive.Add(this);
            _forward = transform.forward;
            _up = Vector3.up;
            Build();
            PickGoal();
            PlantAll();
        }

        private void OnDestroy()
        {
            s_alive.Remove(this);
            foreach (Material m in _materials)
                Destroy(m);
        }

        // ---- The cubes ----

        private void Build()
        {
            Material skin = NewMaterial(SKIN, false);
            Material marks = NewMaterial(MARKS, false);
            Material claws = NewMaterial(CLAWS, false);
            Material eyes = NewMaterial(EYES, true);

            _look = new GameObject("Body").transform;
            _look.SetParent(transform, false);
            // Cephalothorax with the legs on its sides, the big abdomen behind and raised, the head in front.
            Box(_look, "Thorax", new Vector3(0f, 0f, 0.25f), new Vector3(1.4f, 0.65f, 1.6f), skin);
            Box(_look, "Abdomen", new Vector3(0f, 0.35f, -1.55f), new Vector3(1.9f, 1.3f, 2.1f), skin);
            Box(_look, "Mark", new Vector3(0f, 1.0f, -1.55f), new Vector3(0.7f, 0.06f, 1.2f), marks);
            Box(_look, "MarkBar", new Vector3(0f, 1.0f, -1.3f), new Vector3(1.3f, 0.06f, 0.25f), marks);
            Box(_look, "Head", new Vector3(0f, 0.08f, 1.25f), new Vector3(0.9f, 0.5f, 0.55f), skin);
            Box(_look, "FangL", new Vector3(-0.2f, -0.3f, 1.5f), new Vector3(0.14f, 0.45f, 0.14f), claws);
            Box(_look, "FangR", new Vector3(0.2f, -0.3f, 1.5f), new Vector3(0.14f, 0.45f, 0.14f), claws);
            Box(_look, "EyeL", new Vector3(-0.22f, 0.22f, 1.53f), Vector3.one * 0.16f, eyes);
            Box(_look, "EyeR", new Vector3(0.22f, 0.22f, 1.53f), Vector3.one * 0.16f, eyes);
            Box(_look, "EyeL2", new Vector3(-0.36f, 0.14f, 1.5f), Vector3.one * 0.1f, eyes);
            Box(_look, "EyeR2", new Vector3(0.36f, 0.14f, 1.5f), Vector3.one * 0.1f, eyes);

            var legs = new GameObject("Legs").transform;
            legs.SetParent(transform, false);
            float h = BODY_HEIGHT;
            // Tripods: left-front, right-middle, left-rear step together; then the other three.
            _legs = new[]
            {
                new SpiderLeg(legs, new Vector3(-0.6f, 0f, 0.8f), new Vector3(-2.0f, -h, 2.1f), 0, skin, claws),
                new SpiderLeg(legs, new Vector3(0.6f, 0f, 0.8f), new Vector3(2.0f, -h, 2.1f), 1, skin, claws),
                new SpiderLeg(legs, new Vector3(-0.7f, 0f, 0.2f), new Vector3(-2.5f, -h, 0.3f), 1, skin, claws),
                new SpiderLeg(legs, new Vector3(0.7f, 0f, 0.2f), new Vector3(2.5f, -h, 0.3f), 0, skin, claws),
                new SpiderLeg(legs, new Vector3(-0.6f, 0f, -0.35f), new Vector3(-2.1f, -h, -1.7f), 0, skin, claws),
                new SpiderLeg(legs, new Vector3(0.6f, 0f, -0.35f), new Vector3(2.1f, -h, -1.7f), 1, skin, claws),
            };
        }

        private Material NewMaterial(Color color, bool glow)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(lit != null ? lit : Shader.Find("Standard")) { color = color };
            m.SetFloat("_Smoothness", 0.25f);
            if (glow)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 3f);
            }
            _materials.Add(m);
            return m;
        }

        private static void Box(Transform parent, string name, Vector3 at, Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        // ---- The body ----

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            Vector3 start = transform.position;
            Steer(dt);
            Vector3 p = transform.position;

            // Turn the forward towards where it wants to go, on the plane it stands on.
            Vector3 want = Desired(p, out bool stop);
            if (want.sqrMagnitude > 1e-4f)
                _forward = Vector3.RotateTowards(_forward, want, TURN_SPEED * Mathf.Deg2Rad * dt, 0f);
            _forward = Vector3.ProjectOnPlane(_forward, _up).normalized;

            // ---- The surface: what is under it, in front of it, and round an edge ahead ----
            Vector3 normals = Vector3.zero;
            bool hasSurface = false;
            Vector3 surface = default;
            float ahead = float.MaxValue;

            if (Cast(p + _up * 0.5f, -_up, BODY_HEIGHT * 2.5f, out RaycastHit hit))
            {
                normals += hit.normal;
                surface = hit.point;
                hasSurface = true;
            }

            // A wall in front: the closer, the more the body turns up onto it (an inner corner).
            if (Cast(p, _forward, PROBE_AHEAD, out hit))
            {
                ahead = hit.distance;
                float w = 1f - hit.distance / PROBE_AHEAD;
                normals += hit.normal * (w * 3f);
            }

            if (ahead > PROBE_FORWARD)
            {
                Vector3 front = p + _forward * PROBE_FORWARD;
                if (Cast(front + _up * 0.5f, -_up, BODY_HEIGHT * 2.5f, out hit))
                {
                    normals += hit.normal * 0.7f;
                    if (!hasSurface)
                    {
                        surface = hit.point;
                        hasSurface = true;
                    }
                }
                // Nothing under the front: an outer edge (the top of a wall, a roof's eave). Under the edge and back:
                // the face round the corner.
                else if (Cast(front - _up * (BODY_HEIGHT + 0.6f), -_forward, PROBE_FORWARD * 2f, out hit))
                {
                    normals += hit.normal * 1.5f;
                    if (!hasSurface)
                    {
                        surface = hit.point;
                        hasSurface = true;
                    }
                }
            }

            // The planted feet: the body leans by their mean normal too.
            int planted = 0;
            Vector3 feet = Vector3.zero;
            for (int i = 0; i < _legs.Length; i++)
            {
                if (!_legs[i].Planted)
                    continue;
                feet += _legs[i].Normal;
                planted++;
            }
            if (planted > 0)
                normals += feet / planted * 1.2f;

            if (hasSurface && normals.sqrMagnitude > 1e-4f)
            {
                _fall = Vector3.zero;
                Turn(normals.normalized, 1f - Mathf.Exp(-UP_RATE * dt));
                // Along the forward, unless jammed against a wall it is still turning up onto.
                if (!stop && ahead > BODY_HEIGHT * 0.55f)
                    p += _forward * (SPEED * dt);
                // Keep the height over the surface.
                Vector3 target = surface + _up * BODY_HEIGHT;
                p = Vector3.Lerp(p, target, 1f - Mathf.Exp(-HEIGHT_RATE * dt));
            }
            else if (planted >= 3)
            {
                // Clinging on by the feet alone (an odd corner): hold still, the feet will find a way.
                if (normals.sqrMagnitude > 1e-4f)
                    Turn(normals.normalized, 1f - Mathf.Exp(-UP_RATE * dt));
            }
            else
            {
                // Lost the surface: it falls, turning upright.
                _fall += Vector3.down * (GRAVITY * dt);
                p += _fall * dt;
                Turn(Vector3.up, 1f - Mathf.Exp(-3f * dt));
            }

            transform.SetPositionAndRotation(p, Quaternion.LookRotation(_forward, _up));
            _velocity = (p - start) / dt;
        }

        // The up turns towards the surface's; the forward goes round with it (on the ground into a wall becomes up the wall).
        private void Turn(Vector3 up, float k)
        {
            Vector3 next = Vector3.Slerp(_up, up, k).normalized;
            _forward = Quaternion.FromToRotation(_up, next) * _forward;
            _up = next;
            _forward = Vector3.ProjectOnPlane(_forward, _up).normalized;
            if (_forward.sqrMagnitude < 1e-4f)
                _forward = Vector3.ProjectOnPlane(Vector3.forward, _up).normalized;
        }

        // ---- The legs ----

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _legs == null)
                return;

            bool stepping = false;
            for (int i = 0; i < _legs.Length; i++)
            {
                _legs[i].Advance(dt, STEP_TIME, STEP_HEIGHT, _up);
                stepping |= _legs[i].Stepping;
            }

            // A foot left far behind (the body fell, or turned sharply round a corner): it steps at once.
            Vector3 lead = _velocity * (STEP_TIME * 1.2f);
            for (int i = 0; i < _legs.Length; i++)
            {
                SpiderLeg leg = _legs[i];
                if (!leg.Planted)
                {
                    if (!leg.Stepping)
                        Reseat(leg, lead);
                    continue;
                }
                if (Vector3.Distance(transform.TransformPoint(leg.HipLocal), leg.Foot) > SpiderLeg.REACH + 0.3f)
                    Reseat(leg, lead);
            }

            if (!stepping)
            {
                // Tripod: the group whose feet lag the most steps, the two groups taking turns.
                float e0 = GroupError(0, lead), e1 = GroupError(1, lead);
                bool moving = _velocity.sqrMagnitude > 0.25f;
                float threshold = moving ? STEP_DISTANCE : REST_SETTLE;
                if (!moving)
                    _restTimer += dt;
                else
                    _restTimer = 0f;
                if (Mathf.Max(e0, e1) > threshold && (moving || _restTimer > 0.4f))
                {
                    int group = 1 - _lastGroup;
                    if ((group == 0 ? e0 : e1) < 0.1f)
                        group = _lastGroup;
                    for (int i = 0; i < _legs.Length; i++)
                        if (_legs[i].Group == group)
                            Reseat(_legs[i], lead);
                    _lastGroup = group;
                }
            }

            // Breathing a little; leaning on the supports does the rest.
            _look.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 2.3f) * 0.04f, 0f);
            for (int i = 0; i < _legs.Length; i++)
                _legs[i].Pose(transform, _up);
        }

        private float GroupError(int group, Vector3 lead)
        {
            float worst = 0f;
            for (int i = 0; i < _legs.Length; i++)
            {
                SpiderLeg leg = _legs[i];
                if (leg.Group != group)
                    continue;
                if (!leg.Planted || !Ideal(leg, lead, out Vector3 at, out _))
                {
                    worst = Mathf.Max(worst, STEP_DISTANCE * 2f);
                    continue;
                }
                worst = Mathf.Max(worst, Vector3.Distance(leg.Foot, at));
            }
            return worst;
        }

        // Starts a step to the leg's ideal spot (or lets it hang when there is no surface in reach).
        private void Reseat(SpiderLeg leg, Vector3 lead)
        {
            if (Ideal(leg, lead, out Vector3 at, out Vector3 normal))
            {
                if (Vector3.Distance(leg.Foot, at) > 0.12f || !leg.Planted)
                    leg.Step(at, normal, _up);
            }
            else
                leg.Dangle(transform.TransformPoint(leg.RestLocal) + lead, _up);
        }

        private void PlantAll()
        {
            for (int i = 0; i < _legs.Length; i++)
            {
                SpiderLeg leg = _legs[i];
                if (Ideal(leg, Vector3.zero, out Vector3 at, out Vector3 normal))
                {
                    leg.Foot = at;
                    leg.Normal = normal;
                    leg.Planted = true;
                }
                else
                {
                    leg.Foot = transform.TransformPoint(leg.RestLocal);
                    leg.Normal = _up;
                }
            }
        }

        /// <summary>
        /// The spot a foot wants to be on: along the line from the hip out to its rest spot (catches a wall the leg
        /// reaches out to), else straight down from the rest spot, else round the edge under it (back towards the body).
        /// </summary>
        private bool Ideal(SpiderLeg leg, Vector3 lead, out Vector3 at, out Vector3 normal)
        {
            Vector3 hip = transform.TransformPoint(leg.HipLocal) + lead;
            Vector3 rest = transform.TransformPoint(leg.RestLocal) + lead;
            Vector3 reach = rest - hip;
            float length = reach.magnitude;
            Vector3 dir = reach / length;
            RaycastHit hit;
            if (Cast(hip, dir, length * 1.35f, out hit)
                || Cast(rest + _up * (BODY_HEIGHT * 0.8f), -_up, BODY_HEIGHT * 1.8f, out hit)
                || Cast(rest - _up * (BODY_HEIGHT * 0.8f), (transform.position - _up * BODY_HEIGHT - rest).normalized, 3f, out hit))
            {
                if ((hit.point - transform.TransformPoint(leg.HipLocal)).magnitude <= SpiderLeg.REACH + 0.3f)
                {
                    at = hit.point;
                    normal = hit.normal;
                    return true;
                }
            }
            at = rest;
            normal = _up;
            return false;
        }

        // ---- Where to go ----

        private void Steer(float dt)
        {
            _goalTime += dt;
            _player.Refresh();
            Transform player = _player.Transform;
            if (_chasing)
            {
                if (player == null || _goalTime > CHASE_TIME)
                    PickGoal();
                else
                    _goal = Clamp(player.position);
            }

            Vector3 p = transform.position;
            float distance = Flat(_goal - p).magnitude;
            if (!_chasing && distance < ARRIVE && _up.y > 0.6f)
            {
                PickGoal();
                return;
            }
            // No headway for a while (a dead end, a corner it cannot get round): somewhere else.
            if (distance < _bestDistance - 1f)
            {
                _bestDistance = distance;
                _bestTime = 0f;
            }
            else if ((_bestTime += dt) > STUCK_TIME && !(_chasing && distance < CHASE_STOP + 1f))
                PickGoal();
        }

        private void PickGoal()
        {
            _goalTime = 0f;
            _bestTime = 0f;
            _player.Refresh();
            Transform player = _player.Transform;
            _chasing = player != null && Random.value < 0.4f;
            _goal = _chasing
                ? Clamp(player.position)
                : new Vector3(Random.Range(MapMin.x, MapMax.x), 0f, Random.Range(MapMin.y, MapMax.y));
            _bestDistance = Flat(_goal - transform.position).magnitude;
        }

        private static Vector3 Clamp(Vector3 p) =>
            new(Mathf.Clamp(p.x, MapMin.x, MapMax.x), p.y, Mathf.Clamp(p.z, MapMin.y, MapMax.y));

        // The direction it wants on the plane it stands on. On a wall with the goal beyond it: straight up and over.
        private Vector3 Desired(Vector3 p, out bool stop)
        {
            Vector3 to = _goal - p;
            stop = _chasing && Flat(to).magnitude < CHASE_STOP && _up.y > 0.6f;
            Vector3 along = Vector3.ProjectOnPlane(to, _up);
            if (_up.y < 0.6f && Vector3.Dot(Flat(to), Flat(_up)) < 0f)
                along = Vector3.ProjectOnPlane(Vector3.up, _up).normalized * 3f + along.normalized;
            // Off the map (it climbed out over an edge house): back in.
            if (p.x < MapMin.x || p.x > MapMax.x || p.z < MapMin.y || p.z > MapMax.y)
                along = Vector3.ProjectOnPlane(Clamp(p) - p, _up) + along.normalized * 0.1f;
            return along.sqrMagnitude > 1e-4f ? along.normalized : Vector3.zero;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        // ---- Rays: the voxel world's colliders; tanks, debris and anything loose are passed through ----

        private static bool Cast(Vector3 origin, Vector3 dir, float length, out RaycastHit best)
        {
            best = default;
            int count = Physics.RaycastNonAlloc(origin, dir, s_hits, length, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = s_hits[i];
                if (hit.distance >= nearest || hit.distance <= 0f)
                    continue;
                Rigidbody body = hit.rigidbody;
                if (body != null && !body.isKinematic)
                    continue;
                nearest = hit.distance;
                best = hit;
            }
            return nearest < float.MaxValue;
        }
    }
}
