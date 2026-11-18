using System.Collections.Generic;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// The tank's mud, in voxels. Every road wheel presses a rut into voxel ground as it rolls:
    /// a small bite under it every few centimetres, the earth it takes thrown back off the
    /// track as clods that thud down and break apart. Clods of mud also drop off the top of the
    /// tracks as the tank drives, and the tank's own voxel skin gets splashed with the mud it
    /// drives through.
    /// </summary>
    /// <remarks>
    /// Works off the drawn wheels (found by name), after the suspension has set them on the
    /// ground, so the rut is where the wheel is seen. A rut is not dug deeper than
    /// <see cref="_maxDepth"/> below the top of the ground.
    /// </remarks>
    [DefaultExecutionOrder(9000)]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class VoxelTrackRuts : MonoBehaviour, IVoxelLateUpdate
    {
        [Tooltip("The road wheels, as paths from this object; '*' matches any name.")]
        [SerializeField] private string _wheels = "HullVisual/Wheel_*";
        [SerializeField] private float _wheelRadius = 0.225f;

        [Header("Ruts")]
        [Tooltip("Distance (m) a wheel rolls between two bites.")]
        [SerializeField] private float _spacing = 0.12f;
        [Tooltip("Half the width of a rut (m): a third wider than a road wheel's rim.")]
        [SerializeField] private float _biteRadius = 0.22f;
        [Tooltip("Deepest (m) a rut goes below the top of the ground.")]
        [SerializeField] private float _maxDepth = 0.3f;
        [Tooltip("Half the width (m) of the brown print a track leaves on grass and dry ground (no rut).")]
        [SerializeField] private float _dryBiteRadius = 0.2f;
        [Tooltip("Share of the surface voxels under a print that turn the colour of the earth; the rest stay green, however many wheels pass.")]
        [Range(0f, 1f)]
        [SerializeField] private float _trodden = 0.55f;
        [Tooltip("Most clods one bite throws.")]
        [SerializeField] private int _clodsPerBite = 3;

        [Header("Mud off the tracks")]
        [SerializeField] private float _clodsPerMetre = 1.5f;
        [SerializeField] private float _clodVoxel = 0.07f;

        [Header("Mud on the hull")]
        // Small splashes, most of them turned away above the tracks (see _heightFalloff): mud
        // builds up low on the tank over a drive, not all over it in a few metres.
        [Tooltip("Splashes of mud onto the voxel skin per metre driven.")]
        [SerializeField] private float _splashesPerMetre = 20f;
        [SerializeField] private float _splashRadius = 0.16f;
        [Tooltip("Share of the cells in a splash that take mud.")]
        [Range(0f, 1f)]
        [SerializeField] private float _splashFill = 0.5f;
        [Tooltip("How strongly mud keeps to the bottom of each part: 1 — anywhere on it, 2 — mostly the lower half.")]
        [SerializeField] private float _lowBias = 1.6f;
        [Tooltip("How fast mud thins out going up the whole tank: a splash at height share h (0 — the track bottom, 1 — the turret top) sticks with chance (1 - h) to this power.")]
        [SerializeField] private float _heightFalloff = 2.2f;
        [Tooltip("Height (m) of the tank from the bottom of the tracks to the top of the turret.")]
        [SerializeField] private float _tankHeight = 1.3f;

        [Header("Crumbs off the tracks")]
        [Tooltip("Small crumbs flung sideways from under the tracks, per metre the tracks run.")]
        [SerializeField] private float _crumbsPerMetre = 40f;
        [SerializeField] private Vector2 _crumbSize = new(0.035f, 0.06f);
        [Tooltip("Sideways speed (m/s) of a crumb, from–to.")]
        [SerializeField] private Vector2 _crumbSide = new(1.2f, 3.2f);
        [Tooltip("Upward speed (m/s) of a crumb, from–to: low, flat arcs.")]
        [SerializeField] private Vector2 _crumbUp = new(0.6f, 2f);

        [Header("Dry ground")]
        [Tooltip("Share of the crumbs the tracks still kick up on dry ground: a little dust and earth.")]
        [Range(0f, 1f)]
        [SerializeField] private float _dryCrumbShare = 0.25f;
        [Tooltip("Share of the crumbs the tracks still throw on grass: a few torn green bits.")]
        [Range(0f, 1f)]
        [SerializeField] private float _grassCrumbShare = 0.08f;
        [Tooltip("Share of the mud stuck on the tank that works loose per metre driven on dry ground.")]
        [SerializeField] private float _cleanPerMetre = 0.02f;
        [Tooltip("Metres of dry ground until the track belts have lost their mud.")]
        [SerializeField] private float _beltCleanDistance = 12f;
        [Tooltip("Metres of dry ground until the wheels, idlers and sprockets have flung off their mud and run clean, stains and all.")]
        [SerializeField] private float _gearCleanDistance = 12f;
        [Tooltip("How hard (m/s) the turning wheels fling the mud off.")]
        [SerializeField] private float _gearFling = 3.5f;

        private float _gearCleanDebt;

        [Header("Shaken like a wet dog (Shudder)")]
        [Tooltip("How hard (m/s) the mud and blood fly straight out off the tank when it shudders.")]
        [SerializeField] private float _shudderKick = 4.5f;
        [Tooltip("How fast (m/s) they fly round it as well, spun off the way a dog's shake throws water.")]
        [SerializeField] private float _shudderTwist = 3f;
        [Tooltip("Most clods and drops one part throws in one shudder; the rest of what comes off just goes.")]
        [SerializeField] private int _shudderClods = 700;

        [Header("Mud on the running gear")]
        [Tooltip("Splashes per metre onto the voxel wheels, idlers and sprockets: they are coated within a few metres.")]
        [SerializeField] private float _gearSplashesPerMetre = 14f;
        [SerializeField] private float _gearSplashRadius = 0.12f;
        [Tooltip("Metres of driving on mud until the track belt has darkened to the mud's colour.")]
        [SerializeField] private float _beltCoatDistance = 3f;

        [Header("Wheel spin (Shift on the gas)")]
        [Tooltip("Crumbs of earth per second the spinning tracks throw back, at full spin.")]
        [SerializeField] private float _roostPerSecond = 480f;
        [SerializeField] private Vector2 _roostSize = new(0.05f, 0.11f);
        [Tooltip("Backward speed (m/s) of the thrown earth, from–to.")]
        [SerializeField] private Vector2 _roostBack = new(4f, 10f);
        [Tooltip("Upward speed (m/s) of the thrown earth, from–to: a high rooster tail.")]
        [SerializeField] private Vector2 _roostUp = new(3f, 7.5f);
        [Tooltip("Clods (a few voxels stuck together) per second among the crumbs, at full spin.")]
        [SerializeField] private float _roostClodsPerSecond = 32f;
        [Tooltip("Bites per second the spinning tracks dig out of mud, at full spin.")]
        [SerializeField] private float _spinDigsPerSecond = 10f;

        /// <summary>Set by the drive: 0 — the tracks grip, 1 — they spin flat out (Shift on the gas).</summary>
        public float WheelSpin { get; set; }

        /// <summary>+1 the tracks spin forward (earth flies back), -1 in reverse.</summary>
        public float SpinDirection { get; set; } = 1f;

        private float _roostDebt;
        private float _roostClodDebt;
        private float _spinDigDebt;

        /// <summary>Slowest the tracks run (m/s) and still churn mud up.</summary>
        private const float MIN_TRACK_SPEED = 0.4f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly List<VoxelBody> _gear = new();
        private readonly List<Renderer> _belt = new();
        private readonly List<Color> _beltColours = new();
        private MaterialPropertyBlock _block;
        private float _gearDebt;
        private float _crumbDebt;
        private float _beltMud;
        private float _cleanDebt;
        private int _wheelsOnGround;
        private int _wheelsInMud;
        private int _wheelsOnDry;
        private Color32 _dust = new(95, 70, 45, 255);
        private Color32 _grassColour = new(60, 100, 30, 255);

        /// <summary>Share of the wheels in mud (0..1), eased; for the tank's speed.</summary>
        public float MudShare { get; private set; }

        /// <summary>Share of the wheels on dry ground — dry grass, bare earth, rock (0..1), eased.</summary>
        public float DryShare { get; private set; }

        private Rigidbody _body;
        private VoxelizedModel _skin;
        private readonly List<Transform> _wheelList = new();
        private readonly List<Vector3> _lastBite = new();
        // Alpha is dryness (Game/Voxel shader): until the ground is sampled, the mud is wet.
        private Color32 _mud = new(92, 64, 38, 1);
        private bool _onMud;
        private float _clodDebt;
        private float _splashDebt;
        private readonly List<Vector3> _lastWheel = new();

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _skin = GetComponent<VoxelizedModel>();
            string[] steps = _wheels.Split('/');
            string parentPath = string.Join("/", steps, 0, steps.Length - 1);
            Transform parent = parentPath.Length > 0 ? transform.Find(parentPath) : transform;
            string prefix = steps[^1].TrimEnd('*');
            if (parent == null)
                return;

            foreach (Transform child in parent)
            {
                if (!child.name.StartsWith(prefix))
                    continue;
                _wheelList.Add(child);
                _lastBite.Add(child.position);
                _lastWheel.Add(child.position);
            }
        }

        private void OnEnable() => VoxelTicks.Add(this);

        private void OnDisable() => VoxelTicks.Remove(this);

        private void LateUpdate() // perf-ok: ticks itself without the game loop (tests, demo scenes); one flag check when driven
        {
            if (!VoxelTicks.Driven)
                VoxelLateUpdate(Time.deltaTime);
        }

        public void VoxelLateUpdate(float deltaTime)
        {
            using var marker = RutsMarker.Auto();
            // As soon as the skin exists (one flag check after that).
            ReserveStains();
            Vector3 velocity = _body.linearVelocity;
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
            float speed = planar.magnitude;
            Vector3 moving = speed > 0.05f ? planar / speed : transform.forward;

            _onMud = false;
            _wheelsOnGround = 0;
            _wheelsInMud = 0;
            _wheelsOnDry = 0;
            for (int i = 0; i < _wheelList.Count; i++)
                Bite(i, moving, speed);

            CrushDebris(velocity, speed);

            // Shares of the wheels in mud and on dry ground, eased so a single wheel crossing a
            // border does not flick the tank's speed.
            float ease = 1f - Mathf.Exp(-4f * deltaTime);
            float mud = _wheelsOnGround > 0 ? _wheelsInMud / (float)_wheelsOnGround : MudShare;
            float dry = _wheelsOnGround > 0 ? _wheelsOnDry / (float)_wheelsOnGround : DryShare;
            MudShare = Mathf.Lerp(MudShare, mud, ease);
            DryShare = Mathf.Lerp(DryShare, dry, ease);

            // Spinning tracks throw earth even with the tank standing: before the distance check.
            if (WheelSpin > 0.01f && _wheelsOnGround > 0)
            {
                float spinMud = _wheelsInMud / (float)_wheelsOnGround;
                float spinGrass = (_wheelsOnGround - _wheelsInMud - _wheelsOnDry) / (float)_wheelsOnGround;
                Roost(deltaTime, spinMud, spinGrass);
            }

            // How far the tracks ran, not the hull: turning on the spot churns mud as well.
            float travelled = 0f;
            for (int i = 0; i < _wheelList.Count; i++)
            {
                travelled += Vector3.Distance(_wheelList[i].position, _lastWheel[i]);
                _lastWheel[i] = _wheelList[i].position;
            }

            travelled /= Mathf.Max(1, _wheelList.Count);

            // The wheels bob on the springs even standing still: that is not driving.
            if (travelled < MIN_TRACK_SPEED * deltaTime)
                return;
            if (!_onMud || travelled > 2f)
                return;

            // The skin is built in VoxelizedModel.Start: the running gear is looked up on first use.
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
                FindRunningGear();
            }

            // In mud the tracks throw it everywhere and the tank gets a coat; on dry ground they
            // kick up a little dust and earth, and the mud it carries slowly falls off.
            float inMud = _wheelsOnGround > 0 ? _wheelsInMud / (float)_wheelsOnGround : 0f;
            if (inMud > 0f)
            {
                DropClods(travelled * inMud, moving, speed);
                CoatRunningGear(travelled * inMud);
                Splash(travelled * inMud);
            }

            // Mud sprays mud; grass throws a few torn green bits; bare dry ground a little earth.
            float onGrass = _wheelsOnGround > 0 ? (_wheelsOnGround - _wheelsInMud - _wheelsOnDry) / (float)_wheelsOnGround : 0f;
            float share = inMud >= 0.5f ? Mathf.Lerp(_dryCrumbShare, 1f, inMud) : onGrass >= 0.5f ? _grassCrumbShare : _dryCrumbShare;
            Color32 colour = inMud >= 0.5f ? _mud : onGrass >= 0.5f ? _grassColour : _dust;
            SprayCrumbs(travelled * share, moving, speed, colour);

            if (inMud < 0.5f)
                Clean(travelled * (1f - inMud));
        }

        /// <summary>
        /// Driving on dry ground the mud works loose: a little of it drops off per metre, and the
        /// track belts fade back toward their own colour.
        /// </summary>
        private void Clean(float travelled)
        {
            if (travelled <= 0f)
                return;

            _cleanDebt += travelled * _cleanPerMetre;
            if (_cleanDebt >= 0.01f)
            {
                ShakeOffMud(_cleanDebt);
                _cleanDebt = 0f;
            }

            if (_beltMud > 0f)
            {
                _beltMud = Mathf.Max(0f, _beltMud - travelled / _beltCleanDistance);
                TintBelt();
            }

            CleanRunningGear(travelled);
        }

        /// <summary>
        /// On dry ground the turning wheels fling their mud off: clumps stuck on them fly off as
        /// clods, the stains flake away, and within <see cref="_gearCleanDistance"/> they run clean.
        /// </summary>
        private void CleanRunningGear(float travelled)
        {
            // A share of what is left goes per metre: 95% gone after the clean distance.
            _gearCleanDebt += travelled * 3f / Mathf.Max(0.1f, _gearCleanDistance);
            if (_gearCleanDebt < 0.05f)
                return;

            float share = Mathf.Clamp01(_gearCleanDebt);
            _gearCleanDebt = 0f;

            foreach (VoxelBody part in _gear)
            {
                if (part == null)
                    continue;

                // Flung outward from the wheel's own hub.
                Vector3 hub = part.WorldBounds.center;
                part.ShakeOff(share, hub, _gearFling, 120);
                part.Wash(share, hub, _gearFling, 60);
            }
        }

        /// <summary>A bite under one wheel, once it has rolled far enough since the last.</summary>
        private void Bite(int i, Vector3 moving, float speed)
        {
            Transform wheel = _wheelList[i];
            Vector3 position = wheel.position;

            if (!Physics.Raycast(position, Vector3.down, out RaycastHit hit, _wheelRadius + 0.25f, ~(1 << 2),
                    QueryTriggerInteraction.Ignore))
                return;

            VoxelBody ground = hit.collider.GetComponentInParent<VoxelBody>();
            if (ground == null || !ground.IsStatic)
                return;

            _onMud = true;
            _wheelsOnGround++;

            VoxelTerrainSettings terrain = VoxelTerrainSettings.Active;
            TerrainSoil soil = terrain != null ? terrain.SoilAt(hit.point) : TerrainSoil.Mud;
            bool mud = soil == TerrainSoil.Mud;
            if (mud)
                _wheelsInMud++;
            else if (soil != TerrainSoil.Grass)
                _wheelsOnDry++;

            // A little below the top: the earth under the grass, not the grass.
            if (ground.TryGetColor(hit.point - Vector3.up * 0.2f, out Color32 color))
            {
                if (mud)
                    _mud = color;
                else
                    _dust = color;
            }

            // The grass itself, for the bits of it the tracks tear up: taken beside the track, not
            // from the print under it, which is already trodden brown.
            Vector3 beside = hit.point + transform.right * (transform.InverseTransformPoint(position).x >= 0f ? 0.45f : -0.45f);
            if (soil is TerrainSoil.Grass or TerrainSoil.DryGrass && ground.TryGetColor(beside + Vector3.up * 0.05f, out Color32 blade))
                _grassColour = blade;

            if ((position - _lastBite[i]).sqrMagnitude < _spacing * _spacing)
                return;
            _lastBite[i] = position;

            // Grass and dry ground give no rut: the track only presses it flat, and the trodden
            // surface takes the brown of the earth beneath. Nothing is dug out.
            // Cobbles and flags take no print at all.
            if (soil == TerrainSoil.Paved)
                return;

            if (!mud)
            {
                ground.Paint(hit.point - Vector3.up * 0.04f, _dryBiteRadius, _dust, _trodden, true);
                return;
            }

            // Mud gives way into a deep rut, measured from the untouched surface: the terrain's
            // height there, or the top of flat ground.
            float surface = terrain != null ? terrain.HeightAt(hit.point) : ground.WorldBounds.max.y;
            if (surface - hit.point.y > _maxDepth)
                return;

            // Thrown back off the track and up: the faster it runs, the further they fly.
            Vector3 side = Vector3.Cross(Vector3.up, moving) * Random.Range(-0.5f, 0.5f);
            Vector3 throwBack = -moving * (0.4f + speed * 0.3f) + Vector3.up * (1.2f + speed * 0.12f) + side;
            ground.Dig(hit.point + Vector3.down * 0.04f, _biteRadius, throwBack, _clodsPerBite);
        }

        /// <summary>What lies under a moving road wheel is crushed: a sphere per wheel, run in the debris step.</summary>
        private void CrushDebris(Vector3 velocity, float speed)
        {
            if (speed <= 0.5f)
                return;
            VoxelDebris debris = VoxelWorld.Instance != null ? VoxelWorld.Instance.Debris : null;
            if (debris == null)
                return;

            for (int i = 0; i < _wheelList.Count; i++)
                debris.Crush(_wheelList[i].position - Vector3.up * (_wheelRadius * 0.5f), _wheelRadius + 0.1f, velocity);
        }

        /// <summary>Clods of mud dropping off the top run of the tracks, front and back.</summary>
        private void DropClods(float travelled, Vector3 moving, float speed)
        {
            VoxelDebris debris = VoxelWorld.Instance != null ? VoxelWorld.Instance.Debris : null;
            if (debris == null || _wheelList.Count == 0)
                return;

            _clodDebt += travelled * _clodsPerMetre;
            while (_clodDebt >= 1f)
            {
                _clodDebt -= 1f;
                Transform wheel = _wheelList[Random.Range(0, _wheelList.Count)];
                Vector3 from = wheel.position + Vector3.up * (_wheelRadius + 0.12f) - moving * 0.1f;
                Vector3 velocity = _body.GetPointVelocity(from) * 0.5f - moving * (0.3f + speed * 0.1f) +
                                   Vector3.up * Random.Range(0.3f, 0.9f) + Random.insideUnitSphere * 0.3f;

                int clod = debris.BeginCluster(from, velocity, 5f);
                var size = new Vector3Int(Random.Range(1, 4), Random.Range(1, 3), Random.Range(1, 3));
                for (int x = 0; x < size.x; x++)
                for (int y = 0; y < size.y; y++)
                for (int z = 0; z < size.z; z++)
                    debris.AddCube(clod, from + new Vector3(x, y, z) * _clodVoxel, _clodVoxel, Shade(_mud));
            }
        }

        private static readonly Unity.Profiling.ProfilerMarker RutsMarker = new("Voxels.Ruts");

        private bool _stainsReserved;

        /// <summary>
        /// The skin's stain buffers (VoxelBody.ReserveStains) and the running gear (FindRunningGear),
        /// made once the skin is built — at the start, before a fight — not in the frame of the first mud.
        /// </summary>
        private void ReserveStains()
        {
            if (_stainsReserved || _skin == null || _skin.Bodies.Count == 0)
                return;
            _stainsReserved = true;
            IReadOnlyList<VoxelBody> bodies = _skin.Bodies;
            for (int i = 0; i < bodies.Count; i++)
                if (bodies[i] != null)
                    bodies[i].ReserveStains();

            // The running gear and its colour block too: found at the first mud they were ~24 KB of
            // garbage in that frame (every renderer of the tank, a name string for each).
            _block ??= new MaterialPropertyBlock();
            FindRunningGear();
        }

        /// <summary>Mud splashed onto the lower hull and the wheels.</summary>
        private void Splash(float travelled)
        {
            if (_skin == null || _skin.Bodies.Count == 0 || _wheelList.Count == 0)
                return;

            ReserveStains();

            _splashDebt += travelled * _splashesPerMetre;
            while (_splashDebt >= 1f)
            {
                _splashDebt -= 1f;

                // Anywhere on the tank's surface, every part in proportion to its size — hull,
                // turret, wheels — and more often low down on each: an even coat, not a crust.
                VoxelBody body = PickPart();
                if (body == null || !body.TryGetSurfacePoint(_lowBias, out Vector3 at))
                    continue;

                // The lower down the tank, the dirtier: tracks and lower hull most, the turret hardly.
                float height = Mathf.Clamp01(transform.InverseTransformPoint(at).y / _tankHeight);
                if (Random.value > Mathf.Pow(1f - height, _heightFalloff))
                    continue;

                // The ground's own shade, unchanged: every new colour would take a palette slot.
                Color32 mud = _mud;

                // Mud piles on as voxels of its own, and stains the plate round it.
                body.Deposit(at, _splashRadius, mud, _splashFill);
                body.Paint(at, _splashRadius * 1.4f, mud, 0.3f);
                ThrowSplash(at);
            }
        }

        /// <summary>
        /// The clod that made a splash, seen flying: off the nearest wheel, up onto the spot it
        /// stuck to. It lands on the hull's collider or drops back to the ground and breaks there.
        /// </summary>
        private void ThrowSplash(Vector3 at)
        {
            VoxelDebris debris = VoxelWorld.Instance != null ? VoxelWorld.Instance.Debris : null;
            if (debris == null)
                return;

            Transform wheel = NearestWheel(at);
            Vector3 from = wheel.position + Vector3.down * (_wheelRadius * 0.4f);
            Vector3 rise = at - from;
            float gravity = -Physics.gravity.y * VoxelDebris.GRAVITY_SCALE;
            // A short lob that reaches the spot: up speed for the height plus a little extra.
            float up = Mathf.Sqrt(2f * gravity * Mathf.Max(0.05f, rise.y + 0.15f));
            float time = up / gravity;
            Vector3 across = new Vector3(rise.x, 0f, rise.z) / Mathf.Max(0.1f, time);
            Vector3 velocity = _body.GetPointVelocity(from) + across + Vector3.up * up;

            int clod = debris.BeginCluster(from, velocity, 6f);
            debris.AddCube(clod, from, _clodVoxel, Shade(_mud));
            if (Random.value < 0.5f)
                debris.AddCube(clod, from + Random.onUnitSphere * _clodVoxel, _clodVoxel, Shade(_mud));
        }

        /// <summary>
        /// Shift on the gas: the tracks spin and throw a rooster tail of earth back from under
        /// their rear ends — crumbs and clods of whatever they stand on, high and far — and in mud
        /// they dig themselves in.
        /// </summary>
        private void Roost(float deltaTime, float inMud, float onGrass)
        {
            VoxelDebris debris = VoxelWorld.Instance != null ? VoxelWorld.Instance.Debris : null;
            if (debris == null || _wheelList.Count == 0)
                return;

            float direction = SpinDirection >= 0f ? 1f : -1f;
            Vector3 back = Vector3.ProjectOnPlane(-transform.forward * direction, Vector3.up).normalized;
            Vector3 carried = _body.linearVelocity * 0.6f;
            Color32 earth = inMud >= 0.5f ? _mud : _dust;

            _roostDebt += _roostPerSecond * WheelSpin * deltaTime;
            while (_roostDebt >= 1f)
            {
                _roostDebt -= 1f;
                if (!debris.HasRoom(1))
                {
                    _roostDebt = 0f;
                    break;
                }

                Vector3 from = RoostPoint(direction, out Vector3 outward);
                Vector3 velocity = carried + back * Random.Range(_roostBack.x, _roostBack.y) * WheelSpin +
                                   Vector3.up * Random.Range(_roostUp.x, _roostUp.y) * Mathf.Lerp(0.5f, 1f, WheelSpin) +
                                   outward * Random.Range(-1f, 3f) + Random.insideUnitSphere * 1.2f;

                // On grass the tracks tear up turf: green bits among the earth.
                Color32 colour = onGrass >= 0.5f && Random.value < 0.4f ? _grassColour : earth;
                debris.Spawn(from, velocity, Shade(colour), Random.Range(_roostSize.x, _roostSize.y));
            }

            // Clods: in mud plenty, on hard ground a few.
            _roostClodDebt += _roostClodsPerSecond * WheelSpin * Mathf.Lerp(0.35f, 1f, inMud) * deltaTime;
            while (_roostClodDebt >= 1f)
            {
                _roostClodDebt -= 1f;
                if (!debris.HasRoom(8))
                {
                    _roostClodDebt = 0f;
                    break;
                }

                Vector3 from = RoostPoint(direction, out Vector3 outward);
                Vector3 velocity = carried + back * Random.Range(_roostBack.x, _roostBack.y) * 0.8f * WheelSpin +
                                   Vector3.up * Random.Range(_roostUp.x, _roostUp.y) * 0.8f + outward * Random.Range(0f, 1.2f);
                int clod = debris.BeginCluster(from, velocity, 8f);
                if (clod < 0)
                    break;

                var size = new Vector3Int(Random.Range(1, 3), Random.Range(1, 3), Random.Range(1, 3));
                Color32 colour = Shade(onGrass >= 0.5f && Random.value < 0.3f ? _grassColour : earth);
                for (int x = 0; x < size.x; x++)
                for (int y = 0; y < size.y; y++)
                for (int z = 0; z < size.z; z++)
                    debris.AddCube(clod, from + new Vector3(x, y, z) * _clodVoxel, _clodVoxel, colour);
            }

            // In mud the spinning tracks dig in, each bite thrown back with the rest.
            if (inMud <= 0f)
                return;

            _spinDigDebt += _spinDigsPerSecond * WheelSpin * inMud * deltaTime;
            while (_spinDigDebt >= 1f)
            {
                _spinDigDebt -= 1f;
                Transform wheel = _wheelList[Random.Range(0, _wheelList.Count)];
                if (!Physics.Raycast(wheel.position, Vector3.down, out RaycastHit hit, _wheelRadius + 0.25f, ~(1 << 2),
                        QueryTriggerInteraction.Ignore))
                    continue;

                VoxelBody ground = hit.collider.GetComponentInParent<VoxelBody>();
                if (ground == null || !ground.IsStatic)
                    continue;

                VoxelTerrainSettings terrain = VoxelTerrainSettings.Active;
                if (terrain != null && terrain.SoilAt(hit.point) != TerrainSoil.Mud)
                    continue;

                float surface = terrain != null ? terrain.HeightAt(hit.point) : ground.WorldBounds.max.y;
                if (surface - hit.point.y > _maxDepth)
                    continue;

                Vector3 throwBack = carried + back * Random.Range(_roostBack.x, _roostBack.y) * 0.6f +
                                    Vector3.up * Random.Range(_roostUp.x, _roostUp.y) * 0.7f;
                ground.Dig(hit.point + Vector3.down * 0.04f, _biteRadius * 0.8f, throwBack, _clodsPerBite);
            }
        }

        /// <summary>
        /// Under the rear end of a track (the front end in reverse), where the spinning belt comes
        /// off the ground; <paramref name="outward"/> is that track's side.
        /// </summary>
        private Vector3 RoostPoint(float direction, out Vector3 outward)
        {
            // The wheel furthest back of three picked: the earth comes off the rear of the track.
            Transform wheel = _wheelList[Random.Range(0, _wheelList.Count)];
            float rearmost = transform.InverseTransformPoint(wheel.position).z * direction;
            for (int i = 0; i < 2; i++)
            {
                Transform other = _wheelList[Random.Range(0, _wheelList.Count)];
                float z = transform.InverseTransformPoint(other.position).z * direction;
                if (z < rearmost)
                {
                    rearmost = z;
                    wheel = other;
                }
            }

            Vector3 local = transform.InverseTransformPoint(wheel.position);
            outward = transform.right * Mathf.Sign(local.x == 0f ? Random.value - 0.5f : local.x);
            return wheel.position + Vector3.down * (_wheelRadius * 0.7f) - transform.forward * direction * 0.25f +
                   outward * Random.Range(-0.15f, 0.15f);
        }

        /// <summary>
        /// Fine mud flung out sideways from under the tracks: single small crumbs on low, flat
        /// arcs, a little backwards, landing a metre or two off to the side.
        /// </summary>
        private void SprayCrumbs(float travelled, Vector3 moving, float speed, Color32 colour)
        {
            VoxelDebris debris = VoxelWorld.Instance != null ? VoxelWorld.Instance.Debris : null;
            if (debris == null || _wheelList.Count == 0)
                return;

            _crumbDebt += travelled * _crumbsPerMetre;
            while (_crumbDebt >= 1f)
            {
                _crumbDebt -= 1f;
                if (!debris.HasRoom(1))
                {
                    _crumbDebt = 0f;
                    return;
                }

                Transform wheel = _wheelList[Random.Range(0, _wheelList.Count)];
                Vector3 local = transform.InverseTransformPoint(wheel.position);
                Vector3 outward = transform.right * Mathf.Sign(local.x == 0f ? Random.value - 0.5f : local.x);
                Vector3 from = wheel.position + Vector3.down * (_wheelRadius * 0.8f) + outward * 0.15f;

                Vector3 velocity = outward * Random.Range(_crumbSide.x, _crumbSide.y) +
                                   Vector3.up * Random.Range(_crumbUp.x, _crumbUp.y) -
                                   moving * (speed * Random.Range(0.1f, 0.35f)) +
                                   transform.forward * Random.Range(-0.6f, 0.6f);

                debris.Spawn(from, velocity, Shade(colour), Random.Range(_crumbSize.x, _crumbSize.y));
            }
        }

        /// <summary>
        /// Shakes a share of the mud stuck on the tank off it — the jolt of a shot: it drops off
        /// the hull, the turret and the wheels as clods. Stains on the plate stay.
        /// </summary>
        public int ShakeOffMud(float share)
        {
            if (_skin == null)
                return 0;

            Vector3 centre = transform.position + transform.up * 0.6f;
            int removed = 0;
            IReadOnlyList<VoxelBody> bodies = _skin.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                if (bodies[i] != null)
                    removed += bodies[i].ShakeOff(share, centre, 1.2f, 600);
            }
            return removed;
        }

        /// <summary>
        /// The tank shudders like a wet dog — the jolt of a shot: a share of everything stuck on it,
        /// mud and blood, clods and stains, on every part, flies off outward and round
        /// (<paramref name="spin"/> +1 or -1 picks the way), the plate under it as it was; the
        /// track belts lose the same share of their mud. A share of 1 leaves the tank clean.
        /// </summary>
        public int Shudder(float share, float spin)
        {
            share = Mathf.Clamp01(share);
            if (_skin == null || share <= 0f)
                return 0;

            Vector3 centre = transform.position + transform.up * 0.6f;
            float twist = _shudderTwist * Mathf.Sign(spin == 0f ? 1f : spin);
            int removed = 0;
            IReadOnlyList<VoxelBody> bodies = _skin.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                VoxelBody body = bodies[i];
                if (body == null)
                    continue;
                // Stains first: one over mud stuck on turns back to that mud, which the shake then takes.
                removed += body.Wash(share, centre, _shudderKick, _shudderClods / 2, twist);
                removed += body.ShakeOff(share, centre, _shudderKick, _shudderClods, twist);
            }

            if (_beltMud > 0f)
            {
                _beltMud *= 1f - share;
                TintBelt();
            }

            return removed;
        }

        private Transform NearestWheel(Vector3 point)
        {
            Transform nearest = _wheelList[0];
            float best = float.MaxValue;
            foreach (Transform wheel in _wheelList)
            {
                float d = (wheel.position - point).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = wheel;
                }
            }

            return nearest;
        }

        /// <summary>
        /// The running gear takes mud at once: the voxel wheels, idlers and sprockets are coated in
        /// a few metres, and the track belt (plain meshes, not voxels) darkens to the mud's colour.
        /// </summary>
        private void CoatRunningGear(float travelled)
        {
            _gearDebt += travelled * _gearSplashesPerMetre;
            while (_gearDebt >= 1f && _gear.Count > 0)
            {
                _gearDebt -= 1f;
                VoxelBody part = _gear[Random.Range(0, _gear.Count)];
                if (part == null || !part.TryGetSurfacePoint(1f, out Vector3 at))
                    continue;
                part.Paint(at, _gearSplashRadius, _mud, 0.8f);
                part.Deposit(at, _gearSplashRadius * 0.8f, _mud, 0.3f);
            }

            if (_belt.Count == 0 || _beltMud >= 1f)
                return;

            _beltMud = Mathf.Min(1f, _beltMud + travelled / _beltCoatDistance);
            TintBelt();
        }

        /// <summary>The track belt's colour, from clean to caked in the last mud it ran through.</summary>
        private void TintBelt()
        {
            if (_block == null)
                return;

            Color mud = ((Color)_mud).gamma;
            for (int i = 0; i < _belt.Count; i++)
            {
                Renderer belt = _belt[i];
                belt.GetPropertyBlock(_block);
                Color colour = Color.Lerp(_beltColours[i], mud, _beltMud * 0.85f);
                colour.a = 1f;
                _block.SetColor(ColorId, colour);
                belt.SetPropertyBlock(_block);
            }
        }

        /// <summary>The voxel bodies of the wheels, idlers and sprockets, and the belt's renderers.</summary>
        private void FindRunningGear()
        {
            _gear.Clear();
            _belt.Clear();
            _beltColours.Clear();

            if (_skin != null)
            {
                foreach (VoxelBody body in _skin.Bodies)
                {
                    string part = body != null && body.transform.parent != null ? body.transform.parent.name : "";
                    if (part.StartsWith("Wheel_") || part.StartsWith("Track"))
                        _gear.Add(body);
                }
            }

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                string name = renderer.name;
                if (!(name.StartsWith("Track") && (name.Contains("_Link") || name.Contains("_Upper"))))
                    continue;
                Material material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty(ColorId))
                    continue;
                _belt.Add(renderer);
                _beltColours.Add(material.GetColor(ColorId));
            }
        }

        /// <summary>A part of the skin, picked with a chance in proportion to its voxels.</summary>
        private VoxelBody PickPart()
        {
            // By index: foreach over an IReadOnlyList boxes its enumerator, 40 B a call
            IReadOnlyList<VoxelBody> bodies = _skin.Bodies;
            int total = 0;
            for (int i = 0; i < bodies.Count; i++)
            {
                if (bodies[i] != null)
                    total += bodies[i].SolidCount;
            }

            if (total == 0)
                return null;

            int pick = Random.Range(0, total);
            for (int i = 0; i < bodies.Count; i++)
            {
                VoxelBody body = bodies[i];
                if (body == null)
                    continue;
                pick -= body.SolidCount;
                if (pick < 0)
                    return body;
            }

            return null;
        }

        private static Color32 Shade(Color32 color)
        {
            float shade = Random.Range(0.85f, 1.05f);
            return new Color32((byte)Mathf.Min(255, color.r * shade), (byte)Mathf.Min(255, color.g * shade),
                (byte)Mathf.Min(255, color.b * shade), color.a == 0 ? (byte)255 : color.a);
        }
    }
}
