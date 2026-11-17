using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxels
{
    /// <summary>
    /// Lying cubes are drawn as one mesh per world cell: the cubes of a cell baked together in a
    /// Burst job, faces between touching cubes of one piece and faces on the ground left out. A
    /// cell is rebuilt only when it changes (a cube lies down in it, starts to sink or goes), at most
    /// MAX_CELL_BUILDS_PER_FRAME a frame. What is not in a mesh - sinking cubes, cubes waiting for
    /// their cell's build, cubes with no cell - is drawn instanced, gathered each step.
    /// </summary>
    public sealed partial class VoxelDebris
    {
        /// <summary>Side (m) of a world cell on XZ: one mesh of lying cubes per cell.</summary>
        public const float CELL_SIZE = 8f;

        /// <summary>
        /// Cell slots: a slot, once taken by a cell, keeps it for good (its mesh is reused). 1024
        /// cells of 8 m cover 256 x 256 m; a cube past them has no cell and is drawn instanced.
        /// </summary>
        private const int MAX_CELLS = 1024;

        /// <summary>Most cells rebuilt a frame: the rest stay dirty for the next frames, so a bomb is no spike.</summary>
        private const int MAX_CELL_BUILDS_PER_FRAME = 4;

        /// <summary>Most lying cubes drawn instanced (sinking, waiting for a build, with no cell); past it the rest are not drawn.</summary>
        private const int MAX_LYING_INSTANCED = 8192;

        private static readonly ProfilerMarker s_buildCellsMarker = new("VoxelDebris.BuildCells");

        /// <summary>One cell of the lying store.</summary>
        private struct LyingCell
        {
            public int2 Coord;
            /// <summary>Cubes whose Cell is this one (sinking ones too).</summary>
            public int Cubes;
            /// <summary>1 when its mesh no longer shows its cubes: it waits for a build.</summary>
            public int Dirty;
        }

        /// <summary>A cell mesh's vertex: position, normal, colour (alpha is dryness, as on chunk meshes).</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct CellVertex
        {
            public float3 Position;
            public float3 Normal;
            public uint Color;
        }

        private NativeArray<LyingCell> _cells;
        private NativeParallelHashMap<int2, int> _cellIndex;
        /// <summary>Per cell slot, created at its first build and reused: never destroyed during play.</summary>
        private Mesh[] _cellMeshes;
        /// <summary>Per cell slot, as last applied: triangles in its mesh, and the mesh's world box.</summary>
        private int[] _cellTriangles;
        private Bounds[] _cellBounds;
        /// <summary>Per cell slot, this frame: drawn (see <see cref="CullCells"/>).</summary>
        private bool[] _cellVisible;
        /// <summary>
        /// The debris material with instancing off: a cell mesh is drawn once, with its colours in the
        /// vertices and _InstColor (white) from the material.
        /// </summary>
        private Material _cellMaterial;
        private RenderParams _cellParams;

        /// <summary>The cells this step builds, and what the build left for the next Tick to apply.</summary>
        private NativeArray<int> _buildCells;
        private NativeArray<float3x2> _builtBounds;
        private NativeArray<int> _builtTriangles;
        private Mesh.MeshDataArray _buildData;
        private int _buildCount;
        private bool _buildPending;
        private readonly List<Mesh> _buildMeshes = new(MAX_CELL_BUILDS_PER_FRAME);
        /// <summary>The cell slot the search for dirty cells starts from next: the cells go in turn, so none waits for ever.</summary>
        private int _buildFrom;

        /// <summary>Lying cubes drawn instanced this frame, gathered by LyingInstancesJob.</summary>
        private NativeArray<float4x4> _instancedMatrices;
        private NativeArray<float4> _instancedColors;
        private NativeReference<int> _instancedCount;
        private NativeReference<float3x2> _instancedBounds;
        private readonly MaterialPropertyBlock _lyingBlock = new();

        /// <summary>Cell meshes drawn last frame (after culling): for tests and debugging.</summary>
        public int LyingCellsDrawn { get; private set; }

        /// <summary>Cells holding at least one lying cube: for tests and debugging.</summary>
        public int LyingCellCount
        {
            get
            {
                ApplyBuiltCells();
                if (!_cells.IsCreated)
                    return 0;
                int n = 0;
                int cells = _lyingState.Value.Cells;
                for (int c = 0; c < cells; c++)
                    if (_cells[c].Cubes > 0)
                        n++;
                return n;
            }
        }

        /// <summary>Triangles in all cell meshes, as built so far: for tests and debugging.</summary>
        public int LyingTriangleCount
        {
            get
            {
                ApplyBuiltCells();
                int n = 0;
                if (_cellTriangles != null)
                    for (int c = 0; c < _cellTriangles.Length; c++)
                        n += _cellTriangles[c];
                return n;
            }
        }

        /// <summary>Triangles in one cell slot's mesh: for tests and debugging.</summary>
        public int LyingCellTriangles(int cell)
        {
            ApplyBuiltCells();
            return _cellTriangles != null && cell >= 0 && cell < _cellTriangles.Length ? _cellTriangles[cell] : 0;
        }

        /// <summary>Lying cubes drawn instanced, not from a cell mesh (sinking, waiting for a build, with no cell): for tests.</summary>
        public int LyingInstancedCount
        {
            get
            {
                Complete();
                return _instancedCount.IsCreated ? _instancedCount.Value : 0;
            }
        }

        private static int2 CellOf(float3 position) => (int2)math.floor(position.xz / CELL_SIZE);

        /// <summary>The cell's mesh no longer shows its cubes (-1, no cell: nothing to do).</summary>
        private static void MarkDirty(NativeArray<LyingCell> cells, int cell)
        {
            if (cell < 0)
                return;
            LyingCell c = cells[cell];
            c.Dirty = 1;
            cells[cell] = c;
        }

        /// <summary>A cube leaves its cell; the cell is rebuilt only when the cube was in its mesh.</summary>
        private static void LeaveCell(NativeArray<LyingCell> cells, int cell, bool inMesh)
        {
            if (cell < 0)
                return;
            LyingCell c = cells[cell];
            c.Cubes = math.max(0, c.Cubes - 1);
            if (inMesh)
                c.Dirty = 1;
            cells[cell] = c;
        }

        private void CreateCells(Material material)
        {
            _cells = new NativeArray<LyingCell>(MAX_CELLS, Allocator.Persistent);
            _cellIndex = new NativeParallelHashMap<int2, int>(MAX_CELLS, Allocator.Persistent);
            _cellMeshes = new Mesh[MAX_CELLS];
            _cellTriangles = new int[MAX_CELLS];
            _cellBounds = new Bounds[MAX_CELLS];
            _cellVisible = new bool[MAX_CELLS];
            _buildCells = new NativeArray<int>(MAX_CELL_BUILDS_PER_FRAME, Allocator.Persistent);
            _builtBounds = new NativeArray<float3x2>(MAX_CELL_BUILDS_PER_FRAME, Allocator.Persistent);
            _builtTriangles = new NativeArray<int>(MAX_CELL_BUILDS_PER_FRAME, Allocator.Persistent);
            _instancedMatrices = new NativeArray<float4x4>(MAX_LYING_INSTANCED, Allocator.Persistent);
            _instancedColors = new NativeArray<float4>(MAX_LYING_INSTANCED, Allocator.Persistent);
            _instancedCount = new NativeReference<int>(Allocator.Persistent);
            _instancedBounds = new NativeReference<float3x2>(Allocator.Persistent);

            _cellMaterial = new Material(material) { name = material.name + " (cells)", enableInstancing = false };
            _cellParams = new RenderParams(_cellMaterial)
            {
                layer = 0,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off
            };
            // The first SetVectorArray fixes the array's length for good: set it full length once.
            _lyingBlock.SetVectorArray(InstanceColorId, _batchColors);
        }

        private void DisposeCells()
        {
            if (_buildPending)
            {
                _buildData.Dispose();
                _buildPending = false;
            }
            if (_cells.IsCreated) _cells.Dispose();
            if (_cellIndex.IsCreated) _cellIndex.Dispose();
            if (_buildCells.IsCreated) _buildCells.Dispose();
            if (_builtBounds.IsCreated) _builtBounds.Dispose();
            if (_builtTriangles.IsCreated) _builtTriangles.Dispose();
            if (_instancedMatrices.IsCreated) _instancedMatrices.Dispose();
            if (_instancedColors.IsCreated) _instancedColors.Dispose();
            if (_instancedCount.IsCreated) _instancedCount.Dispose();
            if (_instancedBounds.IsCreated) _instancedBounds.Dispose();
            if (_cellMeshes != null)
                foreach (Mesh mesh in _cellMeshes)
                    if (mesh != null)
                        Object.Destroy(mesh);
            if (_cellMaterial != null) Object.Destroy(_cellMaterial);
        }

        /// <summary>
        /// Picks up to MAX_CELL_BUILDS_PER_FRAME dirty cells, in turn, for this step to build, and
        /// allocates their mesh data. Main thread, with no job of the step scheduled yet.
        /// </summary>
        private void ChooseCellBuilds()
        {
            _buildCount = 0;
            int cells = _lyingState.Value.Cells;
            for (int j = 0; j < cells && _buildCount < MAX_CELL_BUILDS_PER_FRAME; j++)
            {
                int c = (_buildFrom + j) % cells;
                if (_cells[c].Dirty == 0)
                    continue;
                _buildCells[_buildCount++] = c;
                _buildFrom = c + 1;
            }

            if (_buildCount == 0)
                return;
            using (s_buildCellsMarker.Auto())
                _buildData = Mesh.AllocateWritableMeshData(_buildCount);
            _buildPending = true;
        }

        /// <summary>The cell builds chosen for this step, then the gathering of what is drawn instanced.</summary>
        private JobHandle ScheduleCells(JobHandle dependsOn)
        {
            JobHandle handle = dependsOn;
            if (_buildCount > 0)
            {
                handle = new BuildCellsJob
                {
                    Chosen = _buildCells,
                    Lying = _lyingCubes,
                    Cells = _cells,
                    State = _lyingState,
                    Meshes = _buildData,
                    Bounds = _builtBounds,
                    Triangles = _builtTriangles
                }.Schedule(_buildCount, 1, handle);
            }

            return new LyingInstancesJob
            {
                Lying = _lyingCubes,
                Matrices = _lyingMatrices,
                Colors = _lyingColors,
                State = _lyingState,
                Built = _buildCells,
                BuiltCount = _buildCount,
                Out = _instancedMatrices,
                OutColors = _instancedColors,
                OutCount = _instancedCount,
                OutBounds = _instancedBounds
            }.Schedule(handle);
        }

        /// <summary>Hands the meshes the last step built to their cells. Main thread, after Complete().</summary>
        private void ApplyBuiltCells()
        {
            Complete();
            if (!_buildPending)
                return;
            _buildPending = false;

            using (s_buildCellsMarker.Auto())
            {
                _buildMeshes.Clear();
                for (int k = 0; k < _buildCount; k++)
                {
                    int cell = _buildCells[k];
                    if (_cellMeshes[cell] == null)
                    {
                        _cellMeshes[cell] = new Mesh { name = "VoxelDebrisCell" };
                        _cellMeshes[cell].MarkDynamic();
                    }
                    _buildMeshes.Add(_cellMeshes[cell]);
                }

                Mesh.ApplyAndDisposeWritableMeshData(_buildData, _buildMeshes,
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices |
                    MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontResetBoneBounds);

                for (int k = 0; k < _buildCount; k++)
                {
                    int cell = _buildCells[k];
                    Bounds bounds = ToBounds(_builtBounds[k]);
                    _cellMeshes[cell].bounds = bounds;
                    _cellBounds[cell] = bounds;
                    _cellTriangles[cell] = _builtTriangles[k];
                }
            }
        }

        /// <summary>
        /// Lying cubes: each cell's mesh in the view camera's frustum and within LYING_DRAW_DISTANCE,
        /// then what is drawn instanced. No shadows: tens of thousands of tiles and crumbs flat on the ground.
        /// </summary>
        private void DrawLying()
        {
            int cells = _lyingState.Value.Cells;
            using (s_cullLyingMarker.Auto())
                CullCells(cells);

            int drawn = 0;
            for (int c = 0; c < cells; c++)
            {
                if (!_cellVisible[c])
                    continue;
                _cellParams.worldBounds = _cellBounds[c];
                Graphics.RenderMesh(_cellParams, _cellMeshes[c], 0, Matrix4x4.identity);
                drawn++;
            }
            LyingCellsDrawn = drawn;

            int count = _instancedCount.Value;
            if (count == 0)
                return;
            NativeArray<Matrix4x4> matrices = _instancedMatrices.Reinterpret<Matrix4x4>();
            NativeArray<Vector4> colors = _instancedColors.Reinterpret<Vector4>();
            _lyingParams.worldBounds = ToBounds(_instancedBounds.Value);
            _lyingParams.matProps = _lyingBlock;
            for (int start = 0; start < count; start += BATCH)
            {
                int n = Mathf.Min(BATCH, count - start);
                NativeArray<Matrix4x4>.Copy(matrices, start, _batchMatrices, 0, n);
                NativeArray<Vector4>.Copy(colors, start, _batchColors, 0, n);
                _lyingBlock.SetVectorArray(InstanceColorId, _batchColors);
                Graphics.RenderMeshInstanced(_lyingParams, _mesh, 0, _batchMatrices, n);
            }
        }

        /// <summary>
        /// Marks which of the first <paramref name="cells"/> cells are drawn: a mesh with triangles,
        /// its box in the view camera's frustum and its nearest point within LYING_DRAW_DISTANCE
        /// (every non-empty one with no camera).
        /// </summary>
        private void CullCells(int cells)
        {
            Camera camera = ViewCamera;
            float3 eye = default;
            if (camera != null)
            {
                GeometryUtility.CalculateFrustumPlanes(camera, _frustum);
                eye = camera.transform.position;
            }

            const float FAR_SQ = LYING_DRAW_DISTANCE * LYING_DRAW_DISTANCE;
            for (int c = 0; c < cells; c++)
            {
                bool visible = _cellTriangles[c] > 0;
                if (visible && camera != null)
                {
                    Bounds bounds = _cellBounds[c];
                    float3 gap = math.max(0f, math.max((float3)bounds.min - eye, eye - (float3)bounds.max));
                    visible = math.lengthsq(gap) <= FAR_SQ && GeometryUtility.TestPlanesAABB(_frustum, bounds);
                }
                _cellVisible[c] = visible;
            }
        }

        /// <summary>
        /// Bakes one chosen cell per index: its lying cubes that are not sinking, each face left out
        /// when it looks down (lies on the ground) or when the next cube on its piece's grid, of the
        /// same size, covers it. Writes the mesh data, the mesh's world box and its triangle count,
        /// and clears the cell's Dirty.
        /// </summary>
        [BurstCompile]
        private struct BuildCellsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> Chosen;
            [ReadOnly] public NativeArray<LyingCube> Lying;
            /// <summary>Only the chosen cell's own entry is written: the chosen cells are distinct.</summary>
            [NativeDisableParallelForRestriction] public NativeArray<LyingCell> Cells;
            [ReadOnly] public NativeReference<LyingState> State;
            public Mesh.MeshDataArray Meshes;
            public NativeArray<float3x2> Bounds;
            public NativeArray<int> Triangles;

            public void Execute(int k)
            {
                int cell = Chosen[k];
                LyingCell c = Cells[cell];
                c.Dirty = 0;
                Cells[cell] = c;

                int count = State.Value.Count;
                var members = new NativeList<int>(64, Allocator.Temp);
                var grid = new NativeParallelHashMap<int4, float>(64, Allocator.Temp);
                for (int i = 0; i < count; i++)
                {
                    LyingCube cube = Lying[i];
                    if (cube.Cell != cell || cube.Sinking > 0f)
                        continue;
                    members.Add(i);
                    if ((cube.Flags & LAID) == 0 && OnGrid(cube, out int3 g))
                        grid.TryAdd(new int4(cube.Group, g), cube.Size);
                }

                var masks = new NativeArray<byte>(members.Length, Allocator.Temp);
                int faces = 0;
                for (int m = 0; m < members.Length; m++)
                {
                    byte mask = VisibleFaces(Lying[members[m]], grid);
                    masks[m] = mask;
                    faces += math.countbits((uint)mask);
                }

                int vertices = faces * 4;
                int indices = faces * 6;
                bool wide = vertices > 65535;
                Mesh.MeshData data = Meshes[k];
                var layout = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Temp);
                layout[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
                layout[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);
                layout[2] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4);
                data.SetVertexBufferParams(vertices, layout);
                data.SetIndexBufferParams(indices, wide ? IndexFormat.UInt32 : IndexFormat.UInt16);

                NativeArray<CellVertex> vertexData = data.GetVertexData<CellVertex>();
                NativeArray<uint> index32 = wide ? data.GetIndexData<uint>() : default;
                NativeArray<ushort> index16 = wide ? default : data.GetIndexData<ushort>();

                var min = new float3(float.MaxValue);
                var max = new float3(float.MinValue);
                int v = 0;
                int t = 0;
                for (int m = 0; m < members.Length; m++)
                {
                    LyingCube cube = Lying[members[m]];
                    uint color = Pack(cube.Color);
                    byte mask = masks[m];
                    for (int face = 0; face < 6; face++)
                    {
                        if ((mask & (1 << face)) == 0)
                            continue;

                        Face(face, out float3 n, out float3 u, out float3 w, out float s);
                        float3 a = n * (0.5f * s) - u * 0.5f - w * 0.5f;
                        float3 normal = math.mul(cube.Rotation, n * s);
                        for (int corner = 0; corner < 4; corner++)
                        {
                            float3 local = a + (corner == 1 || corner == 2 ? u : float3.zero) + (corner >= 2 ? w : float3.zero);
                            float3 p = cube.Position + math.mul(cube.Rotation, local * cube.Scale);
                            min = math.min(min, p);
                            max = math.max(max, p);
                            vertexData[v + corner] = new CellVertex { Position = p, Normal = normal, Color = color };
                        }

                        // The winding of BuildCubeMesh: front faces out.
                        int i0 = v, i1 = s > 0f ? v + 1 : v + 2, i2 = s > 0f ? v + 2 : v + 1;
                        int i3 = v, i4 = s > 0f ? v + 2 : v + 3, i5 = s > 0f ? v + 3 : v + 2;
                        if (wide)
                        {
                            index32[t] = (uint)i0; index32[t + 1] = (uint)i1; index32[t + 2] = (uint)i2;
                            index32[t + 3] = (uint)i3; index32[t + 4] = (uint)i4; index32[t + 5] = (uint)i5;
                        }
                        else
                        {
                            index16[t] = (ushort)i0; index16[t + 1] = (ushort)i1; index16[t + 2] = (ushort)i2;
                            index16[t + 3] = (ushort)i3; index16[t + 4] = (ushort)i4; index16[t + 5] = (ushort)i5;
                        }
                        v += 4;
                        t += 6;
                    }
                }

                if (faces == 0)
                {
                    min = new float3(c.Coord.x, 0f, c.Coord.y) * CELL_SIZE;
                    max = min;
                }

                data.subMeshCount = 1;
                data.SetSubMesh(0, new SubMeshDescriptor(0, indices)
                {
                    bounds = new Bounds((min + max) * 0.5f, max - min),
                    vertexCount = vertices
                }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);

                Bounds[k] = new float3x2(min, max);
                Triangles[k] = faces * 2;

                layout.Dispose();
                masks.Dispose();
                grid.Dispose();
                members.Dispose();
            }

            /// <summary>Where the cube sits on its piece's grid, and whether it sits on it at all (a quarter of a cube off at most).</summary>
            private static bool OnGrid(in LyingCube cube, out int3 g)
            {
                float3 local = math.mul(math.conjugate(cube.Rotation), cube.Position - cube.Anchor) / math.max(cube.Size, 1e-5f);
                float3 r = math.round(local);
                g = (int3)r;
                return math.all(math.abs(local - r) < 0.25f);
            }

            /// <summary>One bit per face (see <see cref="Face"/>): the faces that show.</summary>
            private static byte VisibleFaces(in LyingCube cube, in NativeParallelHashMap<int4, float> grid)
            {
                int3 g = int3.zero;
                bool culls = (cube.Flags & LAID) == 0 && OnGrid(cube, out g);
                byte mask = 0;
                for (int face = 0; face < 6; face++)
                {
                    Face(face, out float3 n, out _, out _, out float s);
                    // Looks down: lies on the ground (or on what it lay on), never seen.
                    if (math.mul(cube.Rotation, n * s).y < -0.9f)
                        continue;
                    if (culls && grid.TryGetValue(new int4(cube.Group, g + (int3)(n * s)), out float size) &&
                        math.abs(size - cube.Size) < cube.Size * 0.01f)
                        continue;
                    mask |= (byte)(1 << face);
                }
                return mask;
            }

            /// <summary>Face 0...5: axis X, Y, Z (n), each -1 then +1 (s); u and w span it, as in BuildCubeMesh.</summary>
            private static void Face(int face, out float3 n, out float3 u, out float3 w, out float s)
            {
                int d = face >> 1;
                s = (face & 1) != 0 ? 1f : -1f;
                n = Axis(d);
                u = Axis((d + 1) % 3);
                w = Axis((d + 2) % 3);
            }

            private static float3 Axis(int d) => d == 0 ? new float3(1f, 0f, 0f) : d == 1 ? new float3(0f, 1f, 0f) : new float3(0f, 0f, 1f);

            private static uint Pack(float4 color)
            {
                uint4 c = (uint4)math.round(math.saturate(color) * 255f);
                return c.x | (c.y << 8) | (c.z << 16) | (c.w << 24);
            }
        }

        /// <summary>
        /// The lying cubes drawn instanced this frame: sinking ones (their matrices shrink as they
        /// go), and those not in a cell mesh yet (PENDING). Cubes of the cells built in this step lose
        /// PENDING first: the meshes applied at the next Tick show them. Does nothing while nothing sinks
        /// and nothing waits.
        /// </summary>
        [BurstCompile]
        private struct LyingInstancesJob : IJob
        {
            public NativeArray<LyingCube> Lying;
            [ReadOnly] public NativeArray<float4x4> Matrices;
            [ReadOnly] public NativeArray<float4> Colors;
            public NativeReference<LyingState> State;
            [ReadOnly] public NativeArray<int> Built;
            public int BuiltCount;
            [WriteOnly] public NativeArray<float4x4> Out;
            [WriteOnly] public NativeArray<float4> OutColors;
            public NativeReference<int> OutCount;
            public NativeReference<float3x2> OutBounds;

            public void Execute()
            {
                LyingState state = State.Value;
                if (state.Sinking == 0 && state.Unbaked == 0)
                {
                    OutCount.Value = 0;
                    return;
                }

                var min = new float3(float.MaxValue);
                var max = new float3(float.MinValue);
                int n = 0;
                for (int i = 0; i < state.Count; i++)
                {
                    LyingCube cube = Lying[i];
                    bool pending = (cube.Flags & PENDING) != 0;
                    if (pending && WasBuilt(cube.Cell))
                    {
                        cube.Flags &= ~PENDING;
                        Lying[i] = cube;
                        state.Unbaked = math.max(0, state.Unbaked - 1);
                        pending = false;
                    }
                    if (!pending && cube.Sinking <= 0f)
                        continue;
                    if (n >= Out.Length)
                        continue;

                    Out[n] = Matrices[i];
                    OutColors[n] = Colors[i];
                    float reach = math.length(cube.Scale) * 0.5f;
                    min = math.min(min, cube.Position - new float3(reach, reach + cube.Size, reach));
                    max = math.max(max, cube.Position + reach);
                    n++;
                }

                State.Value = state;
                OutCount.Value = n;
                OutBounds.Value = new float3x2(min, max);
            }

            private bool WasBuilt(int cell)
            {
                if (cell < 0)
                    return false;
                for (int k = 0; k < BuiltCount; k++)
                    if (Built[k] == cell)
                        return true;
                return false;
            }
        }
    }
}
