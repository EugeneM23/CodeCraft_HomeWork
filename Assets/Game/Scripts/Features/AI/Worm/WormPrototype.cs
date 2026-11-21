using System.Collections;
using Game.Voxels;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Prototype (the user): an underground worm. A crack opens in the ground far off and runs at the player, the earth
    /// breaking along it; some way short of the player the ground bursts and the monster (a cube for now) tears out of
    /// it, stands a few seconds, then leaps and dives back into the ground. U in the sandbox (EnemyTankSpawner).
    /// </summary>
    public sealed class WormPrototype : MonoBehaviour
    {
        private const float START_DISTANCE = 30f;   // the crack opens this far from the player, m
        private const float ERUPT_DISTANCE = 8f;    // and bursts this far short of it, m
        private const float CRACK_SPEED = 7f;       // m/s the crack runs
        private const float CRACK_STEP = 0.45f;     // a bite every this many metres
        private const float CRACK_BITE = 0.4f;      // m, the crack's bites
        private const float CRACK_WIGGLE = 1.2f;    // m, the crack winds side to side
        private const float CRACK_TIMEOUT = 12f;
        private const float BURST_RADIUS = 2.2f;
        private const float DIVE_RADIUS = 1.8f;
        private const float IDLE_TIME = 3f;
        private static readonly Vector3 BODY = new(1.6f, 4f, 1.6f);

        private static readonly Color SKIN = new(0.35f, 0.12f, 0.22f);
        private static readonly RaycastHit[] s_hits = new RaycastHit[16];

        /// <summary>XZ corners of the ground it may come out of (the sandbox's map, a margin in).</summary>
        public static Vector2 MapMin { get; set; } = new(-1000f, -1000f);

        public static Vector2 MapMax { get; set; } = new(1000f, 1000f);

        private readonly PlayerTank _player = new();
        private Transform _body;

        /// <summary>A worm under the ground some way off the player, coming at it. Null — no player or no ground.</summary>
        public static WormPrototype Spawn()
        {
            WormPrototype worm = new GameObject("[Worm]").AddComponent<WormPrototype>();
            return worm;
        }

        private IEnumerator Start()
        {
            _player.Refresh();
            Transform player = _player.Transform;
            if (player == null || !TryStart(player.position, out Vector3 head))
            {
                Destroy(gameObject);
                yield break;
            }

            yield return Crack(head, h => head = h);
            yield return Burst(head);
            yield return Idle();
            yield return Dive();
            Destroy(gameObject);
        }

        // A random side, inside the map, over ground.
        private static bool TryStart(Vector3 centre, out Vector3 start)
        {
            float a0 = Random.value * Mathf.PI * 2f;
            for (int i = 0; i < 16; i++)
            {
                float a = a0 + i * Mathf.PI * 2f / 16f;
                Vector3 p = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * START_DISTANCE;
                if (p.x < MapMin.x || p.x > MapMax.x || p.z < MapMin.y || p.z > MapMax.y)
                    continue;
                if (!Ground(p, out start))
                    continue;
                return true;
            }
            start = default;
            return false;
        }

        // The crack runs at the player, winding a little, biting the ground as it goes; it stops some way short of it.
        private IEnumerator Crack(Vector3 head, System.Action<Vector3> moved)
        {
            float travelled = 0f;
            float nextBite = 0f;
            float time = 0f;
            float phase = Random.value * 10f;
            Vector3 line = head; // the crack's straight course; the head winds about it
            while (time < CRACK_TIMEOUT)
            {
                _player.Refresh();
                Transform player = _player.Transform;
                if (player == null)
                    break;
                Vector3 to = player.position - line;
                to.y = 0f;
                if (to.magnitude <= ERUPT_DISTANCE)
                    break;

                Vector3 along = to.normalized;
                float step = CRACK_SPEED * Time.deltaTime;
                line += along * step;
                travelled += step;
                Vector3 side = Vector3.Cross(Vector3.up, along);
                Vector3 wound = line + side * (Mathf.Sin(travelled * 0.55f + phase) * CRACK_WIGGLE);
                if (Ground(wound, out Vector3 ground))
                    head = ground;
                moved(head);

                while (travelled >= nextBite)
                {
                    nextBite += CRACK_STEP;
                    // A jagged rift, not round pits (the user: like broken asphalt): each step jumps a little to a side,
                    // the slit is cut along the jump in narrow bites, and slabs heave up along its edges.
                    Vector3 jag = head + side * Random.Range(-JAG, JAG);
                    if (!_hasLast)
                    {
                        _last = jag;
                        _hasLast = true;
                    }
                    Slit(_last, jag);
                    Vector3 course = jag - _last;
                    course.y = 0f;
                    Vector3 dir = course.sqrMagnitude > 1e-4f ? course.normalized : along;
                    // A slab every other step or so, sides taking turns: each is a voxel body with colliders.
                    if (Random.value < 0.5f)
                        WormShards.Heave(jag, dir, _slabSide = -_slabSide, CRACK_SCALE);
                    // Now and then a short branch splits off.
                    if (Random.value < BRANCH_CHANCE)
                        Branch(jag, dir);
                    _last = jag;
                }

                time += Time.deltaTime;
                yield return null;
            }
        }

        private const float JAG = 0.42f;            // m, how far each step of the rift jumps to a side
        private const float CRACK_SCALE = 1.2f;     // the rift's slabs (the user: 20 % bigger)
        private const float SLIT_BITE = 0.49f;      // m, the rift's bites (the user: wider, then 30 % wider again)
        private const float SLIT_STEP = 0.36f;      // m between them
        private const float BRANCH_CHANCE = 0.1f;
        private Vector3 _last;
        private bool _hasLast;
        private float _slabSide = 1f;

        // A narrow cut from a to b: small bites close together, a few chips flying.
        private static void Slit(Vector3 a, Vector3 b)
        {
            float length = Vector3.Distance(a, b);
            int n = Mathf.Max(1, Mathf.CeilToInt(length / SLIT_STEP));
            for (int i = 1; i <= n; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)n);
                float r = SLIT_BITE * Random.Range(0.8f, 1.25f);
                // Crumbs fly up out of it as it runs (the user): a stronger throw and more of them.
                Bite(p + Vector3.down * (r * 0.4f), r, 4.9f, 14);
                // Crumbs fly out of the rift as it runs (the user: bright and juicy): the ground's own colours, up and out.
                Crumbs(p, 16, 4.2f, 9.1f, 0.45f);
            }
        }

        // A short side crack: a few jagged steps off at an angle, thinning out.
        private static void Branch(Vector3 from, Vector3 dir)
        {
            float turn = Random.Range(30f, 65f) * (Random.value < 0.5f ? -1f : 1f);
            Vector3 heading = Quaternion.Euler(0f, turn, 0f) * dir;
            Vector3 side = Vector3.Cross(Vector3.up, heading);
            Vector3 at = from;
            int steps = Random.Range(3, 6);
            for (int i = 0; i < steps; i++)
            {
                Vector3 next = at + heading * 0.4f + side * Random.Range(-0.15f, 0.15f);
                if (Ground(next, out Vector3 g))
                    next = g;
                Slit(at, next);
                if (Random.value < 0.4f)
                    WormShards.Heave(next, heading, Random.value < 0.5f ? 1f : -1f, 0.6f);
                at = next;
            }
        }

        // The ground bursts and the body tears up out of it.
        private IEnumerator Burst(Vector3 at)
        {
            // Build-up: the ground shudders and splits round where it will come out.
            Shake(0.5f, 0.35f);
            for (int i = 0; i < 4; i++)
            {
                Vector3 split = at + Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward * Random.Range(0.8f, BURST_RADIUS);
                Slit(at, split);
                WormShards.Heave(split, split - at, Random.value < 0.5f ? 1f : -1f, 0.7f);
                yield return new WaitForSeconds(0.12f);
            }

            // The burst (the user: epic): a wide bite, a big spray of earth, the ground left heaved up round the hole.
            Bite(at, BURST_RADIUS * 1.3f, 12f, 140);
            Crumbs(at, 300, 8.4f, 18.2f, 2f);
            // A fountain of earth up out of the hole with it: bites from deep down throw their crumbs high.
            Bite(at + Vector3.down * 1.2f, BURST_RADIUS * 0.9f, 22f, 160);
            Bite(at + Vector3.down * 0.4f, BURST_RADIUS * 0.6f, 30f, 90);
            WormShards.Erupt(at, BURST_RADIUS);
            Shake(0.7f, 1.3f);

            _body = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            Destroy(_body.GetComponent<Collider>());
            _body.SetParent(transform, false);
            _body.localScale = BODY;
            Renderer skin = _body.GetComponent<Renderer>();
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null)
                skin.material = new Material(lit) { color = SKIN };
            else
                skin.material.color = SKIN;

            // It tears out and up (the user): flung high out of the hole, turning over a little, then falls and lands
            // beside it with a thump.
            Vector3 below = at + Vector3.down * (BODY.y + 0.5f);
            Face(at);
            Vector3 toward = Flat(_body.forward);
            Vector3 landAt = at + toward * Random.Range(2f, 3.5f);
            if (Ground(landAt, out Vector3 landGround))
                landAt = landGround;
            Vector3 stand = landAt + Vector3.up * (BODY.y * 0.5f);
            Quaternion facing = _body.rotation;
            Quaternion tumble = facing * Quaternion.Euler(Random.Range(-35f, 35f), 0f, Random.Range(-35f, 35f));

            const float up = 0.55f;    // s out of the hole to the top
            const float down = 0.45f;  // s down onto the ground
            float apex = Random.Range(7f, 9f);
            Vector3 top = Vector3.Lerp(at, landAt, 0.45f) + Vector3.up * (apex + BODY.y * 0.5f);
            for (float t = 0f; t < up; t += Time.deltaTime)
            {
                float k = t / up;
                float ease = 1f - (1f - k) * (1f - k); // fast out, slowing to the top
                _body.position = Vector3.Lerp(below, top, ease);
                _body.rotation = Quaternion.Slerp(facing, tumble, k);
                yield return null;
            }
            for (float t = 0f; t < down; t += Time.deltaTime)
            {
                float k = t / down;
                _body.position = Vector3.Lerp(top, stand, k * k); // speeding up as it falls
                _body.rotation = Quaternion.Slerp(tumble, facing, k);
                yield return null;
            }
            _body.position = stand;
            _body.rotation = facing;

            // The landing: the ground knocked in under it, crumbs up, the camera shaken; it squashes and springs back.
            Bite(landAt, 1.1f, 9f, 70);
            Crumbs(landAt, 130, 4.2f, 8.4f, 1f);
            Shake(0.4f, 0.9f);
            const float squash = 0.25f;
            for (float t = 0f; t < squash; t += Time.deltaTime)
            {
                float s = Mathf.Sin(t / squash * Mathf.PI) * 0.25f;
                _body.localScale = new Vector3(BODY.x * (1f + s * 0.6f), BODY.y * (1f - s), BODY.z * (1f + s * 0.6f));
                _body.position = stand + Vector3.down * (BODY.y * s * 0.5f);
                yield return null;
            }
            _body.localScale = BODY;
            _body.position = stand;
        }

        // It stands, breathing, turned to the player.
        private IEnumerator Idle()
        {
            Vector3 stand = _body.position;
            for (float t = 0f; t < IDLE_TIME; t += Time.deltaTime)
            {
                float breath = Mathf.Sin(t * 5f) * 0.06f;
                _body.localScale = new Vector3(BODY.x * (1f - breath * 0.5f), BODY.y * (1f + breath), BODY.z * (1f - breath * 0.5f));
                _body.position = stand + Vector3.up * (BODY.y * breath * 0.5f);
                Face(stand);
                yield return null;
            }
            _body.localScale = BODY;
            _body.position = stand;
        }

        // A leap off to the side, nose over, into the ground: it breaks again where it goes in, and is gone.
        private IEnumerator Dive()
        {
            Vector3 from = _body.position;
            Vector3 away = Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * Flat(_body.forward);
            Vector3 land = from + away * 7f;
            if (Ground(land, out Vector3 ground))
                land = ground;
            else
                land.y = from.y - BODY.y * 0.5f;

            Quaternion start = _body.rotation;
            Quaternion nose = Quaternion.LookRotation(Vector3.down, away);
            const float flight = 0.9f;
            for (float t = 0f; t < flight; t += Time.deltaTime)
            {
                float k = t / flight;
                Vector3 p = Vector3.Lerp(from, land, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 5f);
                _body.position = p;
                _body.rotation = Quaternion.Slerp(start, nose, Mathf.SmoothStep(0f, 1f, k));
                yield return null;
            }

            // The dive (the user: more epic): a deep, wide hole, a big throw of crumbs, the full lip of slabs.
            Bite(land, DIVE_RADIUS * 1.4f, 14f, 160);
            Crumbs(land, 280, 7.7f, 16.8f, 2.5f);
            Bite(land + Vector3.down * 1.3f, DIVE_RADIUS * 1.1f, 20f, 120);
            Bite(land + Vector3.down * 2.4f, DIVE_RADIUS * 0.8f, 10f, 40);
            WormShards.Erupt(land, DIVE_RADIUS * 1.2f, 1f, true); // the lip caves in, its slabs pointing down into the hole
            Shake(0.6f, 1.2f);
            Vector3 under = land + Vector3.down * (BODY.y + 1f);
            Vector3 entry = _body.position;
            const float sink = 0.4f;
            for (float t = 0f; t < sink; t += Time.deltaTime)
            {
                _body.position = Vector3.Lerp(entry, under, t / sink);
                yield return null;
            }
        }

        private void Face(Vector3 from)
        {
            _player.Refresh();
            Transform player = _player.Transform;
            if (player == null || _body == null)
                return;
            Vector3 look = Flat(player.position - from);
            if (look.sqrMagnitude > 0.01f)
                _body.rotation = Quaternion.LookRotation(look);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        // ---- Our ground only (the user: the cracks are in the ground, never stuck on the buildings) ----

        /// <summary>The voxel ground's surface under a point (houses, roofs, tanks and debris are passed through).</summary>
        internal static bool Ground(Vector3 above, out Vector3 point)
        {
            int count = Physics.RaycastNonAlloc(new Vector3(above.x, 60f, above.z), Vector3.down, s_hits, 120f, ~0,
                QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            point = above;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = s_hits[i];
                if (hit.distance >= best || !IsGround(hit.collider))
                    continue;
                best = hit.distance;
                point = hit.point;
            }
            return best < float.MaxValue;
        }

        /// <summary>Something standing on the ground at this point (a house, a wall): no slabs heave up there.</summary>
        internal static bool Covered(Vector3 ground)
        {
            int count = Physics.RaycastNonAlloc(ground + Vector3.up * 0.3f, Vector3.up, s_hits, 6f, ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = s_hits[i];
                if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
                    continue; // debris, tanks
                if (!IsGround(hit.collider))
                    return true;
            }
            return false;
        }

        /// <summary>A bite out of the voxel ground alone: the houses and walls round it are spared.</summary>
        internal static void Bite(Vector3 centre, float radius, float force, int debris)
        {
            VoxelBody ground = TerrainUnder(centre);
            if (ground == null)
                return;
            int taken = ground.CarveSphere(centre, radius, force, debris);
            VoxelSignals.RaiseCarved(centre, radius, taken);
        }

        /// <summary>
        /// A spray of our ground's crumbs (the user: bright and juicy): <paramref name="count"/> voxel cubes in the colours
        /// of the ground at <paramref name="at"/> — its top (grass, road) and the soil under it — thrown up at
        /// <paramref name="minSpeed"/>..<paramref name="maxSpeed"/> m/s and out round a spot of <paramref name="spread"/> m;
        /// one in eight a bigger clod.
        /// </summary>
        internal static void Crumbs(Vector3 at, int count, float minSpeed, float maxSpeed, float spread)
        {
            VoxelDebris debris = VoxelWorld.Instance != null ? VoxelWorld.Instance.Debris : null;
            VoxelBody ground = TerrainUnder(at);
            if (debris == null)
                return;
            Color32 top = new(60, 90, 30, 255), soil = new(80, 58, 36, 255);
            if (ground != null)
            {
                if (ground.TryGetColor(at + Vector3.up * 0.05f, out Color32 t))
                    top = t;
                if (ground.TryGetColor(at + Vector3.down * 0.35f, out Color32 s))
                    soil = s;
            }
            for (int i = 0; i < count; i++)
            {
                Vector2 disc = Random.insideUnitCircle * spread;
                Vector3 from = at + new Vector3(disc.x, 0.05f, disc.y);
                // Up, leaning out from the middle.
                Vector3 outward = new Vector3(disc.x, 0f, disc.y) + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * 0.6f;
                Vector3 dir = (Vector3.up * Random.Range(1.2f, 2.2f) + outward.normalized * Random.Range(0.3f, 1f)).normalized;
                float speed = Random.Range(minSpeed, maxSpeed);
                bool clod = Random.value < 0.125f;
                Color32 c = Random.value < 0.4f ? top : soil;
                float k = Random.Range(0.8f, 1.15f);
                c = new Color32((byte)Mathf.Min(255, c.r * k), (byte)Mathf.Min(255, c.g * k), (byte)Mathf.Min(255, c.b * k), 255);
                if (debris.Spawn(from, dir * speed, c, clod ? Random.Range(0.18f, 0.3f) : Random.Range(0.07f, 0.13f)) < 0)
                    break; // the debris is full
            }
        }

        private static bool IsGround(Collider collider)
        {
            if (collider == null)
                return false;
            VoxelBody body = collider.GetComponentInParent<VoxelBody>();
            return body != null && body.IsTerrain;
        }

        private static VoxelBody TerrainUnder(Vector3 at)
        {
            var bodies = VoxelWorld.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                VoxelBody b = bodies[i];
                if (b == null || !b.IsCreated || !b.IsTerrain)
                    continue;
                Bounds bounds = b.WorldBounds;
                if (at.x >= bounds.min.x && at.x <= bounds.max.x && at.z >= bounds.min.z && at.z <= bounds.max.z)
                    return b;
            }
            return null;
        }

        private static void Shake(float duration, float strength)
        {
            if (GameContext.TryGetInstance(out GameContext game) && game.TryGetPlayerContext(out PlayerContext player))
                CameraShakeUseCase.ShakePlayerCamera(player, new CameraShakeArgs(duration, strength));
        }
    }
}
