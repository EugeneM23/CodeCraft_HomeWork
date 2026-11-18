using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace Game.Voxels
{
    /// <summary>
    /// One voxel object, Teardown style: a dense grid of one-byte materials (0 — empty) in the
    /// object's own space. The grid is cut into 16³ chunks; each chunk is a child with its own mesh
    /// and box colliders, rebuilt by Burst jobs only when a carve touched it.
    /// After every carve a flood fill finds the pieces that lost their hold: small ones crumble
    /// into debris cubes, bigger ones become voxel bodies of their own with a Rigidbody.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class VoxelBody : MonoBehaviour
    {
        public const int CHUNK = 16;

        /// <summary>A piece this small or smaller crumbles into debris cubes instead of becoming a body.</summary>
        private const int CRUMBLE_MAX = 12;

        /// <summary>A fracture piece smaller than this drops as a clod rather than flying on as a body.</summary>
        private const int FRACTURE_BODY_MIN = 60;

        /// <summary>...a building's, smaller than this (about 0.25 m³).</summary>
        private const int STRUCTURE_BODY_MIN = 250;

        /// <summary>Edge, in voxels, of the cells a carve's spray is grouped into clumps by.</summary>
        private const int CLUMP = 3;

        /// <summary>Share of the spray that flies as single crumbs rather than in clumps.</summary>
        private const float CRUMB_SHARE = 0.2f;

        /// <summary>Clumps one throw makes at most, about: a bomb's fountain is ~2600 cubes in 3³ cells.</summary>
        private const int CLUMPS_CAPACITY = 1024;

        // Sized up front and filled at level start (Prewarm): grown in the first blast they were ~14 KB of garbage.
        private static readonly Dictionary<int3, List<int>> Clumps = new(CLUMPS_CAPACITY);
        private static readonly Stack<List<int>> ClumpPool = new(CLUMPS_CAPACITY);

        /// <summary>
        /// Fills the clump pool before the fight (VoxelWorld at level start), so the first blast
        /// makes no garbage growing it.
        /// </summary>
        internal static void Prewarm()
        {
            while (ClumpPool.Count < CLUMPS_CAPACITY)
                ClumpPool.Push(new List<int>(CLUMP * CLUMP * CLUMP));
        }

        private const MeshUpdateFlags MESH_FLAGS =
            MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;

        public int3 Size { get; private set; }
        public float VoxelSize => _settings.VoxelSize;
        public float3 Origin => _settings.Origin;
        public VoxelAnchor Anchor => _settings.Anchor;
        public int SolidCount { get; private set; }

        /// <summary>
        /// How many breaks this piece is from a standing body: 0 for one that stands, 1 for a piece
        /// knocked off it, 2 for a piece of that piece... Impact fracture stops after a few, so
        /// rubble is left lying instead of everything ending as dust.
        /// </summary>
        public int Generation { get; private set; }
        public bool IsCreated => _voxels.IsCreated;
        public float Toughness => _settings.Toughness;

        internal NativeArray<byte> Voxels => _voxels;
        internal NativeArray<Color32> Palette => _palette;
        internal bool HasDirtyChunks => _dirtyCount > 0;

        private NativeArray<byte> _voxels;
        private NativeArray<Color32> _palette;

        /// <summary>Palette slots of loose mud: it can be shaken off (see ShakeOff).</summary>
        private readonly bool[] _loose = new bool[256];

        /// <summary>Palette slots a track's print (Paint with a fixed pattern) has used.</summary>
        private readonly bool[] _print = new bool[256];

        /// <summary>Under each stain (Paint) on a moving body: the material it covers; 0 unstained.</summary>
        private byte[] _beneath;
        private int _stained;
        /// <summary>Loose mud voxels, give or take (a carve may take some unseen): 0 — ShakeOff need not look.</summary>
        private int _looseCount;
        private VoxelBodySettings _settings;
        private Chunk[] _chunks;
        private bool[] _dirty;
        /// <summary>The chunk's voxels came or went (not just changed colour): its collider must follow.</summary>
        private bool[] _shapeDirty;
        /// <summary>A remesh of the chunk is still running (smooth bodies remesh over several frames).</summary>
        private bool[] _busy;
        private int _dirtyCount;
        /// <summary>The first, whole build is done: from now on a smooth body remeshes without waiting.</summary>
        private bool _built;
        private int3 _chunkCount;
        private int _chunk = CHUNK;
        private Material _material;
        private uint _seed;

        private Rigidbody _rigidbody;
        private bool _activatePending;
        private Vector3 _pendingVelocity;
        private Vector3 _pendingBlastCenter;
        private float _pendingBlastForce;
        private Vector3 _pendingSpin;

        /// <summary>
        /// A blast throws a piece up to this mass (kg, ~140 brick voxels) at its full speed; a
        /// heavier one gets a share in inverse proportion to its mass, as a fixed push would give it.
        /// A house that loses its last post (56 t) only sags off it.
        /// </summary>
        private const float BLAST_FULL_MASS = 250f;

        /// <summary>Distance (m) at which a blast's push on a piece falls off to nothing.</summary>
        private const float BLAST_REACH = 8f;

        /// <summary>How much "up" is added to a blast's push, so pieces lift rather than skim.</summary>
        private const float BLAST_LIFT = 0.4f;

        /// <summary>Spin (rad/s per m/s of push) a blast gives a piece, on a random axis.</summary>
        private const float BLAST_SPIN = 0.35f;

        /// <summary>Fastest a piece may set off (m/s): nothing a blast or a break does exceeds it.</summary>
        public const float MAX_LAUNCH_SPEED = 30f;

        /// <summary>Fastest a piece may spin (rad/s), at launch and after.</summary>
        public const float MAX_SPIN = 12f;

        /// <summary>
        /// Carve speed (m/s) above which a settled body is thrown again: a shell's blast (9) is, a
        /// bullet's chip (2.5) is not.
        /// </summary>
        public const float WAKE_FORCE = 5f;

        /// <summary>
        /// Thrown, falling, or about to be (let go at the end of this frame, see FinishRebuild).
        /// A piece that settled (Freeze) keeps its Rigidbody, kinematic, and is not loose.
        /// </summary>
        public bool IsLoose => _rigidbody != null && (!_rigidbody.isKinematic || _activatePending);

        /// <summary>
        /// Lets a body made whole (a creature's piece) go as a loose one: kinematic until its
        /// colliders exist at the end of this frame, then off with this velocity and spin.
        /// </summary>
        public void Launch(Vector3 velocity, Vector3 spin)
        {
            if (IsLoose || !_voxels.IsCreated)
                return;

            MakeDynamic(BodyMass(), velocity, transform.position, 0f);
            _pendingSpin = spin;
        }

        /// <summary>
        /// Takes out every piece not joined to the biggest one: big ones as bodies, scraps as clods.
        /// Called right after Launch they fly on with this body (see InheritedVelocity).
        /// </summary>
        public void SplitLoose()
        {
            if (_voxels.IsCreated && _settings.Split && SolidCount > 0)
                Split(transform.position, 0f);
        }

        /// <summary>
        /// Takes a loose body that came to rest out of the simulation: it stays where it lies,
        /// colliders and all, its Rigidbody kinematic. The Rigidbody is kept, not destroyed: a
        /// destroyed one lingers to the end of the frame, so a blast in the same step could not add
        /// a new one; and the game tells a piece from the scenery by its body (the Rubble layer).
        /// </summary>
        internal void Freeze()
        {
            if (!IsLoose)
                return;

            // Zeroed while still simulated: a kinematic body's velocity is not to be set.
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.isKinematic = true;
            _activatePending = false;
        }

        internal sealed class Chunk
        {
            public GameObject GameObject;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public readonly List<BoxCollider> Boxes = new();
            public MeshCollider Collider;

            /// <summary>
            /// A mesh collider's own meshes, apart from the drawn one: one is in the collider, the
            /// other is written and cooked on a worker, then swapped in. Changing a mesh a collider
            /// holds would cook it again on the main thread.
            /// </summary>
            public Mesh CollisionA;
            public Mesh CollisionB;
            /// <summary>The spare collision mesh is being cooked: it is not to be written until it is swapped in.</summary>
            public bool Cooking;

            /// <summary>Ground: the chunk has more than level top faces (see UpdateRelief).</summary>
            public bool Relief;
            public bool ShadowsSet;
        }

        /// <summary>A chunk rebuild in flight: the job outputs and where they go.</summary>
        internal struct ChunkJob
        {
            public VoxelBody Body;
            public int Chunk;
            public NativeList<VoxelVertex> Vertices;
            public NativeList<int> Indices;
            public NativeList<VoxelBox> Boxes;
            /// <summary>Where an async job's region starts: its boxes are in region voxels, this far from the grid's.</summary>
            public int3 BoxOffset;
            /// <summary>An over-several-frames remesh's own copies of the voxels and palette it reads.</summary>
            public NativeArray<byte> Region;
            public NativeArray<Color32> Palette;
            /// <summary>Voxels came or went: the collider is rebuilt too (a colour change only redraws).</summary>
            public bool Shape;
            /// <summary>Remeshes over several frames: the chunk is busy until it is applied.</summary>
            public bool Async;
            public int Frame;
            public JobHandle Handle;

            public void Dispose()
            {
                Vertices.Dispose();
                Indices.Dispose();
                if (Boxes.IsCreated)
                    Boxes.Dispose();
                if (Region.IsCreated)
                    Region.Dispose();
                if (Palette.IsCreated)
                    Palette.Dispose();
            }
        }

        /// <summary>
        /// Hands the body its grid and palette; the body owns and disposes both from now on.
        /// </summary>
        public void Init(int3 size, NativeArray<byte> voxels, NativeArray<Color32> palette, VoxelBodySettings settings)
        {
            if (_voxels.IsCreated)
                Release();

            Size = size;
            _voxels = voxels;
            _palette = palette;
            _settings = settings;
            if (_settings.Toughness <= 0f)
                _settings.Toughness = 1f;
            if (_settings.Density <= 0f)
                _settings.Density = 1800f;
            if (_settings.CraterDepth <= 0f)
                _settings.CraterDepth = 1f;

            _material = settings.Material != null ? settings.Material : VoxelWorld.Instance.ChunkMaterial;
            _seed = (uint)GetInstanceID();

            _chunk = _settings.ChunkSize > 0 ? _settings.ChunkSize : CHUNK;
            _chunkCount = (size + _chunk - 1) / _chunk;
            int chunks = _chunkCount.x * _chunkCount.y * _chunkCount.z;
            _chunks = new Chunk[chunks];
            _dirty = new bool[chunks];
            _shapeDirty = new bool[chunks];
            _busy = new bool[chunks];
            _dirtyCount = 0;
            _built = false;
            _looseCount = 0;

            using (var count = new NativeReference<int>(Allocator.TempJob))
            {
                new CountSolidJob { Voxels = _voxels, Count = count }.Run();
                SolidCount = count.Value;
            }

            MarkDirty(int3.zero, size - 1);
            VoxelWorld.Register(this);
        }

        /// <summary>True for a body that stands (ground, a building), not one that flies or rides.</summary>
        public bool IsStatic => _rigidbody == null && _settings.Anchor == VoxelAnchor.Ground;

        /// <summary>Rolling ground: driven on, not rammed.</summary>
        public bool IsTerrain => _settings.Terrain;

        /// <summary>
        /// Makes room now for the plate remembered under stains and mud (Paint, Deposit), which is
        /// otherwise made at the first stain — half a megabyte over a tank's skin in the frame of the
        /// first crush. For a skin that will be stained in a fight; call it at level start.
        /// </summary>
        public void ReserveStains()
        {
            if (_voxels.IsCreated && !IsStatic)
                _beneath ??= new byte[_voxels.Length];
        }

        /// <summary>
        /// The colour of the first solid voxel at or below a point, looking a few voxels down.
        /// False when there is none.
        /// </summary>
        public bool TryGetColor(Vector3 worldPoint, out Color32 color)
        {
            color = default;
            if (!_voxels.IsCreated)
                return false;

            int3 p = (int3)math.floor(((float3)transform.InverseTransformPoint(worldPoint) - Origin) / VoxelSize);
            for (int i = 0; i < 4; i++, p.y--)
            {
                if (!VoxelMath.Inside(p, Size))
                    continue;
                byte material = _voxels[VoxelMath.Index(p, Size)];
                if (material == 0)
                    continue;
                color = _palette[material];
                return true;
            }

            return false;
        }

        /// <summary>
        /// Splashes a colour onto the solid voxels in a sphere, each with the given chance — mud on
        /// a hull. The colour takes a free palette slot the first time; nothing happens once the
        /// palette is full.
        /// </summary>
        /// <param name="fixedPattern">The same voxels take the colour however often the spot is
        /// painted (a track's print, passed over by every road wheel): what is left unpainted stays so.</param>
        public int Paint(Vector3 worldCenter, float worldRadius, Color32 color, float chance, bool fixedPattern = false)
        {
            if (!_voxels.IsCreated || SolidCount == 0)
                return 0;

            // A print takes slots of its own, apart from the same shade in the ground, so a print
            // over a print is known as such (below) and natural earth of that shade is not.
            byte material = PaletteSlot(color, false, fixedPattern);
            if (material == 0)
                return 0;

            float scale = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
            float3 center = ((float3)transform.InverseTransformPoint(worldCenter) - Origin) / VoxelSize;
            float radius = Mathf.Max(0.75f, worldRadius / (scale * VoxelSize));
            int3 lo = math.max(0, (int3)math.floor(center - radius));
            int3 hi = math.min(Size - 1, (int3)math.floor(center + radius));
            uint seed = fixedPattern ? 0x5EEDu : _seed++;
            int painted = 0;

            for (int z = lo.z; z <= hi.z; z++)
            for (int y = lo.y; y <= hi.y; y++)
            for (int x = lo.x; x <= hi.x; x++)
            {
                int3 p = new int3(x, y, z);
                int index = VoxelMath.Index(p, Size);
                if (_voxels[index] == 0 || _voxels[index] == material)
                    continue;
                // A print over a print stays as it is: the following road wheels bring the earth's
                // shade sampled afresh, and each repaint redrew the chunk for nothing.
                if (fixedPattern && _print[_voxels[index]])
                    continue;
                // A fixed pattern goes by 2-voxel blocks: what stays unpainted is tufts, not single pixels.
                if (math.distancesq((float3)p + 0.5f, center) > radius * radius ||
                    VoxelMath.Hash01(fixedPattern ? p / 2 : p, seed) > chance)
                    continue;

                // A moving body remembers what was under the stain, so it can be washed off (Wash).
                if (!IsStatic)
                {
                    _beneath ??= new byte[_voxels.Length];
                    if (_beneath[index] == 0)
                    {
                        _beneath[index] = _voxels[index];
                        _stained++;
                    }
                }

                _voxels[index] = material;
                painted++;
            }

            if (painted > 0)
                MarkDirty(lo, hi, false);
            return painted;
        }

        /// <summary>
        /// Washes a share of the stains (<see cref="Paint"/>) off a moving body: those voxels take
        /// back the colour they had, and some of the mud flakes off as crumbs with the body's own
        /// motion plus <paramref name="kick"/> out from <paramref name="from"/> and
        /// <paramref name="twist"/> round it (m/s; a wet dog's shake). Returns how many.
        /// </summary>
        public int Wash(float share, Vector3 from, float kick, int debrisBudget, float twist = 0f)
        {
            if (_beneath == null || _stained == 0 || !_voxels.IsCreated || share <= 0f)
                return 0;

            VoxelDebris debris = VoxelWorld.Instance != null ? VoxelWorld.Instance.Debris : null;
            float size = VoxelSize * Mathf.Abs(transform.lossyScale.x);
            int washed = 0;
            int thrown = 0;

            for (int i = 0; i < _beneath.Length; i++)
            {
                byte under = _beneath[i];
                if (under == 0)
                    continue;

                byte material = _voxels[i];
                // Shot away: nothing of the stain is left to wash.
                if (material == 0)
                {
                    _beneath[i] = 0;
                    _stained--;
                    continue;
                }

                // Under loose mud since: ShakeOff takes the mud and gives back what was beneath.
                if (_loose[material])
                    continue;

                if (Random.value > share)
                    continue;

                _voxels[i] = under;
                // A stain over loose mud washes back to that mud: ShakeOff must look for it again.
                if (_loose[under])
                    _looseCount++;
                _beneath[i] = 0;
                _stained--;
                washed++;

                if (debris == null || thrown >= debrisBudget || Random.value > 0.5f)
                    continue;

                Vector3 position = transform.TransformPoint(Origin + ((float3)VoxelMath.Coord(i, Size) + 0.5f) * VoxelSize);
                Vector3 away = position - from;
                away.y = 0f;
                away = away.sqrMagnitude > 0.0001f ? away.normalized : Random.onUnitSphere;
                Vector3 velocity = InheritedVelocity(position) + Flung(away, kick, twist);
                debris.Spawn(position, velocity, _palette[material], size * 0.8f);
                thrown++;
            }

            if (washed > 0)
                MarkDirty(int3.zero, Size - 1, false);
            return washed;
        }

        /// <summary>
        /// The palette slot holding a colour, taking a free one if it is new; 0 when full.
        /// <paramref name="loose"/> slots are kept apart from the same colour on the plate: mud
        /// that sits on the body (Deposit) can be shaken off, a stain on the plate (Paint) cannot.
        /// <paramref name="print"/> slots (a track's print) are kept apart the same way.
        /// </summary>
        private byte PaletteSlot(Color32 color, bool loose = false, bool print = false)
        {
            for (int i = 1; i < _palette.Length; i++)
            {
                Color32 entry = _palette[i];
                // Alpha is the colour's dryness (see MeshVoxelizer.WithWetness); 0 would read as a free slot.
                if (color.a == 0)
                    color.a = 255;

                if (entry.a == 0)
                {
                    _palette[i] = color;
                    _loose[i] = loose;
                    _print[i] = print;
                    return (byte)i;
                }

                if (entry.r == color.r && entry.g == color.g && entry.b == color.b && entry.a == color.a &&
                    _loose[i] == loose && _print[i] == print)
                    return (byte)i;
            }

            return 0;
        }

        /// <summary>
        /// Knocks a share of the loose mud (<see cref="Deposit"/>) off the body: mud stuck on
        /// beside the plate goes, mud laid over it gives the plate back as it was (<see cref="_beneath"/>);
        /// it falls off as clods with the body's own motion plus <paramref name="kick"/> out from
        /// <paramref name="from"/> and <paramref name="twist"/> round it. Returns how many came off.
        /// </summary>
        public int ShakeOff(float share, Vector3 from, float kick, int debrisBudget, float twist = 0f)
        {
            // No loose mud on the body: nothing to scan for (driving on dry ground asks ten times a second).
            if (!_voxels.IsCreated || share <= 0f || _looseCount <= 0)
                return 0;

            VoxelDebris debris = VoxelWorld.Instance != null ? VoxelWorld.Instance.Debris : null;
            float size = VoxelSize * Mathf.Abs(transform.lossyScale.x);
            int removed = 0;
            int restored = 0;
            int thrown = 0;
            int left = 0;

            for (int i = 0; i < _voxels.Length; i++)
            {
                byte material = _voxels[i];
                if (material == 0 || !_loose[material])
                    continue;
                if (Random.value > share)
                {
                    left++;
                    continue;
                }

                byte under = _beneath != null ? _beneath[i] : (byte)0;
                if (under != 0)
                {
                    _beneath[i] = 0;
                    _stained--;
                }

                if (under != 0 && !_loose[under])
                {
                    _voxels[i] = under;
                    restored++;
                }
                else
                {
                    _voxels[i] = 0;
                    removed++;
                }

                // Only the chunks the mud fell from are redrawn, not the whole body.
                int3 cell = VoxelMath.Coord(i, Size);
                MarkDirty(cell, cell);

                if (debris == null || thrown >= debrisBudget)
                    continue;

                Vector3 position = transform.TransformPoint(Origin + ((float3)cell + 0.5f) * VoxelSize);
                Vector3 away = position - from;
                away.y = 0f;
                away = away.sqrMagnitude > 0.0001f ? away.normalized : Random.onUnitSphere;
                Vector3 velocity = InheritedVelocity(position) + Flung(away, kick, twist);
                debris.Spawn(position, velocity, _palette[material], size);
                thrown++;
            }

            _looseCount = left;
            if (removed > 0)
                SolidCount -= removed;

            return removed + restored;
        }

        /// <summary>
        /// Speed of a clod coming off: out along <paramref name="away"/> (flat), round the body by
        /// <paramref name="twist"/> (+ is anticlockwise seen from above) and a little up.
        /// </summary>
        private static Vector3 Flung(Vector3 away, float kick, float twist) =>
            away * (kick * Random.Range(0.4f, 1f)) +
            Vector3.Cross(Vector3.up, away) * (twist * Random.Range(0.5f, 1f)) +
            Vector3.up * Random.Range(0.3f, 1.2f);

        /// <summary>World-space bounds of the whole grid.</summary>
        public Bounds WorldBounds
        {
            get
            {
                Vector3 min = Origin;
                Vector3 max = (Vector3)Origin + (Vector3)(float3)Size * VoxelSize;
                Bounds bounds = new Bounds(transform.TransformPoint(min), Vector3.zero);
                for (int i = 1; i < 8; i++)
                {
                    Vector3 corner = new Vector3((i & 1) != 0 ? max.x : min.x, (i & 2) != 0 ? max.y : min.y,
                        (i & 4) != 0 ? max.z : min.z);
                    bounds.Encapsulate(transform.TransformPoint(corner));
                }

                return bounds;
            }
        }

        /// <summary>
        /// Takes a sphere out of the body — squashed to a bowl on ground (see
        /// <see cref="VoxelBodySettings.CraterDepth"/>). Returns how many voxels went.
        /// </summary>
        /// <param name="force">Speed (m/s) the debris and loose pieces leave the blast with.</param>
        /// <param name="debrisBudget">Most debris cubes this carve may throw.</param>
        public int CarveSphere(Vector3 worldCenter, float worldRadius, float force, int debrisBudget)
        {
            int taken = Carve(worldCenter, worldRadius * Toughness, _settings.CraterDepth, 0.25f, true, debrisBudget,
                worldCenter, force, Vector3.zero);

            // A settled piece a blast reaches is thrown again; a bullet only chips it. It goes as a
            // fresh piece does, after this carve's rebuild; a second blast this frame finds it loose.
            if (taken > 0 && _settings.Settle && !IsLoose && force > WAKE_FORCE &&
                _voxels.IsCreated && SolidCount > 0)
                MakeDynamic(BodyMass(), Vector3.zero, worldCenter, force);

            return taken;
        }

        /// <summary>How far an air bomb crater's rim wanders in and out, as a share of its radius.</summary>
        private const float CRATER_RIM_ROUGHNESS = 0.1f;

        /// <summary>
        /// An air bomb's crater: a bowl with no wall at its edge, nowhere steeper than
        /// <paramref name="slope"/>, so a tank that lands in it drives back out (see
        /// <see cref="CarveBowlJob"/>). The depth follows from the radius and the slope, taken at
        /// the rim's narrowest.
        /// </summary>
        /// <param name="worldCenter">Where the bomb struck the surface.</param>
        /// <param name="worldRadius">Radius of the rim.</param>
        /// <param name="slope">Steepest slope of the wall, in degrees.</param>
        /// <param name="debrisLife">How long (s, ±25 %) the clods lie before they go; 0 — the usual 25–40 s.</param>
        public int CarveCrater(Vector3 worldCenter, float worldRadius, float slope, float force, int debrisBudget,
            float debrisLife = 0f)
        {
            if (!_voxels.IsCreated || SolidCount == 0)
                return 0;

            float scale = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
            float3 center = ((float3)transform.InverseTransformPoint(worldCenter) - Origin) / VoxelSize;
            float radius = Mathf.Max(2f, worldRadius * Toughness / (scale * VoxelSize));
            // The wall is steepest halfway out, at π/2 · depth / rim.
            float narrowest = radius * (1f - CRATER_RIM_ROUGHNESS);
            float depth = Mathf.Tan(Mathf.Clamp(slope, 1f, 80f) * Mathf.Deg2Rad) * narrowest / (Mathf.PI * 0.5f);

            var removed = new NativeList<RemovedVoxel>(4096, Allocator.TempJob);
            new CarveBowlJob
            {
                Voxels = _voxels,
                Size = Size,
                Center = center,
                Radius = radius,
                Depth = depth,
                Roughness = CRATER_RIM_ROUGHNESS,
                NoiseOffset = new float2(Random.value, Random.value) * 100f,
                Removed = removed
            }.Run();

            float outer = radius * (1f + CRATER_RIM_ROUGHNESS);
            int taken = AfterCarve(removed, center, new float3(outer, depth, outer), true, worldCenter, force,
                Vector3.zero, debrisBudget, debrisLife);
            removed.Dispose();
            return taken;
        }

        /// <summary>Edge, in voxels, of the blocks a torn lump is drawn in (see <see cref="Tear"/>).</summary>
        private const int LUMP_CELL = 2;

        /// <summary>
        /// Rips a ragged lump out of the body whole — a slab of turf and the earth under it — and
        /// throws it as one debris clump: no collider, so it never gets in the way of the tank or
        /// the camera; it flies, lands, breaks up (see <see cref="VoxelDebris"/>) and is gone after
        /// <paramref name="life"/> seconds (±25 %).
        /// </summary>
        /// <param name="worldRadius">Rough radius of the lump; its edge wanders by a third.</param>
        /// <returns>How many cubes the clump got: 0 when there was too little ground there.</returns>
        public int Tear(Vector3 worldCenter, float worldRadius, Vector3 velocity, Vector3 spin, float life)
        {
            if (!_voxels.IsCreated || SolidCount == 0)
                return 0;

            float scale = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
            float3 center = ((float3)transform.InverseTransformPoint(worldCenter) - Origin) / VoxelSize;
            float radius = Mathf.Max(2f, worldRadius / (scale * VoxelSize));
            // Squashed: a lump of ground is wider than it is tall.
            float3 squash = new float3(1f, 1.35f, 1f);
            int3 reach = (int3)math.ceil(radius * 1.25f / squash) + 1;
            int3 min = math.max(0, (int3)math.floor(center) - reach);
            int3 max = math.min(Size - 1, (int3)math.floor(center) + reach);
            if (math.any(max < min))
                return 0;

            int3 size = max - min + 1;
            float3 offset = new float3(Random.value, Random.value, Random.value) * 100f;
            float frequency = 1.6f / radius;
            var grid = new NativeArray<byte>(size.x * size.y * size.z, Allocator.TempJob);
            var bounds = new NativeArray<int3>(3, Allocator.TempJob);

            // The lump's shape, in Burst: scanning it here with a noise call per voxel took about
            // 10 ms a lump, 80 ms for an air bomb's eight.
            new TearScanJob
            {
                Voxels = _voxels,
                Size = Size,
                Min = min,
                Extent = size,
                Center = center,
                Squash = squash,
                Radius = radius,
                Frequency = frequency,
                Offset = offset,
                Grid = grid,
                Result = bounds
            }.Run();

            int count = bounds[0].x;
            int3 lo = bounds[1];
            int3 hi = bounds[2];
            bounds.Dispose();

            if (count <= CRUMBLE_MAX * 4)
            {
                grid.Dispose();
                return 0;
            }

            VoxelDebris debris = VoxelWorld.Instance.Debris;
            int cubes = 0;
            int3 blocks = (hi - lo) / LUMP_CELL + 1;
            if (debris != null && debris.HasRoom(blocks.x * blocks.y * blocks.z))
            {
                // The middle of the lump, in world space: the clump turns round it.
                float3 mid = Origin + ((float3)(min + lo) + (float3)(hi - lo + 1) * 0.5f) * VoxelSize;
                int clump = debris.BeginCluster(transform.TransformPoint(mid), velocity, spin.magnitude);
                debris.SetLife(clump, Random.Range(life * 0.75f, life * 1.25f));
                float cube = LUMP_CELL * VoxelSize * scale;

                // One cube per LUMP_CELL³ block that is at least a third full, in the colour of its
                // topmost voxel (turf stays on top): a lump is a few hundred cubes, not thousands.
                for (int bz = 0; bz < blocks.z; bz++)
                for (int by = 0; by < blocks.y; by++)
                for (int bx = 0; bx < blocks.x; bx++)
                {
                    int3 from = lo + new int3(bx, by, bz) * LUMP_CELL;
                    int3 to = math.min(from + LUMP_CELL - 1, hi);
                    int solid = 0;
                    byte top = 0;
                    for (int y = to.y; y >= from.y; y--)
                    for (int z = from.z; z <= to.z; z++)
                    for (int x = from.x; x <= to.x; x++)
                    {
                        byte material = grid[VoxelMath.Index(new int3(x, y, z), size)];
                        if (material == 0)
                            continue;
                        solid++;
                        if (top == 0)
                            top = material;
                    }

                    if (solid * 3 < LUMP_CELL * LUMP_CELL * LUMP_CELL)
                        continue;

                    float3 local = Origin + ((float3)(min + from) + LUMP_CELL * 0.5f) * VoxelSize;
                    debris.AddCube(clump, transform.TransformPoint(local), cube, _palette[top]);
                    cubes++;
                }
            }

            // Out of the ground whether it flew or there was no room for it: the hole is the same.
            new TearClearJob { Voxels = _voxels, Size = Size, Min = min, Extent = size, Lo = lo, Hi = hi, Grid = grid }.Run();

            grid.Dispose();
            SolidCount -= count;
            MarkDirty(min + lo, min + hi);
            return cubes;
        }

        /// <summary>Marks the voxels of a torn lump in a grid of its own; Result: (count,0,0), lo, hi.</summary>
        [BurstCompile(DisableSafetyChecks = true)]
        private struct TearScanJob : IJob
        {
            [ReadOnly] public NativeArray<byte> Voxels;
            public int3 Size;
            public int3 Min;
            public int3 Extent;
            public float3 Center;
            public float3 Squash;
            public float Radius;
            public float Frequency;
            public float3 Offset;
            public NativeArray<byte> Grid;
            public NativeArray<int3> Result;

            public void Execute()
            {
                int count = 0;
                int3 lo = Extent;
                int3 hi = -1;

                for (int z = 0; z < Extent.z; z++)
                for (int y = 0; y < Extent.y; y++)
                for (int x = 0; x < Extent.x; x++)
                {
                    int3 cell = Min + new int3(x, y, z);
                    byte material = Voxels[VoxelMath.Index(cell, Size)];
                    if (material == 0)
                        continue;

                    float3 d = ((float3)cell + 0.5f - Center) * Squash;
                    float edge = Radius * (0.85f + 0.35f * noise.snoise((float3)cell * Frequency + Offset));
                    if (math.lengthsq(d) > edge * edge)
                        continue;

                    Grid[VoxelMath.Index(new int3(x, y, z), Extent)] = material;
                    count++;
                    lo = math.min(lo, new int3(x, y, z));
                    hi = math.max(hi, new int3(x, y, z));
                }

                Result[0] = new int3(count, 0, 0);
                Result[1] = lo;
                Result[2] = hi;
            }
        }

        /// <summary>Empties the grid where a torn lump was.</summary>
        [BurstCompile(DisableSafetyChecks = true)]
        private struct TearClearJob : IJob
        {
            public NativeArray<byte> Voxels;
            public int3 Size;
            public int3 Min;
            public int3 Extent;
            public int3 Lo;
            public int3 Hi;
            [ReadOnly] public NativeArray<byte> Grid;

            public void Execute()
            {
                for (int z = Lo.z; z <= Hi.z; z++)
                for (int y = Lo.y; y <= Hi.y; y++)
                for (int x = Lo.x; x <= Hi.x; x++)
                    if (Grid[VoxelMath.Index(new int3(x, y, z), Extent)] != 0)
                        Voxels[VoxelMath.Index(Min + new int3(x, y, z), Size)] = 0;
            }
        }

        /// <summary>
        /// Scoops a small bite out without looking for loose pieces (a track's rut): what it takes
        /// is thrown along <paramref name="throwVelocity"/> as clods.
        /// </summary>
        public int Dig(Vector3 worldCenter, float worldRadius, Vector3 throwVelocity, int debrisBudget) =>
            Carve(worldCenter, worldRadius, 1f, 0.1f, false, debrisBudget, worldCenter, 0f, throwVelocity);

        private int Carve(Vector3 worldCenter, float worldRadius, float depth, float roughness, bool split,
            int debrisBudget, Vector3 blastCenter, float force, Vector3 throwVelocity, float debrisLife = 0f)
        {
            if (!_voxels.IsCreated || SolidCount == 0)
                return 0;

            float scale = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
            float3 local = transform.InverseTransformPoint(worldCenter);
            float3 center = (local - Origin) / VoxelSize;
            float radius = Mathf.Max(0.75f, worldRadius / (scale * VoxelSize));
            float3 reach = new float3(radius, radius * depth, radius);

            var removed = new NativeList<RemovedVoxel>(256, Allocator.TempJob);
            new CarveSphereJob
            {
                Voxels = _voxels,
                Size = Size,
                Center = center,
                Radius = radius,
                Squash = 1f / math.max(0.05f, depth),
                Roughness = roughness,
                Seed = _seed++,
                Removed = removed
            }.Run();

            int taken = AfterCarve(removed, center, reach, split, blastCenter, force, throwVelocity, debrisBudget,
                debrisLife);
            removed.Dispose();
            return taken;
        }

        /// <summary>
        /// What every carve does once its voxels are out: count them, rebuild the chunks round
        /// <paramref name="center"/> ± <paramref name="reach"/> (grid units), throw the debris and
        /// break off whatever no longer hangs on.
        /// </summary>
        private int AfterCarve(NativeList<RemovedVoxel> removed, float3 center, float3 reach, bool split,
            Vector3 blastCenter, float force, Vector3 throwVelocity, int debrisBudget, float debrisLife)
        {
            int taken = removed.Length;
            if (taken > 0)
            {
                SolidCount -= taken;
                int3 lo = math.max(0, (int3)math.floor(center - reach));
                int3 hi = math.min(Size - 1, (int3)math.floor(center + reach));
                MarkDirty(lo, hi);
                ThrowDebris(removed, blastCenter, force, throwVelocity, debrisBudget, debrisLife);
            }

            if (taken > 0 && split && _settings.Split)
            {
                // A small bite (a bullet's hole, a chip) looks for what broke off at the end of the frame,
                // once for every bite this frame: Split labels the whole grid (1.3-1.6 ms on a house), and a
                // machine-gun bullet bit 3 times a wall, up to 3 walls.
                if (taken <= DEFER_SPLIT_MAX && SolidCount > 0)
                    DeferSplit(blastCenter, force, center, reach);
                else
                {
                    RebuildNow();
                    Split(blastCenter, force);
                }
            }
            else if (SolidCount <= 0)
                Destroy(gameObject);

            // A building that lost voxels works out its load again at the end of the frame.
            if (taken > 0 && split && SolidCount > 0)
                MarkStructureDirtyAt(center, reach, false, taken <= DEFER_SPLIT_MAX ? MINOR_SOLVE_DELAY : 0f);

            return taken;
        }

        /// <summary>
        /// Throws what a carve took out as clods: neighbouring voxels fly off together as one
        /// clump (cells of CLUMP³ voxels), a share as single crumbs. The spray is a narrow
        /// fountain — mostly up, a little outwards — not a burst in every direction.
        /// </summary>
        private void ThrowDebris(NativeList<RemovedVoxel> removed, Vector3 center, float force, Vector3 throwVelocity,
            int budget, float life = 0f)
        {
            VoxelDebris debris = VoxelWorld.Instance.Debris;
            if (debris == null || budget <= 0 || removed.Length == 0)
                return;

            float chance = Mathf.Min(1f, budget / (float)removed.Length);
            float size = VoxelSize * Mathf.Abs(transform.lossyScale.x);
            Vector3 inherited = InheritedVelocity(center);
            int3 jitter = new int3(Random.Range(0, CLUMP), Random.Range(0, CLUMP), Random.Range(0, CLUMP));

            Clumps.Clear();
            Matrix4x4 toWorld = transform.localToWorldMatrix;
            // A bomb's crater takes out tens of thousands of voxels for a 2600-cube fountain: the
            // kept ones are picked by a random stride, not by a dice roll for every voxel.
            int step = chance < 1f ? Mathf.Max(1, Mathf.RoundToInt(1f / chance)) : 1;
            for (int i = step > 1 ? Random.Range(0, step) : 0; i < removed.Length; i += step > 1 ? Random.Range(1, 2 * step) : 1)
            {
                RemovedVoxel voxel = removed[i];
                Vector3 position = toWorld.MultiplyPoint3x4(Origin + ((float3)voxel.Position + 0.5f) * VoxelSize);

                if (Random.value < CRUMB_SHARE)
                {
                    Keep(debris, debris.Spawn(position, inherited + Fling(position, center, force, throwVelocity, 0.6f),
                        _palette[voxel.Material], size));
                    continue;
                }

                int3 cell = (voxel.Position + jitter) / CLUMP;
                if (!Clumps.TryGetValue(cell, out List<int> members))
                {
                    members = ClumpPool.Count > 0 ? ClumpPool.Pop() : new List<int>();
                    Clumps[cell] = members;
                }

                members.Add(i);
            }

            foreach (List<int> members in Clumps.Values)
            {
                Vector3 middle = Vector3.zero;
                foreach (int i in members)
                    middle += toWorld.MultiplyPoint3x4(Origin + ((float3)removed[i].Position + 0.5f) * VoxelSize);
                middle /= members.Count;

                int clump = debris.BeginCluster(middle, inherited + Fling(middle, center, force, throwVelocity, 1f));
                if (life > 0f)
                    debris.SetLife(clump, Random.Range(life * 0.75f, life * 1.25f));
                Keep(debris, clump);
                foreach (int i in members)
                {
                    Vector3 position = toWorld.MultiplyPoint3x4(Origin + ((float3)removed[i].Position + 0.5f) * VoxelSize);
                    debris.AddCube(clump, position, size, _palette[removed[i].Material]);
                }

                members.Clear();
                ClumpPool.Push(members);
            }

            Clumps.Clear();
        }

        /// <summary>
        /// A clod's launch: a steep cone round "up and away from the blast", a third to two
        /// thirds of the blast's speed; or the given throw (a track's), with a little scatter.
        /// </summary>
        private static Vector3 Fling(Vector3 position, Vector3 center, float force, Vector3 throwVelocity, float speedShare)
        {
            if (throwVelocity != Vector3.zero)
                return throwVelocity * Random.Range(0.7f, 1.1f) + Random.insideUnitSphere * 0.4f;

            Vector3 away = position - center;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.up;
            Vector3 direction = (away * 0.45f + Vector3.up + Random.insideUnitSphere * 0.15f).normalized;
            return direction * (force * speedShare * Random.Range(0.35f, 0.7f));
        }

        private const string PIECE_NAME = "VoxelPiece";
        private const string CHUNK_NAME = "Chunk";

        /// <summary>Collision cell (voxels) of a building's pieces, at least.</summary>
        private const int PIECE_COLLIDER_CELL = 3;

        /// <summary>Carves that took at most this many voxels split at the end of the frame (see DeferSplit).</summary>
        private const int DEFER_SPLIT_MAX = 600;

        private bool _splitPending;
        private Vector3 _splitCenter;
        private float _splitForce;

        /// <summary>
        /// Asks for a Split at the end of this frame (VoxelWorld, before the load solve and the rebuild):
        /// what broke off still goes in this frame, but one labelling serves every bite the frame took.
        /// </summary>
        private void DeferSplit(Vector3 blastCenter, float force, float3 center, float3 reach)
        {
            // Where the bites were: a standing building looks round them only (LocalSplit).
            if (_splitRegions < SPLIT_REGIONS)
            {
                _splitLo[_splitRegions] = (int3)math.floor(center - reach) - 1;
                _splitHi[_splitRegions] = (int3)math.ceil(center + reach) + 1;
            }

            _splitRegions++;
            if (!_splitPending || force >= _splitForce)
            {
                _splitCenter = blastCenter;
                _splitForce = force;
            }

            if (_splitPending)
                return;
            _splitPending = true;
            VoxelWorld.QueueSplit(this);
        }

        /// <summary>Runs the Split asked for this frame, if any (VoxelWorld).</summary>
        internal void RunDeferredSplit()
        {
            if (!_splitPending)
                return;
            _splitPending = false;
            int regions = _splitRegions;
            _splitRegions = 0;
            if (_voxels.IsCreated && SolidCount > 0)
            {
                if (IsStatic && regions <= SPLIT_REGIONS)
                    LocalSplit(regions, _splitCenter, _splitForce);
                else
                    Split(_splitCenter, _splitForce);
            }

            _splitForce = 0f;
        }

        /// <summary>Bites a frame a building looks round on its own (LocalSplit); more, and it labels the whole grid.</summary>
        private const int SPLIT_REGIONS = 16;

        private readonly int3[] _splitLo = new int3[SPLIT_REGIONS];
        private readonly int3[] _splitHi = new int3[SPLIT_REGIONS];
        private int _splitRegions;

        /// <summary>A search from a bite that gets this big holds on (the load solve finds a bigger piece that does not).</summary>
        private const int LOCAL_SPLIT_BUDGET = 200000;

        /// <summary>
        /// Split for a standing building's small bites: a flood from the voxels round each bite,
        /// stopped as soon as it reaches the bottom layer or grows past LOCAL_SPLIT_BUDGET (it holds
        /// on). What closes off before either is a scrap the bites cut loose: it goes as in Split.
        /// A bullet's hole costs a few thousand voxels' search, not the house's whole grid.
        /// </summary>
        private void LocalSplit(int regions, Vector3 blastCenter, float force)
        {
            var lo = new NativeArray<int3>(regions, Allocator.TempJob);
            var hi = new NativeArray<int3>(regions, Allocator.TempJob);
            for (int i = 0; i < regions; i++)
            {
                lo[i] = math.max(_splitLo[i], 0);
                hi[i] = math.min(_splitHi[i], Size - 1);
            }

            var labels = new NativeArray<int>(_voxels.Length, Allocator.TempJob);
            var queue = new NativeList<int>(4096, Allocator.TempJob);
            var components = new NativeList<VoxelComponent>(8, Allocator.TempJob);
            new LocalSplitJob
            {
                Voxels = _voxels, Size = Size, Lo = lo, Hi = hi, Budget = LOCAL_SPLIT_BUDGET, Labels = labels, Queue = queue,
                Components = components
            }.Run();

            bool any = false;
            for (int i = 0; i < components.Length; i++)
            {
                VoxelComponent piece = components[i];
                if (piece.Grounded)
                    continue;
                any = true;
                if (piece.Count <= CRUMBLE_MAX)
                    Crumble(labels, i + 1, piece, blastCenter, force);
                else
                    Detach(labels, i + 1, piece, blastCenter, force);
                MarkDirty(piece.Min, piece.Max);
            }

            if (any)
                RebuildNow();

            lo.Dispose();
            hi.Dispose();
            labels.Dispose();
            queue.Dispose();
            components.Dispose();

            if (SolidCount <= 0)
                Destroy(gameObject);
        }

        /// <summary>
        /// Finds the pieces that no longer hold on and takes them out of this body.
        /// </summary>
        private void Split(Vector3 blastCenter, float force)
        {
            int count = _voxels.Length;
            var labels = new NativeArray<int>(count, Allocator.TempJob);
            var queue = new NativeArray<int>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var components = new NativeList<VoxelComponent>(8, Allocator.TempJob);

            var noCells = new NativeArray<int>(1, Allocator.TempJob);
            new LabelComponentsJob
            {
                Voxels = _voxels, Size = Size, Labels = labels, Queue = queue, Components = components, Cells = noCells
            }.Run();
            noCells.Dispose();

            // A body that already flies on its own keeps its biggest piece whatever its anchor.
            bool ground = _settings.Anchor == VoxelAnchor.Ground && _rigidbody == null;
            int largest = -1;
            for (int i = 0; i < components.Length; i++)
                if (largest < 0 || components[i].Count > components[largest].Count)
                    largest = i;

            for (int i = 0; i < components.Length; i++)
            {
                VoxelComponent piece = components[i];
                bool stays = ground ? piece.Grounded : i == largest;
                if (stays)
                    continue;

                if (piece.Count <= CRUMBLE_MAX)
                    Crumble(labels, i + 1, piece, blastCenter, force);
                else
                    Detach(labels, i + 1, piece, blastCenter, force);

                MarkDirty(piece.Min, piece.Max);
            }

            labels.Dispose();
            queue.Dispose();
            components.Dispose();

            if (SolidCount <= 0)
                Destroy(gameObject);
            else
                UpdateMass();
        }

        /// <summary>A scrap too small to be a body drops off as one clod, which breaks where it lands.</summary>
        private void Crumble(NativeArray<int> labels, int label, VoxelComponent piece, Vector3 blastCenter, float force)
        {
            VoxelDebris debris = VoxelWorld.Instance.Debris;
            float size = VoxelSize * Mathf.Abs(transform.lossyScale.x);
            Vector3 middle = transform.TransformPoint(Origin + ((float3)(piece.Min + piece.Max) * 0.5f + 0.5f) * VoxelSize);
            Vector3 away = (middle - blastCenter).normalized;
            int clump = debris != null ? debris.BeginCluster(middle, InheritedVelocity(middle) + away * (force * 0.2f)) : -1;
            Keep(debris, clump);

            for (int z = piece.Min.z; z <= piece.Max.z; z++)
            for (int y = piece.Min.y; y <= piece.Max.y; y++)
            for (int x = piece.Min.x; x <= piece.Max.x; x++)
            {
                int index = VoxelMath.Index(new int3(x, y, z), Size);
                if (labels[index] != label)
                    continue;

                Vector3 position = transform.TransformPoint(Origin + new float3(x + 0.5f, y + 0.5f, z + 0.5f) * VoxelSize);
                debris?.AddCube(clump, position, size, _palette[_voxels[index]]);
                _voxels[index] = 0;
            }

            SolidCount -= piece.Count;
        }

        /// <summary>
        /// Moves a piece into a new body of its own, placed exactly where it was, with a Rigidbody.
        /// It stays kinematic until its colliders are built (end of this frame), so it cannot fall
        /// through the floor in a physics step that runs before then.
        /// </summary>
        private void Detach(NativeArray<int> labels, int label, VoxelComponent piece, Vector3 blastCenter, float force)
        {
            // A piece leaves: the rest's colliders must be right before the piece moves (RebuildsAsync).
            RebuildNow();
            int3 size = piece.Max - piece.Min + 1;
            var grid = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent);

            new ExtractComponentJob
            {
                Source = _voxels,
                Labels = labels,
                SourceSize = Size,
                Label = label,
                Min = piece.Min,
                Target = grid,
                TargetSize = size
            }.Run();

            SolidCount -= piece.Count;

            // Fixed names: a name built from the parent's grew a string per piece ("House_Piece_Piece…").
            var go = new GameObject(PIECE_NAME) { layer = VoxelWorld.PieceLayer };
            go.transform.SetPositionAndRotation(transform.position, transform.rotation);
            go.transform.localScale = transform.lossyScale;

            VoxelBodySettings settings = _settings;
            settings.Origin = Origin + (float3)piece.Min * VoxelSize;
            settings.Anchor = VoxelAnchor.Largest;
            settings.Colliders = true;
            settings.Layer = VoxelWorld.PieceLayer;
            // A building's rubble collides in 30 cm cells at least: a third of the boxes (and their
            // components) of 20 cm ones, which a falling storey made by the hundred in one frame.
            if (HasStrength)
                settings.ColliderCell = math.max(settings.ColliderCell, PIECE_COLLIDER_CELL);
            settings.Material = _material;

            VoxelBody body = go.AddComponent<VoxelBody>();
            body.Init(size, grid, new NativeArray<Color32>(_palette, Allocator.Persistent), settings);
            if (HasStrength)
                body.SetStrength(CopyStrength());
            body.Generation = Generation + 1;

            // The piece goes on at the speed its own middle had, not the blast's point: a spinning
            // body hit far from its centre would hand the piece ω × r on top of its speed.
            Vector3 middle = transform.TransformPoint(Origin + ((float3)(piece.Min + piece.Max) * 0.5f + 0.5f) * VoxelSize);
            body.MakeDynamic(body.BodyMass(), InheritedVelocity(middle), blastCenter, force);
        }

        /// <summary>
        /// Breaks a loose body apart at a point of impact, RayFire style but on the grid: Voronoi
        /// cells, most of their seeds packed round the impact (small pieces there, big ones
        /// further off), jagged borders. The biggest piece stays this body; pieces big enough fly
        /// on as bodies of their own (and can break again), smaller ones drop as clods.
        /// </summary>
        /// <param name="cells">How many pieces to aim for.</param>
        /// <param name="spread">Speed (m/s) the pieces push apart with, away from the impact.</param>
        /// <returns>False when the body is too small to break, or not loose.</returns>
        /// <param name="scatter">Seeds all through the body, none packed at the impact: it breaks up evenly (a storey landing hard).</param>
        public bool Fracture(Vector3 worldPoint, int cells, float spread, bool scatter = false)
        {
            if (!_voxels.IsCreated || !IsLoose || SolidCount <= CRUMBLE_MAX || cells < 2)
                return false;

            float3 impact = ((float3)transform.InverseTransformPoint(worldPoint) - Origin) / VoxelSize;
            impact = math.clamp(impact, 0f, (float3)Size - 0.01f);
            var seeds = new NativeArray<float3>(cells, Allocator.TempJob);
            float near = math.max(2f, math.cmax((float3)Size) * 0.25f);
            for (int i = 0; i < cells; i++)
            {
                // Two seeds in three round the impact, the rest anywhere in the body; a building's
                // piece, one in two: it breaks all through, along its seams, not only where it hit.
                bool round = !scatter && (HasStrength ? i % 2 == 0 : i % 3 != 2);
                seeds[i] = round
                    ? math.clamp(impact + (float3)(Random.insideUnitSphere * near), 0f, (float3)Size)
                    : new float3(Random.value * Size.x, Random.value * Size.y, Random.value * Size.z);
            }

            int count = _voxels.Length;
            NativeArray<int> cellOf;
            // A building's piece parts along its weak joints (floor from wall, pane from frame);
            // anything else along jagged Voronoi borders.
            if (HasStrength)
            {
                cellOf = SeamCells(seeds);
            }
            else
            {
                cellOf = new NativeArray<int>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                new VoronoiCellsJob
                {
                    Voxels = _voxels, Seeds = seeds, Size = Size, Jitter = 1.6f, Seed = _seed++, Cells = cellOf
                }.Run();
            }

            // A building's piece crumbles along its cracks: much of what lines them goes as crumbs.
            if (HasStrength)
                CrumbleSeams(cellOf, worldPoint, spread);

            var labels = new NativeArray<int>(count, Allocator.TempJob);
            var queue = new NativeArray<int>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var components = new NativeList<VoxelComponent>(cells * 2, Allocator.TempJob);
            new LabelComponentsJob
            {
                Voxels = _voxels, Size = Size, Labels = labels, Queue = queue, Components = components,
                Cells = cellOf, UseCells = true
            }.Run();

            int largest = 0;
            for (int i = 1; i < components.Length; i++)
                if (components[i].Count > components[largest].Count)
                    largest = i;

            for (int i = 0; i < components.Length; i++)
            {
                VoxelComponent piece = components[i];
                // A building's small parts go as clods (they burst where they land), not as bodies.
                int bodyMin = HasStrength ? STRUCTURE_BODY_MIN : FRACTURE_BODY_MIN;
                if (i == largest && piece.Count >= bodyMin)
                    continue;

                if (piece.Count < bodyMin)
                    Crumble(labels, i + 1, piece, worldPoint, spread * 2f);
                else
                    Detach(labels, i + 1, piece, worldPoint, spread);
                MarkDirty(piece.Min, piece.Max);
            }

            seeds.Dispose();
            cellOf.Dispose();
            labels.Dispose();
            queue.Dispose();
            components.Dispose();

            if (SolidCount <= 0)
            {
                Destroy(gameObject);
                return true;
            }

            UpdateMass();
            return true;
        }

        /// <summary>
        /// Sticks mud onto the body in voxels: empty cells in a sphere that touch the body's
        /// surface fill up with the colour, each with the given chance, and surface voxels there
        /// are stained with it. Mud a blast later knocks off like any other voxel.
        /// </summary>
        public int Deposit(Vector3 worldCenter, float worldRadius, Color32 color, float chance)
        {
            if (!_voxels.IsCreated || SolidCount == 0)
                return 0;

            byte material = PaletteSlot(color, true);
            if (material == 0)
                return 0;

            float scale = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
            float3 center = ((float3)transform.InverseTransformPoint(worldCenter) - Origin) / VoxelSize;
            float radius = Mathf.Max(0.75f, worldRadius / (scale * VoxelSize));
            int3 lo = math.max(0, (int3)math.floor(center - radius));
            int3 hi = math.min(Size - 1, (int3)math.floor(center + radius));
            uint seed = _seed++;
            int added = 0;
            int stained = 0;

            for (int z = lo.z; z <= hi.z; z++)
            for (int y = lo.y; y <= hi.y; y++)
            for (int x = lo.x; x <= hi.x; x++)
            {
                int3 p = new int3(x, y, z);
                if (math.distancesq((float3)p + 0.5f, center) > radius * radius || VoxelMath.Hash01(p, seed) > chance)
                    continue;

                int index = VoxelMath.Index(p, Size);
                byte current = _voxels[index];
                if (current != 0)
                {
                    if (current != material && Touches(p, 0, 0))
                    {
                        // The plate under the mud is remembered, as under a stain (Paint): shaken
                        // off, the mud gives the plate back rather than taking the voxel with it.
                        if (!IsStatic && !_loose[current])
                        {
                            _beneath ??= new byte[_voxels.Length];
                            if (_beneath[index] == 0)
                            {
                                _beneath[index] = current;
                                _stained++;
                            }
                        }

                        _voxels[index] = material;
                        stained++;
                    }

                    continue;
                }

                // Mud clings to the body: next to its own voxels, never in mid-air and never on mud
                // of the same shade, so it builds one layer rather than a ball.
                if (!Touches(p, 1, material))
                    continue;

                _voxels[index] = material;
                added++;
            }

            SolidCount += added;
            _looseCount += added + stained;
            if (added + stained > 0)
                MarkDirty(lo, hi);
            return added;
        }

        /// <summary>
        /// A random voxel on the body's surface (solid, with an empty neighbour), in world space.
        /// <paramref name="lowBias"/> above 1 favours the bottom of the body: 1 is even, 2 puts
        /// three quarters of the picks in the lower half.
        /// </summary>
        public bool TryGetSurfacePoint(float lowBias, out Vector3 world) => TryGetSurfacePoint(lowBias, out world, out _);

        /// <summary>
        /// As <see cref="TryGetSurfacePoint(float, out Vector3)"/>, with the way the surface faces
        /// there (world space, unit): toward its empty neighbours. Zero where they lie on opposite
        /// sides alike — a part one voxel thin faces both ways.
        /// </summary>
        public bool TryGetSurfacePoint(float lowBias, out Vector3 world, out Vector3 normal)
        {
            world = default;
            normal = default;
            if (!_voxels.IsCreated || SolidCount == 0)
                return false;

            for (int attempt = 0; attempt < 48; attempt++)
            {
                var p = new int3(Random.Range(0, Size.x), (int)(Mathf.Pow(Random.value, lowBias) * Size.y),
                    Random.Range(0, Size.z));
                p.y = math.min(p.y, Size.y - 1);
                if (_voxels[VoxelMath.Index(p, Size)] == 0 || !Touches(p, 0, 0))
                    continue;

                world = transform.TransformPoint(Origin + ((float3)p + 0.5f) * VoxelSize);

                float3 open = float3.zero;
                for (int d = 0; d < 6; d++)
                {
                    int3 q = p;
                    int step = (d & 1) == 0 ? 1 : -1;
                    q[d >> 1] += step;
                    if (!VoxelMath.Inside(q, Size) || _voxels[VoxelMath.Index(q, Size)] == 0)
                        open[d >> 1] += step;
                }

                if (math.lengthsq(open) > 0f)
                    normal = transform.TransformDirection(math.normalize(open));
                return true;
            }

            return false;
        }

        /// <summary>
        /// For <paramref name="want"/> = 1: whether a neighbour is solid (a cell on the surface,
        /// outside). For 0: whether a neighbour is empty (a voxel on the surface, inside).
        /// </summary>
        private bool Touches(int3 p, int want, byte ignore)
        {
            for (int d = 0; d < 6; d++)
            {
                int3 q = p;
                q[d >> 1] += (d & 1) == 0 ? 1 : -1;
                byte neighbour = VoxelMath.Inside(q, Size) ? _voxels[VoxelMath.Index(q, Size)] : (byte)0;
                bool solid = neighbour != 0 && (ignore == 0 || neighbour != ignore);
                if (solid == (want == 1))
                    return true;
            }

            return false;
        }

        private void UpdateMass()
        {
            if (_rigidbody != null)
                _rigidbody.mass = BodyMass();
        }

        /// <summary>The body's own mass: a building's by its materials (VoxelLoad.GroupDensity), any other by its density.</summary>
        private float BodyMass()
        {
            if (!HasStrength || !_voxels.IsCreated)
                return MassOf(SolidCount);

            float edge = VoxelSize * Mathf.Abs(transform.lossyScale.x);
            using var mass = new NativeReference<float>(Allocator.TempJob);
            new BodyMassJob { Voxels = _voxels, Strength = _strength, VoxelVolume = edge * edge * edge, Mass = mass }.Run();
            return Mathf.Max(1f, mass.Value);
        }

        private float MassOf(int voxels)
        {
            float edge = VoxelSize * Mathf.Abs(transform.lossyScale.x);
            return Mathf.Max(1f, voxels * edge * edge * edge * _settings.Density);
        }

        private void Keep(VoxelDebris debris, int cluster)
        {
            if (_settings.KeepDebris && debris != null && cluster >= 0)
                debris.SetKeep(cluster);
        }

        /// <summary>
        /// Readies the body to go loose at the end of this frame (FinishRebuild): a new piece gets
        /// its Rigidbody and the components a loose piece carries; a settled one (Freeze) already
        /// has them, kinematic, and is only let go again.
        /// </summary>
        private void MakeDynamic(float mass, Vector3 velocity, Vector3 blastCenter, float force)
        {
            if (_rigidbody == null)
            {
                _rigidbody = gameObject.AddComponent<Rigidbody>();
                _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                _rigidbody.maxDepenetrationVelocity = 3f;
                _rigidbody.maxAngularVelocity = MAX_SPIN;

                if (!TryGetComponent(out VoxelImpactFracture added))
                    added = gameObject.AddComponent<VoxelImpactFracture>();
                if (_settings.FractureSpeed > 0f)
                    added.MinSpeed = _settings.FractureSpeed;
                if (_settings.Settle && !TryGetComponent(out VoxelSettle _))
                    gameObject.AddComponent<VoxelSettle>();
            }

            _rigidbody.mass = mass;
            _rigidbody.isKinematic = true;
            // A woken piece starts out lying in what it lay on, as a new one starts out in its
            // neighbours: it cannot break on that first touch either.
            if (TryGetComponent(out VoxelImpactFracture fracture))
                fracture.Arm();

            _activatePending = true;
            _pendingVelocity = velocity;
            _pendingBlastCenter = blastCenter;
            _pendingBlastForce = force;
        }

        /// <summary>The velocity of whatever carries this body: its own Rigidbody, or a vehicle's.</summary>
        private Vector3 InheritedVelocity(Vector3 point)
        {
            // Launched this frame and still kinematic: its pieces take the speed it is about to get.
            if (_rigidbody != null && _activatePending)
                return _pendingVelocity;

            Rigidbody body = _rigidbody != null ? _rigidbody : GetComponentInParent<Rigidbody>();
            return body != null && !body.isKinematic ? body.GetPointVelocity(point) : Vector3.zero;
        }

        /// <param name="shape">False when only colours changed (Paint, Wash): the chunk is redrawn, its collider kept.</param>
        private void MarkDirty(int3 lo, int3 hi, bool shape = true)
        {
            // A face on a chunk border belongs to the neighbour too: widen by one voxel.
            int3 c0 = math.max(0, (lo - 1) / _chunk);
            int3 c1 = math.min(_chunkCount - 1, (hi + 1) / _chunk);

            for (int z = c0.z; z <= c1.z; z++)
            for (int y = c0.y; y <= c1.y; y++)
            for (int x = c0.x; x <= c1.x; x++)
            {
                int index = x + _chunkCount.x * (y + _chunkCount.y * z);
                if (shape)
                    _shapeDirty[index] = true;
                if (_dirty[index])
                    continue;
                _dirty[index] = true;
                _dirtyCount++;
            }
        }

        /// <summary>
        /// Once it stands, a body remeshes without the frame waiting for it when nothing waits on
        /// its colliders: the ground, smooth or blocky (its mesh collider is swapped in when
        /// cooked), and a skin without colliders (the tank's, muddied and washed as it drives). Each job gets its own
        /// copy of the voxels round its chunk, the grid is carved on meanwhile, and the result is
        /// put in when it is done, a frame or two later. Buildings and loose pieces stay in step:
        /// a piece is let go only once its box colliders exist. A skin whose shape changed (a hit
        /// that knocks a piece off, mud piling on) is redrawn in step too: the piece must not fly
        /// off while the hull still shows it.
        /// </summary>
        internal bool RebuildsAsync => _built &&
                                       (_settings.Smooth || _settings.Terrain || (!_settings.Colliders && !HasShapeDirty()) ||
                                        // A standing building nibbled (a bullet's hole) remeshes on workers, its collider a
                                        // frame behind; after a blast or a piece breaking off it rebuilds in the frame, so
                                        // the piece never starts inside the old collider.
                                        (IsStatic && Time.frameCount > _rebuildNowFrame));

        /// <summary>The last frame something big changed this body (a blast, a piece off): it rebuilds in that frame.</summary>
        private int _rebuildNowFrame = -1;

        /// <summary>This frame's change must be in the colliders by its end (see RebuildsAsync).</summary>
        internal void RebuildNow() => _rebuildNowFrame = Time.frameCount;

        private bool HasShapeDirty()
        {
            for (int i = 0; i < _shapeDirty.Length; i++)
                if (_shapeDirty[i] && _dirty[i])
                    return true;
            return false;
        }

        /// <summary>
        /// A remesh that was dropped unapplied (the world went away): the chunk is free and dirty again.
        /// </summary>
        internal void CancelAsync(int chunk, bool shape)
        {
            if (_busy == null || chunk < 0 || chunk >= _busy.Length)
                return;
            _busy[chunk] = false;
            if (shape)
                _shapeDirty[chunk] = true;
            if (!_dirty[chunk])
            {
                _dirty[chunk] = true;
                _dirtyCount++;
            }
        }

        /// <summary>Where the next frame's async scheduling starts: chunks re-dirtied every frame cannot starve the rest.</summary>
        private int _asyncCursor;

        /// <summary>
        /// Starts remeshing dirty chunks that are not already being remeshed, at most
        /// <paramref name="budget"/> of them: a crater's dozens of chunks go over a few frames.
        /// </summary>
        internal void ScheduleAsync(List<ChunkJob> jobs, ref int budget, int frame)
        {
            bool smooth = _settings.Smooth;
            int count = _dirty.Length;
            int start = _asyncCursor % count;
            for (int k = 0; k < count && budget > 0; k++)
            {
                int index = (start + k) % count;
                if (!_dirty[index] || _busy[index])
                    continue;
                _asyncCursor = index + 1;

                int3 chunk = new int3(index % _chunkCount.x, index / _chunkCount.x % _chunkCount.y,
                    index / (_chunkCount.x * _chunkCount.y));
                int3 min = chunk * _chunk;
                int3 max = math.min(min + _chunk, Size);
                int3 offset;
                int3 dims;
                if (smooth)
                    SurfaceNetsJob.RegionOf(min, max, Size, out offset, out dims);
                else
                    GreedyMeshJob.RegionOf(min, max, Size, out offset, out dims);

                var region = new NativeArray<byte>(dims.x * dims.y * dims.z, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                new CopyRegionJob { Voxels = _voxels, Size = Size, Offset = offset, Dims = dims, Region = region }.Run();

                var job = new ChunkJob
                {
                    Body = this,
                    Chunk = index,
                    Vertices = new NativeList<VoxelVertex>(2048, Allocator.Persistent),
                    Indices = new NativeList<int>(3072, Allocator.Persistent),
                    Region = region,
                    Palette = new NativeArray<Color32>(_palette, Allocator.Persistent),
                    Shape = _shapeDirty[index],
                    Async = true,
                    Frame = frame
                };

                job.Handle = smooth
                    ? new SurfaceNetsJob
                    {
                        Voxels = job.Region,
                        Palette = job.Palette,
                        Size = Size,
                        Offset = offset,
                        Dims = dims,
                        SkipUniform = SurfaceNetsJob.SkipUniformLayers,
                        Min = min,
                        Max = max,
                        VoxelSize = VoxelSize,
                        Origin = Origin,
                        Vertices = job.Vertices,
                        Indices = job.Indices
                    }.Schedule()
                    : new GreedyMeshJob
                    {
                        Voxels = job.Region,
                        Palette = job.Palette,
                        Size = Size,
                        Offset = offset,
                        Dims = dims,
                        ClosedBelow = _settings.Terrain,
                        Min = min,
                        Max = max,
                        VoxelSize = VoxelSize,
                        Origin = Origin,
                        Vertices = job.Vertices,
                        Indices = job.Indices
                    }.Schedule();

                // A building's boxes from the same copy (a bullet's hole: the wall's collider follows a frame later).
                if (!smooth && _settings.Colliders && !_settings.Terrain && job.Shape)
                {
                    job.Boxes = new NativeList<VoxelBox>(16, Allocator.Persistent);
                    job.BoxOffset = offset;
                    JobHandle boxes = new BoxDecomposeJob
                    {
                        Voxels = job.Region, Size = dims, Min = min - offset, Max = max - offset, Cell = _settings.ColliderCell,
                        Boxes = job.Boxes
                    }.Schedule();
                    job.Handle = JobHandle.CombineDependencies(job.Handle, boxes);
                }

                _dirty[index] = false;
                _shapeDirty[index] = false;
                _busy[index] = true;
                _dirtyCount--;
                budget--;
                jobs.Add(job);
            }
        }

        internal void ScheduleRebuild(List<ChunkJob> jobs)
        {
            for (int index = 0; index < _dirty.Length; index++)
            {
                // A chunk still remeshing on a worker waits for it: its older result must not land
                // over this newer one. It stays dirty and goes next frame.
                if (!_dirty[index] || _busy[index])
                    continue;

                _dirty[index] = false;
                _dirtyCount--;
                bool shape = _shapeDirty[index];
                _shapeDirty[index] = false;

                int3 chunk = new int3(index % _chunkCount.x, index / _chunkCount.x % _chunkCount.y,
                    index / (_chunkCount.x * _chunkCount.y));
                int3 min = chunk * _chunk;
                int3 max = math.min(min + _chunk, Size);

                var job = new ChunkJob
                {
                    Body = this,
                    Chunk = index,
                    Vertices = new NativeList<VoxelVertex>(1024, Allocator.TempJob),
                    Indices = new NativeList<int>(1536, Allocator.TempJob),
                    Shape = shape
                };

                if (_settings.Smooth)
                {
                    // Smooth: one surface over the voxels, which is its own collider (cooked later).
                    // The first build reads the grid itself and is waited for: the ground is there
                    // before anything stands on it.
                    job.Handle = new SurfaceNetsJob
                    {
                        Voxels = _voxels,
                        Palette = _palette,
                        Size = Size,
                        Offset = int3.zero,
                        Dims = Size,
                        SkipUniform = SurfaceNetsJob.SkipUniformLayers,
                        Min = min,
                        Max = max,
                        VoxelSize = VoxelSize,
                        Origin = Origin,
                        Vertices = job.Vertices,
                        Indices = job.Indices
                    }.Schedule();
                    jobs.Add(job);
                    continue;
                }

                job.Handle = new GreedyMeshJob
                {
                    Voxels = _voxels,
                    Palette = _palette,
                    Size = Size,
                    Offset = int3.zero,
                    Dims = Size,
                    ClosedBelow = _settings.Terrain,
                    Min = min,
                    Max = max,
                    VoxelSize = VoxelSize,
                    Origin = Origin,
                    Vertices = job.Vertices,
                    Indices = job.Indices
                }.Schedule();

                // Terrain collides with its mesh (cooked in jobs): boxes over a stepped slope would
                // run to hundreds of thousands. A colour-only change keeps the boxes it has.
                if (_settings.Colliders && !_settings.Terrain && shape)
                {
                    job.Boxes = new NativeList<VoxelBox>(16, Allocator.TempJob);
                    JobHandle boxes = new BoxDecomposeJob
                    {
                        Voxels = _voxels,
                        Size = Size,
                        Min = min,
                        Max = max,
                        Cell = _settings.ColliderCell,
                        Boxes = job.Boxes
                    }.Schedule();
                    job.Handle = JobHandle.CombineDependencies(job.Handle, boxes);
                }

                jobs.Add(job);
            }

            _built = true;
        }

        internal void ApplyChunk(in ChunkJob job)
        {
            if (job.Async)
                _busy[job.Chunk] = false;

            Chunk chunk = _chunks[job.Chunk];
            int indexCount = job.Indices.Length;

            if (indexCount == 0)
            {
                // Emptied (a wall knocked down to the last voxel): its colliders go with it, navigation reads the ground again.
                if (chunk != null && IsStatic && chunk.GameObject.activeSelf)
                    VoxelWorld.RaiseChunkCollidersChanged(chunk.Renderer.bounds);
                if (chunk != null)
                    chunk.GameObject.SetActive(false);
                return;
            }

            chunk ??= _chunks[job.Chunk] = CreateChunk(job.Chunk);
            chunk.GameObject.SetActive(true);

            Mesh mesh = chunk.Mesh;
            mesh.Clear();
            mesh.SetVertexBufferParams(job.Vertices.Length, VoxelVertex.Layout);
            mesh.SetVertexBufferData(job.Vertices.AsArray(), 0, 0, job.Vertices.Length, 0, MESH_FLAGS);
            mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
            mesh.SetIndexBufferData(job.Indices.AsArray(), 0, 0, indexCount, MESH_FLAGS);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexCount), MESH_FLAGS);
            mesh.bounds = ChunkBounds(job.Chunk);

            if (job.Boxes.IsCreated)
                ApplyBoxes(chunk, job.Boxes, job.BoxOffset);
            else if ((_settings.Smooth || _settings.Terrain) && _settings.Colliders &&
                     (job.Shape || chunk.Collider == null || chunk.Collider.sharedMesh == null))
                WriteCollision(chunk, job);

            if (_settings.Terrain && !_settings.Smooth && job.Shape)
                UpdateRelief(job.Chunk, chunk, job.Vertices);
        }

        /// <summary>
        /// Flat blocky ground casts no shadow but on what lies under it: a chunk that is all level
        /// top faces is left out of the shadow map. A chunk with a crater, a rut or a step in it
        /// casts, and so do the chunks round it (a rim on the border shades the next chunk).
        /// </summary>
        private void UpdateRelief(int index, Chunk chunk, NativeList<VoxelVertex> vertices)
        {
            // Any face but a level top: a crater, a rut, a step, or the cliff round the map (it
            // shades the strip of bedrock beyond).
            bool relief = false;
            for (int i = 0; i < vertices.Length && !relief; i++)
                relief = vertices[i].Normal.y < 0.5f;

            if (chunk.Relief == relief && chunk.ShadowsSet)
                return;
            chunk.Relief = relief;

            int3 c = VoxelMath.Coord(index, _chunkCount);
            for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int3 n = c + new int3(dx, dy, dz);
                if (VoxelMath.Inside(n, _chunkCount) && _chunks[VoxelMath.Index(n, _chunkCount)] is { } neighbour)
                    SetShadows(n, neighbour);
            }
        }

        private void SetShadows(int3 c, Chunk chunk)
        {
            bool cast = false;
            for (int dz = -1; dz <= 1 && !cast; dz++)
            for (int dy = -1; dy <= 1 && !cast; dy++)
            for (int dx = -1; dx <= 1 && !cast; dx++)
            {
                int3 n = c + new int3(dx, dy, dz);
                cast = VoxelMath.Inside(n, _chunkCount) && _chunks[VoxelMath.Index(n, _chunkCount)] is { Relief: true };
            }

            chunk.ShadowsSet = true;
            chunk.Renderer.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        /// <summary>
        /// Puts a chunk's new surface into its spare collision mesh and has it cooked on a worker;
        /// the collider takes it once cooked (see VoxelWorld). Until then it keeps the old one.
        /// </summary>
        private void WriteCollision(Chunk chunk, in ChunkJob job)
        {
            chunk.Collider ??= chunk.GameObject.AddComponent<MeshCollider>();

            // The spare is still cooking from an earlier change: let that land first.
            if (chunk.Cooking && VoxelWorld.Instance != null)
                VoxelWorld.Instance.FinishBakes(true);

            chunk.CollisionA ??= new Mesh { name = "ChunkCollisionA" };
            chunk.CollisionB ??= new Mesh { name = "ChunkCollisionB" };
            Mesh spare = chunk.Collider.sharedMesh == chunk.CollisionA ? chunk.CollisionB : chunk.CollisionA;

            int indexCount = job.Indices.Length;
            spare.Clear();
            spare.SetVertexBufferParams(job.Vertices.Length, VoxelVertex.Layout);
            spare.SetVertexBufferData(job.Vertices.AsArray(), 0, 0, job.Vertices.Length, 0, MESH_FLAGS);
            spare.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
            spare.SetIndexBufferData(job.Indices.AsArray(), 0, 0, indexCount, MESH_FLAGS);
            spare.subMeshCount = 1;
            spare.SetSubMesh(0, new SubMeshDescriptor(0, indexCount), MESH_FLAGS);
            spare.bounds = chunk.Mesh.bounds;

            chunk.Cooking = true;
            VoxelWorld.QueueBake(spare, chunk);
        }

        private void ApplyBoxes(Chunk chunk, NativeList<VoxelBox> boxes, int3 offset)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                BoxCollider collider;
                if (i < chunk.Boxes.Count)
                    collider = chunk.Boxes[i];
                else
                {
                    collider = chunk.GameObject.AddComponent<BoxCollider>();
                    chunk.Boxes.Add(collider);
                }

                VoxelBox box = boxes[i];
                collider.center = (Vector3)(Origin + ((float3)(box.Min + box.Max) * 0.5f + offset) * VoxelSize);
                collider.size = (Vector3)((float3)(box.Max - box.Min) * VoxelSize);
                collider.enabled = true;
            }

            for (int i = boxes.Length; i < chunk.Boxes.Count; i++)
                chunk.Boxes[i].enabled = false;

            if (IsStatic)
                VoxelWorld.RaiseChunkCollidersChanged(chunk.Renderer.bounds);
        }

        /// <summary>
        /// After the frame's rebuild: a freshly detached piece now has colliders, so it may move.
        /// </summary>
        internal void FinishRebuild()
        {
            if (!_activatePending || _rigidbody == null)
                return;

            _activatePending = false;
            _rigidbody.isKinematic = false;

            // The blast's push goes through the centre of mass and weakens with the piece's mass:
            // AddExplosionForce in VelocityChange gave a 56 t house a brick's kick, off-centre, and
            // sent it spinning across the map.
            // The grid's middle, not worldCenterOfMass: the colliders came this frame, and the
            // mass properties may not have caught up with them yet.
            Vector3 middle = transform.TransformPoint(Origin + (float3)Size * (VoxelSize * 0.5f));
            // A building's piece pushed apart by a break (a soft push, not a blast) slides off its
            // neighbours: it is not tossed up, or rubble hops like cardboard.
            float lift = HasStrength && _pendingBlastForce < WAKE_FORCE ? 0f : BLAST_LIFT;
            Vector3 push = BlastPush(middle, _rigidbody.mass, _pendingBlastCenter, _pendingBlastForce, lift);
            Vector3 spin = _pendingSpin + Random.onUnitSphere * (push.magnitude * BLAST_SPIN);
            _rigidbody.linearVelocity = Vector3.ClampMagnitude(_pendingVelocity + push, MAX_LAUNCH_SPEED);
            _rigidbody.angularVelocity = Vector3.ClampMagnitude(spin, MAX_SPIN);
            _pendingSpin = Vector3.zero;
        }

        /// <summary>
        /// The speed a blast of <paramref name="force"/> m/s at <paramref name="center"/> gives a piece
        /// whose centre of mass is at <paramref name="at"/>: away from the blast and a little up,
        /// falling off linearly to nothing at <see cref="BLAST_REACH"/>, full for pieces up to
        /// <see cref="BLAST_FULL_MASS"/> and in inverse proportion to the mass above it.
        /// </summary>
        internal static Vector3 BlastPush(Vector3 at, float mass, Vector3 center, float force, float lift = BLAST_LIFT)
        {
            if (force <= 0f)
                return Vector3.zero;

            Vector3 away = at - center;
            float distance = away.magnitude;
            float falloff = 1f - Mathf.Clamp01(distance / BLAST_REACH);
            if (falloff <= 0f)
                return Vector3.zero;

            Vector3 direction = distance > 0.001f ? away / distance : Vector3.up;
            direction = (direction + Vector3.up * lift).normalized;
            float share = Mathf.Min(1f, BLAST_FULL_MASS / Mathf.Max(1f, mass));
            return direction * (force * falloff * share);
        }

        private Chunk CreateChunk(int index)
        {
            var go = new GameObject(CHUNK_NAME) { layer = _settings.Layer };
            go.transform.SetParent(transform, false);

            var mesh = new Mesh { name = CHUNK_NAME };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            // No light or reflection probes in the voxel scenes: the ambient probe and the skybox
            // are what the shader gets either way, without the per-renderer probe lookup.
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            // Standing ground and buildings never move: the camera's motion is all they have.
            if (IsStatic)
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
            // The tank's skin is lit by a blast's real Light (with its belts): not by the shader too.
            if (VoxelFlash.LitByLight(go.layer))
            {
                var block = new MaterialPropertyBlock();
                block.SetFloat(VoxelFlash.ReceiveId, 0f);
                renderer.SetPropertyBlock(block);
            }

            return new Chunk { GameObject = go, Mesh = mesh, Renderer = renderer };
        }

        private Bounds ChunkBounds(int index)
        {
            int3 chunk = new int3(index % _chunkCount.x, index / _chunkCount.x % _chunkCount.y,
                index / (_chunkCount.x * _chunkCount.y));
            float3 min = Origin + (float3)(chunk * _chunk) * VoxelSize;
            float3 max = Origin + (float3)math.min((chunk + 1) * _chunk, Size) * VoxelSize;
            // A smooth surface reaches a voxel past its chunk on every side.
            float pad = _settings.Smooth ? VoxelSize * 1.5f : 0f;
            return new Bounds((min + max) * 0.5f, max - min + pad * 2f);
        }

        private void OnDestroy()
        {
            VoxelWorld.Unregister(this);
            Release();
        }

        private void Release()
        {
            ReleaseStructure();
            if (_voxels.IsCreated)
                _voxels.Dispose();
            if (_palette.IsCreated)
                _palette.Dispose();

            if (_chunks == null)
                return;

            foreach (Chunk chunk in _chunks)
            {
                if (chunk == null)
                    continue;
                // A worker may still be cooking a collision mesh of this chunk.
                if (chunk.Cooking && VoxelWorld.Current != null)
                    VoxelWorld.Current.FinishBakes(true);
                if (chunk.Mesh != null)
                    Destroy(chunk.Mesh);
                if (chunk.CollisionA != null)
                    Destroy(chunk.CollisionA);
                if (chunk.CollisionB != null)
                    Destroy(chunk.CollisionB);
            }
        }
    }
}
