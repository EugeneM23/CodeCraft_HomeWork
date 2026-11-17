using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxels
{
    /// <summary>
    /// A smoke cloud made of voxels, in the manner of Counter-Strike 2's smoke grenade.
    ///
    /// The cloud is a grid of 30 cm cells round the point it went off. It grows outward for
    /// <see cref="SpreadTime"/> seconds, cell by cell, to a fixed volume: a flood fill that goes
    /// round walls, through doors and windows, never through them, so in a corridor the same smoke
    /// runs further than in the open. Every filled cell carries a density, drawn as a grey cube that
    /// shrinks as the density falls.
    ///
    /// Shots open it up: a bullet's path (<see cref="Cut"/>) takes the density out of a narrow
    /// tunnel, a blast (<see cref="Blast"/>) out of a whole sphere. An emptied cell holds for a moment,
    /// then fills back from its neighbours, so a hole closes from its edges inward in about a second.
    /// After <see cref="Lifetime"/> seconds the cloud thins away, its edges first.
    /// </summary>
    public sealed class VoxelSmoke : MonoBehaviour
    {
        /// <summary>Edge (m) of a smoke cell.</summary>
        public const float CELL = 0.3f;

        private const int SX = 48;
        private const int SY = 22;
        private const int SZ = 48;
        private const int CELLS = SX * SY * SZ;

        /// <summary>Cells below the source: room for smoke to settle into a crater.</summary>
        private const int BELOW = 3;

        private const byte UNKNOWN = 0;
        private const byte QUEUED = 1;
        private const byte FILLED = 2;
        private const byte BLOCKED = 3;

        /// <summary>Most cells checked against the world per frame while the cloud grows.</summary>
        private const int CHECKS_PER_FRAME = 900;

        /// <summary>Most cubes one cloud draws.</summary>
        private const int MAX_CUBES = 8000;

        private const int BATCH = 1023;
        private const string MATERIAL = "Voxels/VoxelSmoke";

        /// <summary>Climbing a cell costs this much more than a step sideways: the cloud is low and wide.</summary>
        private const float VERTICAL_COST = 1.35f;

        private static readonly int InstanceColorId = Shader.PropertyToID("_InstColor");
        private static readonly List<VoxelSmoke> s_active = new();
        private static Material s_material;
        private static Mesh s_mesh;

        /// <summary>What the smoke cannot pass: the ground, walls, structures, rubble.</summary>
        public static int WallMask { get; set; } = (1 << 0) | (1 << 3) | (1 << 10) | (1 << 14) | (1 << 16);

        public static IReadOnlyList<VoxelSmoke> Active => s_active;

        [Tooltip("Reach (m) of the cloud in the open, sideways; it is lower than wide.")]
        [SerializeField] private float _radius = 3.2f;
        [Tooltip("Seconds the cloud takes to fill out.")]
        [SerializeField] private float _spreadTime = 1.5f;
        [Tooltip("Seconds the cloud stands, the thinning at the end included.")]
        [SerializeField] private float _lifetime = 18f;
        [Tooltip("Seconds it thins away at the end, edges first.")]
        [SerializeField] private float _fadeTime = 3.5f;
        [Tooltip("Density per second an emptied cell regains, next to full cells.")]
        [SerializeField] private float _regrow = 1.6f;
        [SerializeField] private Color _tint = new(0.80f, 0.81f, 0.83f, 1f);

        private NativeArray<byte> _state;
        private NativeArray<float> _density;
        private NativeArray<float> _next;
        private NativeArray<float> _hold;
        private NativeArray<float> _edge;
        private NativeArray<float> _best;
        private NativeArray<short> _steps;
        private NativeArray<float4x4> _matrices;
        private NativeArray<float4> _colors;
        private NativeReference<int> _count;

        private readonly List<float> _heapKey = new();
        private readonly List<int> _heapCell = new();
        private readonly Matrix4x4[] _batchMatrices = new Matrix4x4[BATCH];
        private readonly Vector4[] _batchColors = new Vector4[BATCH];
        private MaterialPropertyBlock _block;

        private float3 _origin;
        private int3 _source;
        private int _targetCells;
        private float _age;
        private bool _created;

        public float Radius { get => _radius; set => _radius = value; }
        public float Lifetime { get => _lifetime; set => _lifetime = value; }
        public float SpreadTime => _spreadTime;
        public float Age => _age;

        /// <summary>Cells the cloud took so far.</summary>
        public int FilledCells { get; private set; }

        /// <summary>Cubes drawn last frame.</summary>
        public int DrawnCubes { get; private set; }

        /// <summary>The world box the grid covers.</summary>
        public Bounds GridBounds { get; private set; }

        /// <summary>Sets a smoke cloud off at a point (on the ground, or just above it).</summary>
        public static VoxelSmoke Deploy(Vector3 point, float radius = 3.2f, float lifetime = 18f)
        {
            var go = new GameObject("<VoxelSmoke>");
            go.transform.position = point;
            VoxelSmoke smoke = go.AddComponent<VoxelSmoke>();
            smoke._radius = radius;
            smoke._lifetime = lifetime;
            return smoke;
        }

        /// <summary>
        /// A shot through the smoke: every cloud it crosses loses its density in a tunnel of
        /// <paramref name="radius"/> round the segment, soft at the rim.
        /// </summary>
        public static void Cut(Vector3 from, Vector3 to, float radius = 0.5f, float hold = 0.5f)
        {
            for (int i = 0; i < s_active.Count; i++)
                s_active[i].ClearCapsule(from, to, radius, hold);
        }

        /// <summary>A blast: the smoke within <paramref name="radius"/> is blown out, thinner up to 1.5 × that.</summary>
        public static void Blast(Vector3 point, float radius, float hold = 1.2f)
        {
            for (int i = 0; i < s_active.Count; i++)
                s_active[i].ClearSphere(point, radius, hold);
        }

        /// <summary>Density (0..1) of the smoke at a point, over all clouds.</summary>
        public static float DensityAt(Vector3 point)
        {
            float density = 0f;
            for (int i = 0; i < s_active.Count; i++)
                density = math.max(density, s_active[i].LocalDensity(point));
            return density;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_active.Clear();
            s_material = null;
            s_mesh = null;
        }

        // Built in Start, not OnEnable: Deploy sets the radius and lifetime after AddComponent.
        private void Start()
        {
            if (!_created)
                Create();
        }

        private void OnEnable()
        {
            if (!s_active.Contains(this))
                s_active.Add(this);
        }

        private void OnDisable() => s_active.Remove(this);

        private void OnDestroy()
        {
            s_active.Remove(this);
            if (!_created)
                return;
            _state.Dispose();
            _density.Dispose();
            _next.Dispose();
            _hold.Dispose();
            _edge.Dispose();
            _best.Dispose();
            _steps.Dispose();
            _matrices.Dispose();
            _colors.Dispose();
            _count.Dispose();
            _created = false;
        }

        private void Create()
        {
            _state = new NativeArray<byte>(CELLS, Allocator.Persistent);
            _density = new NativeArray<float>(CELLS, Allocator.Persistent);
            _next = new NativeArray<float>(CELLS, Allocator.Persistent);
            _hold = new NativeArray<float>(CELLS, Allocator.Persistent);
            _edge = new NativeArray<float>(CELLS, Allocator.Persistent);
            _best = new NativeArray<float>(CELLS, Allocator.Persistent);
            _steps = new NativeArray<short>(CELLS, Allocator.Persistent);
            _matrices = new NativeArray<float4x4>(MAX_CUBES, Allocator.Persistent);
            _colors = new NativeArray<float4>(MAX_CUBES, Allocator.Persistent);
            _count = new NativeReference<int>(Allocator.Persistent);
            _created = true;

            float3 point = transform.position;
            _source = new int3(SX / 2, BELOW, SZ / 2);
            _origin = point - new float3((_source.x + 0.5f) * CELL, (_source.y + 0.5f) * CELL, (_source.z + 0.5f) * CELL);
            var size = new Vector3(SX * CELL, SY * CELL, SZ * CELL);
            GridBounds = new Bounds((Vector3)_origin + size * 0.5f, size);

            // In the open the cloud is half an ellipsoid: R sideways, R / VERTICAL_COST up.
            float r = _radius / CELL;
            _targetCells = (int)(0.5f * 4f / 3f * math.PI * r * r * (r / VERTICAL_COST));

            // Walls moved or made this frame are not in the physics scene yet: the fill would leak through them.
            Physics.SyncTransforms();

            // A grenade lying on the ground has its centre cell half in it: start a cell or two up.
            for (int up = 0; up < 4; up++)
            {
                int3 c = _source + new int3(0, up, 0);
                if (!Blocked(c))
                {
                    _source = c;
                    break;
                }
            }

            for (int i = 0; i < CELLS; i++)
                _best[i] = float.MaxValue;

            int start = Index(_source);
            _best[start] = 0f;
            _state[start] = QUEUED;
            Push(0f, start);
        }

        private void LateUpdate()
        {
            if (!_created)
                return;

            float dt = math.min(Time.deltaTime, 1f / 20f);
            _age += dt;
            if (_age >= _lifetime)
            {
                Destroy(gameObject);
                return;
            }

            Grow();

            float fade = 1f;
            float fadeStart = _lifetime - _fadeTime;
            if (_age > fadeStart)
                fade = math.saturate((_lifetime - _age) / _fadeTime);

            JobHandle handle = new RegrowJob
            {
                State = _state,
                Density = _density,
                Next = _next,
                Hold = _hold,
                Edge = _edge,
                Fade = fade,
                Regrow = _regrow,
                DeltaTime = dt
            }.Schedule(CELLS, 512);

            handle = new CubesJob
            {
                State = _state,
                Density = _next,
                Matrices = _matrices,
                Colors = _colors,
                Count = _count,
                Origin = _origin,
                Time = Time.time,
                Tint = new float4(_tint.r, _tint.g, _tint.b, 1f)
            }.Schedule(handle);
            handle.Complete();

            (_density, _next) = (_next, _density);
            DrawnCubes = _count.Value;
            Draw(DrawnCubes);
        }

        /// <summary>
        /// The flood fill: cells are taken nearest first — nearest by the way round walls, not as
        /// the crow flies — until the cloud reaches the share of its volume the time allows. Each
        /// cell is checked against the world when it is reached, so a frame only checks the few
        /// hundred cells it takes.
        /// </summary>
        private void Grow()
        {
            if (FilledCells >= _targetCells || _heapCell.Count == 0)
                return;

            float t = math.saturate(_age / _spreadTime);
            float share = 1f - (1f - t) * (1f - t);
            int allowed = math.max(1, (int)(_targetCells * share));
            float limit = _radius * 2.4f / CELL;

            int checks = 0;
            while (FilledCells < allowed && _heapCell.Count > 0 && checks < CHECKS_PER_FRAME)
            {
                Pop(out float key, out int index);
                if (_state[index] != QUEUED || key > _best[index] + 0.001f)
                    continue;

                int3 c = Cell(index);
                checks++;
                if (Blocked(c))
                {
                    _state[index] = BLOCKED;
                    continue;
                }

                _state[index] = FILLED;
                _density[index] = 0f;
                _edge[index] = math.saturate(key / (_radius / CELL)) * 0.8f + Hash(index) * 0.2f;
                FilledCells++;

                for (int n = 0; n < 6; n++)
                {
                    int3 d = n switch
                    {
                        0 => new int3(1, 0, 0),
                        1 => new int3(-1, 0, 0),
                        2 => new int3(0, 0, 1),
                        3 => new int3(0, 0, -1),
                        4 => new int3(0, 1, 0),
                        _ => new int3(0, -1, 0)
                    };
                    int3 nc = c + d;
                    if (math.any(nc < 0) || nc.x >= SX || nc.y >= SY || nc.z >= SZ)
                        continue;

                    int ni = Index(nc);
                    byte s = _state[ni];
                    if (s == FILLED || s == BLOCKED)
                        continue;

                    // Steps taken round obstacles past the straight Manhattan way are the detour:
                    // in the open it is nil and the cloud is round, behind a wall it adds up.
                    short steps = (short)(_steps[index] + 1);
                    int3 off = nc - _source;
                    float3 scaled = new float3(off.x, off.y * VERTICAL_COST, off.z);
                    float straight = math.length(scaled);
                    int manhattan = math.abs(off.x) + math.abs(off.y) + math.abs(off.z);
                    float detour = math.max(0, steps - manhattan);
                    float jitter = 1f + (Hash(ni) - 0.5f) * 0.3f;
                    float k = (straight + detour * 0.9f) * jitter;
                    if (k > limit || k >= _best[ni])
                        continue;

                    _best[ni] = k;
                    _steps[ni] = steps;
                    _state[ni] = QUEUED;
                    Push(k, ni);
                }
            }
        }

        private bool Blocked(int3 c)
        {
            Vector3 centre = (Vector3)CellCentre(c);
            return Physics.CheckBox(centre, Vector3.one * (CELL * 0.4f), Quaternion.identity, WallMask,
                QueryTriggerInteraction.Ignore);
        }

        private void ClearCapsule(Vector3 from, Vector3 to, float radius, float hold)
        {
            if (!_created || FilledCells == 0)
                return;

            var segment = new Bounds(from, Vector3.zero);
            segment.Encapsulate(to);
            segment.Expand(radius * 2f);
            if (!segment.Intersects(GridBounds))
                return;

            int3 lo = math.clamp((int3)math.floor(((float3)segment.min - _origin) / CELL), 0, new int3(SX - 1, SY - 1, SZ - 1));
            int3 hi = math.clamp((int3)math.floor(((float3)segment.max - _origin) / CELL), 0, new int3(SX - 1, SY - 1, SZ - 1));
            float3 a = from;
            float3 ab = (float3)to - a;
            float length = math.lengthsq(ab);

            for (int z = lo.z; z <= hi.z; z++)
            for (int y = lo.y; y <= hi.y; y++)
            for (int x = lo.x; x <= hi.x; x++)
            {
                int i = Index(new int3(x, y, z));
                if (_state[i] != FILLED)
                    continue;
                float3 p = CellCentre(new int3(x, y, z));
                float s = length > 0f ? math.saturate(math.dot(p - a, ab) / length) : 0f;
                float distance = math.distance(p, a + ab * s);
                Thin(i, distance / radius, hold);
            }
        }

        private void ClearSphere(Vector3 point, float radius, float hold)
        {
            if (!_created || FilledCells == 0)
                return;

            float outer = radius * 1.5f;
            if (GridBounds.SqrDistance(point) > outer * outer)
                return;

            float3 p0 = point;
            int3 lo = math.clamp((int3)math.floor((p0 - outer - _origin) / CELL), 0, new int3(SX - 1, SY - 1, SZ - 1));
            int3 hi = math.clamp((int3)math.floor((p0 + outer - _origin) / CELL), 0, new int3(SX - 1, SY - 1, SZ - 1));

            for (int z = lo.z; z <= hi.z; z++)
            for (int y = lo.y; y <= hi.y; y++)
            for (int x = lo.x; x <= hi.x; x++)
            {
                int i = Index(new int3(x, y, z));
                if (_state[i] != FILLED)
                    continue;
                float distance = math.distance(CellCentre(new int3(x, y, z)), p0);
                if (distance >= outer)
                    continue;
                float share = distance <= radius ? 0f : (distance - radius) / (outer - radius);
                Thin(i, share, hold * (0.8f + Hash(i) * 0.5f));
            }
        }

        /// <param name="share">0 at the middle of the cut (all gone) to 1 at its rim (untouched).</param>
        private void Thin(int i, float share, float hold)
        {
            if (share >= 1f)
                return;
            // The inner half of the cut is emptied outright, the rest thins toward the rim: a hole
            // you see through, not a dent the cubes round it close over.
            float keep = math.smoothstep(0.5f, 1f, share);
            _density[i] *= keep;
            _hold[i] = math.max(_hold[i], hold * (1f - keep));
        }

        private float LocalDensity(Vector3 point)
        {
            if (!_created || !GridBounds.Contains(point))
                return 0f;
            int3 c = (int3)math.floor(((float3)point - _origin) / CELL);
            int i = Index(c);
            return _state[i] == FILLED ? _density[i] : 0f;
        }

        private void Draw(int count)
        {
            if (count == 0)
                return;

            _block ??= new MaterialPropertyBlock();
            s_mesh ??= VoxelDebris.BuildCubeMesh();
            s_material ??= LoadMaterial();

            NativeArray<Matrix4x4> matrices = _matrices.Reinterpret<Matrix4x4>();
            NativeArray<Vector4> colors = _colors.Reinterpret<Vector4>();
            for (int start = 0; start < count; start += BATCH)
            {
                int n = Mathf.Min(BATCH, count - start);
                NativeArray<Matrix4x4>.Copy(matrices, start, _batchMatrices, 0, n);
                NativeArray<Vector4>.Copy(colors, start, _batchColors, 0, n);
                _block.SetVectorArray(InstanceColorId, _batchColors);
                Graphics.DrawMeshInstanced(s_mesh, 0, s_material, _batchMatrices, n, _block, ShadowCastingMode.On, true);
            }
        }

        private static Material LoadMaterial()
        {
            Material material = Resources.Load<Material>(MATERIAL);
            if (material == null)
            {
                Shader shader = Shader.Find("Game/Voxel");
                material = new Material(shader != null ? shader : Shader.Find("Standard"));
                material.SetFloat("_Glossiness", 0f);
            }

            if (!material.enableInstancing)
                material = new Material(material) { enableInstancing = true };
            return material;
        }

        private float3 CellCentre(int3 c) => _origin + ((float3)c + 0.5f) * CELL;

        private static int Index(int3 c) => c.x + SX * (c.y + SY * c.z);

        private static int3 Cell(int i) => new(i % SX, (i / SX) % SY, i / (SX * SY));

        private static float Hash(int i)
        {
            uint h = (uint)i * 0x9E3779B9u;
            h ^= h >> 15;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            return (h & 0xFFFF) / 65535f;
        }

        // A small binary min-heap: the cells waiting to be taken, nearest first.
        private void Push(float key, int cell)
        {
            _heapKey.Add(key);
            _heapCell.Add(cell);
            int i = _heapKey.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_heapKey[parent] <= _heapKey[i])
                    break;
                Swap(i, parent);
                i = parent;
            }
        }

        private void Pop(out float key, out int cell)
        {
            key = _heapKey[0];
            cell = _heapCell[0];
            int last = _heapKey.Count - 1;
            _heapKey[0] = _heapKey[last];
            _heapCell[0] = _heapCell[last];
            _heapKey.RemoveAt(last);
            _heapCell.RemoveAt(last);

            int i = 0;
            int count = _heapKey.Count;
            while (true)
            {
                int l = i * 2 + 1;
                if (l >= count)
                    break;
                int r = l + 1;
                int m = r < count && _heapKey[r] < _heapKey[l] ? r : l;
                if (_heapKey[i] <= _heapKey[m])
                    break;
                Swap(i, m);
                i = m;
            }
        }

        private void Swap(int a, int b)
        {
            (_heapKey[a], _heapKey[b]) = (_heapKey[b], _heapKey[a]);
            (_heapCell[a], _heapCell[b]) = (_heapCell[b], _heapCell[a]);
        }

        /// <summary>
        /// Density eases toward what the cloud wants there: full while it stands, nothing once its
        /// edge share passes the fade. An emptied cell waits out its hold, then fills back — slowly
        /// on its own, fast next to full cells, so holes close from the rim inward.
        /// </summary>
        [BurstCompile]
        private struct RegrowJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> State;
            [ReadOnly] public NativeArray<float> Density;
            [ReadOnly] public NativeArray<float> Edge;
            [WriteOnly] public NativeArray<float> Next;
            public NativeArray<float> Hold;
            public float Fade;
            public float Regrow;
            public float DeltaTime;

            public void Execute(int i)
            {
                if (State[i] != FILLED)
                {
                    Next[i] = 0f;
                    return;
                }

                float density = Density[i];
                float target = math.saturate(Fade * 1.6f - Edge[i] * 0.6f);

                float hold = Hold[i];
                if (hold > 0f)
                {
                    Hold[i] = hold - DeltaTime;
                    Next[i] = math.min(density, target);
                    return;
                }

                if (density > target)
                {
                    Next[i] = math.max(target, density - DeltaTime * 0.9f);
                    return;
                }

                int3 c = Cell(i);
                float around = 0f;
                around = math.max(around, Neighbour(c + new int3(1, 0, 0)));
                around = math.max(around, Neighbour(c + new int3(-1, 0, 0)));
                around = math.max(around, Neighbour(c + new int3(0, 1, 0)));
                around = math.max(around, Neighbour(c + new int3(0, -1, 0)));
                around = math.max(around, Neighbour(c + new int3(0, 0, 1)));
                around = math.max(around, Neighbour(c + new int3(0, 0, -1)));

                float rate = Regrow * (0.3f + 0.9f * around);
                Next[i] = math.min(target, density + rate * DeltaTime);
            }

            private float Neighbour(int3 c)
            {
                if (math.any(c < 0) || c.x >= SX || c.y >= SY || c.z >= SZ)
                    return 0f;
                int i = Index(c);
                return State[i] == FILLED ? Density[i] : 0f;
            }
        }

        /// <summary>
        /// One grey cube per cell with any smoke in it: full size and a little overlapping when the
        /// cell is full, shrinking as it thins, each one drifting slowly about its cell.
        /// </summary>
        [BurstCompile]
        private struct CubesJob : IJob
        {
            [ReadOnly] public NativeArray<byte> State;
            [ReadOnly] public NativeArray<float> Density;
            [WriteOnly] public NativeArray<float4x4> Matrices;
            [WriteOnly] public NativeArray<float4> Colors;
            public NativeReference<int> Count;
            public float3 Origin;
            public float Time;
            public float4 Tint;

            public void Execute()
            {
                int count = 0;
                for (int i = 0; i < State.Length && count < Matrices.Length; i++)
                {
                    if (State[i] != FILLED)
                        continue;
                    float density = Density[i];
                    if (density < 0.04f)
                        continue;

                    int3 c = Cell(i);
                    float h = Hash(i);
                    float h2 = Hash(i + 7919);
                    float3 drift = new float3(
                        math.sin(Time * 0.55f + h * 6.283f),
                        math.sin(Time * 0.4f + h2 * 6.283f) * 0.6f,
                        math.cos(Time * 0.5f + (h + h2) * 3.14f)) * (CELL * 0.14f);
                    float3 position = Origin + ((float3)c + 0.5f) * CELL + drift;
                    float size = CELL * (0.2f + 0.98f * density) * (0.92f + 0.16f * h2);

                    // Lighter on top, a shade darker low down and cell by cell.
                    float shade = (0.9f + 0.1f * h) * (0.9f + 0.1f * math.saturate(c.y / 10f));
                    Matrices[count] = float4x4.TRS(position, quaternion.identity, new float3(size));
                    Colors[count] = new float4(Tint.xyz * shade, 1f);
                    count++;
                }

                Count.Value = count;
            }
        }
    }
}
