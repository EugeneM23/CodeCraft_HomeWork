using System;
using System.Collections.Generic;
using Game.Voxels;
using Unity.Profiling;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The arena of the chapter scene: builds one from its definition and the run's seed, takes the
    /// last one down, puts the tank at the entry, opens burrows for the AI director and the gate
    /// when the arena is cleared. Goes into the level's GameContext as <c>Arena</c>.
    /// </summary>
    /// <remarks>
    /// In a run the core calls <see cref="Build(ArenaId, int)"/>; the arena opens the gate on the run's
    /// ArenaCleared and tells it MarkGateOpened and MarkArenaLeft. Without a run (the scene played on its
    /// own) it builds arena 1 on Start. Nothing here ticks: the gate's exit is a physics trigger.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ArenaService : MonoBehaviour, IArenaService
    {
        [Tooltip("The arenas of the chapter: Build finds an arena here by its id.")]
        [SerializeField] private ArenaChapter _chapter;

        [Tooltip("The tank's body: put at the entry of every new arena; the gate waits for it.")]
        [SerializeField] private Rigidbody _tank;

        [Tooltip("Flat ground round the voxel tile, to the horizon. Empty — none.")]
        [SerializeField] private Material _backdrop;

        [Tooltip("Without a run (the scene played on its own), build arena 1 on Start.")]
        [SerializeField] private bool _buildOnStart = true;

        [Tooltip("Seed for Build On Start; 0 — a new one each time.")]
        [SerializeField] private int _seed;

        [Header("Burrows")]
        [Tooltip("Radius (m) of the crater a burrow tears in the ground.")]
        [SerializeField] private float _burrowRadius = 1.5f;
        [Tooltip("Speed (m/s) the clods leave a burrow's crater with.")]
        [SerializeField] private float _burrowForce = 4f;
        [Tooltip("Most debris cubes one burrow throws.")]
        [SerializeField] private int _burrowDebris = 500;

        private static readonly ProfilerMarker BuildMarker = new("Arena.Build");

        private readonly List<bool> _opened = new();
        private GameObject _root;
        private ArenaGate _gate;
        private ArenaPlan _plan;
        private Bounds _area;
        private System.Random _random = new(1);
        private IRunService _run;

        public event Action<ArenaPlan> Built;
        public event Action<Vector3> BurrowOpened;
        public event Action GateOpened;
        public event Action GatePassed;

        public bool IsBuilt => _plan != null;
        public ArenaPlan Plan => _plan;
        public ArenaDefinition Definition => _plan?.Definition;
        public Bounds Bounds => _plan != null ? _plan.Bounds : default;
        public Vector3 Entry => _plan != null ? _plan.Entry : transform.position;
        public IReadOnlyList<Vector3> BurrowPoints => _plan != null ? _plan.Burrows : Array.Empty<Vector3>();
        public IReadOnlyList<Vector3> CoverPoints => _plan != null ? _plan.Cover : Array.Empty<Vector3>();
        public bool IsGateOpen => _gate != null && _gate.IsOpen;
        public Vector3 GatePosition => _plan != null ? _plan.Gate.Position : transform.position;
        public ArenaChapter Chapter => _chapter;

        /// <summary>How long (ms) the last build took, for the perf report.</summary>
        public float LastBuildMs { get; private set; }

        public Rigidbody Tank
        {
            get => _tank;
            set => _tank = value;
        }

        private void Awake() => Register();

        private System.Collections.IEnumerator Start()
        {
            Register();
            HookRun();
            if (_run != null || !_buildOnStart || IsBuilt || _chapter == null || _chapter.At(1) == null)
                yield break;

            // The sandbox's camera orbit takes the camera's offset from the tank on its second frame:
            // the tank is moved to the arena after that, or the camera would stay hundreds of metres off.
            yield return null;
            yield return null;
            // The run may have come into the context meanwhile: then the core builds.
            HookRun();
            if (_run == null && !IsBuilt)
                Build(_chapter.At(1), _seed != 0 ? _seed : Environment.TickCount & 0x7FFFFFFF);
        }

        private void OnDestroy()
        {
            if (_run != null)
                _run.ArenaCleared -= OnArenaCleared;
            _run = null;

            if (GameContext.TryGetInstance(out GameContext context) && context.TryGetArena(out IArenaService arena) && ReferenceEquals(arena, this))
                context.DelArena();
        }

        public bool Build(ArenaId arena, int seed)
        {
            ArenaDefinition definition = _chapter != null ? _chapter.Find(arena) : null;
            if (definition == null)
            {
                Debug.LogError($"[ArenaService] The chapter has no arena '{arena}'.", this);
                return false;
            }

            Build(definition, seed);
            return true;
        }

        /// <summary>Builds this arena: see <see cref="IArenaService.Build"/>.</summary>
        public void Build(ArenaDefinition definition, int seed)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            HookRun();
            float started = Time.realtimeSinceStartup;
            using (BuildMarker.Auto())
            {
                TakeDown();

                _plan = ArenaPlanner.Plan(definition, seed);
                _random = new System.Random(seed ^ 0x5bd1e995);
                _opened.Clear();
                for (int i = 0; i < _plan.Burrows.Count; i++)
                    _opened.Add(false);

                _root = ArenaBuilder.Build(_plan, transform, _tank, _backdrop, out _gate);
                _gate.Passed += OnGatePassed;
                _area = _plan.Bounds;
                _area.Expand(new Vector3(ArenaPlan.MARGIN * 2f + 10f, 20f, ArenaPlan.MARGIN * 2f + 10f));

                PlaceTank();
            }

            LastBuildMs = (Time.realtimeSinceStartup - started) * 1000f;
            Built?.Invoke(_plan);
        }

        public bool TryPickBurrow(Vector3 tank, out int burrow)
        {
            burrow = -1;
            if (_plan == null)
                return false;

            const float far = ArenaPlan.BURROW_MIN_DISTANCE * ArenaPlan.BURROW_MIN_DISTANCE;
            List<Vector3> points = _plan.Burrows;
            int count = 0;
            for (int i = 0; i < points.Count; i++)
                if (FlatDistanceSq(points[i], tank) >= far)
                    count++;
            if (count == 0)
                return false;

            int pick = _random.Next(count);
            for (int i = 0; i < points.Count; i++)
            {
                if (FlatDistanceSq(points[i], tank) < far)
                    continue;
                if (pick-- == 0)
                {
                    burrow = i;
                    return true;
                }
            }

            return false;
        }

        public Vector3 OpenBurrow(int burrow)
        {
            if (_plan == null || burrow < 0 || burrow >= _plan.Burrows.Count)
                throw new ArgumentOutOfRangeException(nameof(burrow));

            Vector3 point = _plan.Burrows[burrow];
            if (_opened[burrow])
                return point;

            _opened[burrow] = true;
            // A small air blast: a bowl the horde climbs out of, a few lumps of turf and a spray of clods.
            VoxelDamage.AirBlast(point, _burrowRadius, 40f, _burrowForce, 2, 0.35f, 5f, _burrowDebris, 6f);
            BurrowOpened?.Invoke(point);
            return point;
        }

        public bool TryOpenBurrow(Vector3 tank, out Vector3 point)
        {
            point = default;
            if (!TryPickBurrow(tank, out int burrow))
                return false;

            point = OpenBurrow(burrow);
            return true;
        }

        public void OpenGate()
        {
            if (_gate == null || _gate.IsOpen)
                return;

            _gate.Open();
            GateOpened?.Invoke();
            _run?.MarkGateOpened();
        }

        private void OnGatePassed()
        {
            GatePassed?.Invoke();
            _run?.MarkArenaLeft();
        }

        private void OnArenaCleared(int arena) => OpenGate();

        /// <summary>Into the level's GameContext, once it is there (its Awake may come after this one).</summary>
        private void Register()
        {
            if (GameContext.TryGetInstance(out GameContext context) && !context.TryGetArena(out _))
                context.AddArena(this);
        }

        /// <summary>The run of the level, if there is one: its ArenaCleared opens the gate.</summary>
        private void HookRun()
        {
            if (_run != null)
                return;

            if (!GameContext.TryGetInstance(out GameContext context) || !context.TryGetRun(out IRunService run) || run == null)
                return;

            _run = run;
            _run.ArenaCleared += OnArenaCleared;
        }

        /// <summary>
        /// Destroys the arena that stands and the loose pieces that broke off it (they live at the
        /// scene's root). Debris cubes lying there stay: the next arena is elsewhere on the map.
        /// </summary>
        private void TakeDown()
        {
            if (_gate != null)
                _gate.Passed -= OnGatePassed;
            _gate = null;

            if (_root != null)
            {
                IReadOnlyList<VoxelBody> bodies = VoxelWorld.Bodies;
                for (int i = bodies.Count - 1; i >= 0; i--)
                {
                    VoxelBody body = bodies[i];
                    if (body == null || body.transform.parent != null || body.IsStatic)
                        continue;
                    if (_tank != null && body.transform.IsChildOf(_tank.transform))
                        continue;
                    if (_area.Intersects(body.WorldBounds))
                        Destroy(body.gameObject);
                }

                // Now, not at the end of the frame: the new ground must be the only one VoxelDamage sees.
                DestroyImmediate(_root);
            }

            _root = null;
            _plan = null;
        }

        private void PlaceTank()
        {
            if (_tank == null)
                return;

            Vector3 at = _plan.Entry + Vector3.up * 0.6f;
            Vector3 moved = at - _tank.position;
            Quaternion facing = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            _tank.linearVelocity = Vector3.zero;
            _tank.angularVelocity = Vector3.zero;
            _tank.transform.SetPositionAndRotation(at, facing);
            _tank.position = at;
            _tank.rotation = facing;
            Physics.SyncTransforms();

            // The camera jumps with the tank instead of easing after it across the map.
            if (GameContext.TryGetInstance(out GameContext context) && context.TryGetPlayerContext(out PlayerContext player)
                && player != null && player.TryGetCameraRoot(out Transform rig) && rig != null)
                rig.position += moved;
        }

        private static float FlatDistanceSq(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
