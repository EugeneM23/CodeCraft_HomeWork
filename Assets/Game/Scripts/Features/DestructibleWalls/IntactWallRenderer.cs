using System;
using System.Collections.Generic;
using RayFire;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// Draws the standing part of a prefragmented RayFire wall as a handful of combined meshes rather
    /// than one renderer per piece.
    ///
    /// The walls are hundreds of pieces (ConcreteWall_Voronoi alone has 450), each with a renderer of
    /// its own and two materials, and every piece was drawn on its own in every pass: each shadow
    /// cascade, depth, depth-normals, forward base and forward-add. A standing piece never moves, so
    /// the wall is cut into cells of up to about eighty neighbouring pieces, each cell drawn as one mesh
    /// with a submesh per material, and the pieces' own renderers are switched off. Three quarters of a
    /// wall's triangles are faces where two pieces touch (RayFire gives them the interior material);
    /// nobody can see them while the neighbours stand, so a piece's interior faces stay out of its cell
    /// until one of its neighbours has left.
    ///
    /// A piece leaves the wall through RayFire (activated, cut off in a chunk by connectivity,
    /// demolished) or by being moved, reparented, switched off or destroyed. All of these are watched:
    /// a piece gets its own renderer back the moment it leaves, before anything reads its bounds or
    /// draws it, and the cells it touched are rebuilt once, after everything else's LateUpdate.
    ///
    /// Installs itself on every RayFire MeshRoot wall with connectivity when a scene loads, so no scene
    /// or prefab changes. <see cref="Enabled"/> switches it off and on at runtime for an A/B comparison.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class IntactWallRenderer : MonoBehaviour
    {
        /// <summary>
        /// A/B switch. While it is off no wall is set up, and the walls already set up draw their
        /// pieces one by one again until it is back on.
        /// </summary>
        public static bool Enabled = true;

        // A cell takes up to this many pieces, and is halved while its pieces' centres spread wider
        // than this (m) and are more than the minimum; the walls here come out at 26 cells, 1-2 m
        // across. A release rebuilds a small mesh, cameras and shadow cascades skip the cells out of
        // their view, and every cell is one more draw call per pass.
        private const int MAX_CELL_PIECES = 80;
        private const float MAX_CELL_SPREAD = 2f;
        private const int MIN_CELL_PIECES = 16;

        // Cell meshes have 16-bit indices.
        private const int MAX_CELL_VERTICES = ushort.MaxValue;

        // Pieces whose bounding boxes come this close (m) are neighbours: either may cover some of the
        // other's interior faces.
        private const float NEIGHBOUR_GAP = 0.01f;

        // Mobile tier: a loose body whose bounding box diagonal is shorter than this (m) casts no shadow.
        private const float SMALL_BODY_SIZE = 0.3f;

        // Milliseconds per frame, shared by all walls, spent reading the pieces' meshes at the start.
        private const double READ_BUDGET_MS = 1.0;

        // Every standing piece is put through the in-wall test once over this many frames.
        private const int VALIDATE_FRAMES = 30;

        private const MeshUpdateFlags UPDATE_FLAGS = MeshUpdateFlags.DontValidateIndices
                                                     | MeshUpdateFlags.DontRecalculateBounds
                                                     | MeshUpdateFlags.DontNotifyMeshUsers
                                                     | MeshUpdateFlags.DontResetBoneBounds;

        private static readonly VertexAttributeDescriptor[] VertexLayout =
        {
            new(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3)
        };

        private static readonly List<IntactWallRenderer> Walls = new();
        private static readonly List<GameObject> SceneRoots = new();
        private static readonly List<RayfireRigid> SceneRigids = new();
        private static readonly List<Vector3> Positions = new();
        private static readonly List<Vector3> Normals = new();
        private static readonly List<int> Indices = new();
        private static readonly List<int> Touched = new();
        private static readonly List<int> TextureIds = new();
        private static readonly SubMeshDescriptor[] SubMeshes = new SubMeshDescriptor[32];
        private static readonly System.Diagnostics.Stopwatch ReadClock = new();

        // Shared by every wall: one cell is rebuilt at a time.
        private static CellVertex[] _stagingVertices = Array.Empty<CellVertex>();
        private static ushort[] _stagingIndices = Array.Empty<ushort>();
        private static int[] _remap = Array.Empty<int>();
        private static int _readFrame = -1;
        private static double _readSpentMs;

        private enum Stage
        {
            WaitingForRayFire,
            Reading,
            Ready
        }

        // One vertex of a cell mesh, in world space: the walls' plain materials need nothing else.
        private struct CellVertex
        {
            public Vector3 Position;
            public Vector3 Normal;
        }

        // The triangles of one material slot of one piece, its vertices copied out of the piece's mesh.
        private struct Slice
        {
            public int Material;
            public int VertexStart;
            public int VertexCount;
            public int IndexStart;
            public int IndexCount;
        }

        private sealed class Piece
        {
            public RayfireRigid Rigid;
            public Transform Transform;
            public GameObject GameObject;
            public MeshRenderer Renderer;
            public Bounds Bounds;

            // Where the piece stood when it was read: its cached triangles are there.
            public Vector3 Position;
            public Quaternion Rotation;

            public int[] Neighbours = Array.Empty<int>();
            public int SliceStart;
            public int SliceCount;
            public int VertexTotal;
            public int IndexTotal;
            public int Cell = -1;

            // Has slices and the wall's renderer settings: a cell can draw it.
            public bool Eligible;
            public bool ShattersOnDamage;
            public bool HasInterior;
            public bool InWall = true;

            // Drawn by its cell rather than by its own renderer, while it stands.
            public bool Batched;

            // Its own renderer is switched off by this component.
            public bool Hidden;

            // A neighbour has left, so its interior faces can be seen.
            public bool InteriorShown;
        }

        private sealed class Cell
        {
            public int[] Pieces;
            public Bounds Bounds;
            public int VertexCapacity;
            public int IndexCapacity;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public bool Allocated;
            public bool Dirty;
            public int MaterialMask;
        }

        private readonly Dictionary<RayfireRigid, Piece> _pieceOf = new();
        private readonly List<Material> _materials = new();
        private readonly List<bool> _combinable = new();
        private readonly List<bool> _interior = new();
        private readonly Dictionary<int, Material[]> _materialSets = new();
        private readonly List<Slice> _readSlices = new();
        private readonly List<CellVertex> _readVertices = new();
        private readonly List<ushort> _readIndices = new();
        private readonly List<Cell> _dirty = new();
        private readonly List<Piece> _released = new();
        private readonly HashSet<RFPhysic> _standingPhysics = new();

        private RayfireRigid _root;
        private RayfireConnectivity _connectivity;
        private Transform _wall;
        private Stage _stage;
        private Piece[] _pieces = Array.Empty<Piece>();
        private Slice[] _slices = Array.Empty<Slice>();
        private CellVertex[] _vertices = Array.Empty<CellVertex>();
        private ushort[] _indices = Array.Empty<ushort>();
        private bool[] _interiorMaterial = Array.Empty<bool>();
        private Cell[] _cells = Array.Empty<Cell>();
        private GameObject _cellRoot;
        private Predicate<RFPhysic> _isStandingPhysics;
        private RFEvent.EventActionMeshRoot _onPieceActivated;
        private RFConnectivityEvent.EventAction _onConnectivity;
        private RFEvent.EventAction _onDemolished;
        private Matrix4x4 _readToWorld;
        private Matrix4x4 _readNormalToWorld;

        // Renderer settings every combined piece shares, taken from the first one; the cells copy them.
        private bool _hasSettings;
        private ShadowCastingMode _shadowCasting;
        private bool _receiveShadows;
        private LightProbeUsage _lightProbes;
        private ReflectionProbeUsage _reflectionProbes;
        private uint _renderingLayers;
        private bool _occlusion;
        private int _layer;

        private int _readCursor;
        private int _validateCursor;
        private int _shardCount;
        private bool _hooked;
        private bool _applied;
        private bool _appliedMobile;
        private bool _scanPending;

        private void Awake()
        {
            _root = GetComponent<RayfireRigid>();
            _connectivity = GetComponent<RayfireConnectivity>();
            _wall = transform;
            _isStandingPhysics = _standingPhysics.Contains;
            _onPieceActivated = OnPieceActivated;
            _onConnectivity = OnConnectivity;
            _onDemolished = OnDemolished;
        }

        private void OnEnable()
        {
            if (Walls.Count == 0)
                Camera.onPreCull += FlushBeforeCulling;
            Walls.Add(this);

            // Whatever left the wall while this was switched off turns up in the next scan.
            Hook();
            _scanPending = true;
        }

        private void OnDisable()
        {
            Unapply();
            Unhook();

            Walls.Remove(this);
            if (Walls.Count == 0)
                Camera.onPreCull -= FlushBeforeCulling;
        }

        private void OnDestroy()
        {
            // OnDisable has already handed the pieces their renderers back.
            for (int i = 0; i < _cells.Length; i++)
                if (_cells[i].Mesh != null)
                    Destroy(_cells[i].Mesh);

            if (_cellRoot != null)
                Destroy(_cellRoot);
        }

        // Unity sends this when a piece is reparented (into a chunk RayFire cuts off) or destroyed.
        private void OnTransformChildrenChanged() => _scanPending = true;

        // Last in the frame: CollapseUnderOwnWeight and RayFire's coroutines let pieces go before this.
        private void LateUpdate()
        {
            if (!Enabled)
            {
                Unapply();
                return;
            }

            if (_stage != Stage.Ready && !Prepare())
                return;

            if (!_applied)
                Apply();

            // RayFire drops every piece it takes out from the wall's shard list: a change none of the
            // hooks saw still means a piece left.
            int shardCount = ShardCount();
            if (shardCount != _shardCount)
            {
                _shardCount = shardCount;
                _scanPending = true;
            }

            FollowTier();
            Validate();
            Flush();
        }

        // Normally LateUpdate has rebuilt everything; this catches pieces let go after it, before any
        // camera culls. It runs for every camera, every frame, so it only acts on what is still pending.
        private static void FlushBeforeCulling(Camera camera)
        {
            for (int i = 0; i < Walls.Count; i++)
            {
                IntactWallRenderer wall = Walls[i];
                if (wall._applied && (wall._scanPending || wall._dirty.Count > 0))
                    wall.Flush();
            }
        }

        // Waits until RayFire has set the wall up (it reads the pieces' renderer bounds while it does),
        // then reads the pieces' meshes over as many frames as that takes. True once the cells exist.
        private bool Prepare()
        {
            if (_stage == Stage.WaitingForRayFire)
            {
                if (!RayFireDone())
                    return false;

                ListPieces();
                _stage = Stage.Reading;
            }

            if (!ReadPieces())
                return false;

            FinishReading();
            _stage = Stage.Ready;
            Hook();
            return true;
        }

        private bool RayFireDone() =>
            _root != null && _connectivity != null && _root.initialized && _root.HasFragments && ShardCount() > 0;

        private int ShardCount()
        {
            List<RFShard> shards = _connectivity != null ? _connectivity.cluster?.shards : null;
            return shards?.Count ?? 0;
        }

        private void ListPieces()
        {
            List<RayfireRigid> fragments = _root.fragments;
            var pieces = new List<Piece>(fragments.Count);

            for (int i = 0; i < fragments.Count; i++)
            {
                RayfireRigid rigid = fragments[i];
                if (rigid == null || _pieceOf.ContainsKey(rigid))
                    continue;

                var piece = new Piece
                {
                    Rigid = rigid,
                    Transform = rigid.transform,
                    GameObject = rigid.gameObject
                };

                // Every piece's box up front, so one gone before it is read still has its neighbours.
                MeshFilter filter = rigid.mFlt != null ? rigid.mFlt : rigid.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                piece.Bounds = mesh != null
                    ? WorldBounds(mesh.bounds, piece.Transform.localToWorldMatrix)
                    : new Bounds(piece.Transform.position, Vector3.zero);

                _pieceOf.Add(rigid, piece);
                pieces.Add(piece);
            }

            _pieces = pieces.ToArray();
            _readCursor = 0;
        }

        // Reads pieces until this frame's share of the budget is spent, so the scene does not hitch
        // as it starts. True once every piece is read.
        private bool ReadPieces()
        {
            if (_readFrame != Time.frameCount)
            {
                _readFrame = Time.frameCount;
                _readSpentMs = 0;
            }

            if (_readSpentMs >= READ_BUDGET_MS)
                return false;

            ReadClock.Restart();
            while (_readCursor < _pieces.Length)
            {
                Read(_pieces[_readCursor++]);
                if (_readSpentMs + ReadClock.Elapsed.TotalMilliseconds >= READ_BUDGET_MS)
                    break;
            }

            _readSpentMs += ReadClock.Elapsed.TotalMilliseconds;
            return _readCursor >= _pieces.Length;
        }

        // Copies the piece's triangles, material slot by material slot, into the wall's store in world
        // space. A piece the cells cannot draw exactly as its own renderer does keeps that renderer.
        private void Read(Piece piece)
        {
            RayfireRigid rigid = piece.Rigid;
            if (rigid == null)
                return;

            MeshFilter filter = rigid.mFlt != null ? rigid.mFlt : rigid.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            MeshRenderer renderer = rigid.mRnd != null ? rigid.mRnd : rigid.GetComponent<MeshRenderer>();
            piece.Renderer = renderer;
            piece.ShattersOnDamage = rigid.dmlTp != DemolitionType.None;

            // The in-wall test compares against this. Not Transform.hasChanged: any write sets that,
            // the same position written again included.
            piece.Transform.GetPositionAndRotation(out piece.Position, out piece.Rotation);

            if (renderer == null || mesh == null || !CanCombine(renderer, mesh))
                return;

            // Grouped by material, not by submesh: the interior faces are not always the second submesh.
            // A slot past the last submesh draws the last submesh again, as Unity does.
            Material[] materials = renderer.sharedMaterials;
            int subMeshCount = mesh.subMeshCount;
            if (materials.Length == 0 || subMeshCount == 0)
                return;

            for (int slot = 0; slot < materials.Length; slot++)
            {
                int subMesh = Mathf.Min(slot, subMeshCount - 1);
                if (materials[slot] == null || MaterialIndex(materials[slot]) < 0
                    || mesh.GetTopology(subMesh) != MeshTopology.Triangles)
                    return;
            }

            mesh.GetVertices(Positions);
            mesh.GetNormals(Normals);
            if (Normals.Count != Positions.Count)
                return;

            _readToWorld = piece.Transform.localToWorldMatrix;
            _readNormalToWorld = _readToWorld.inverse.transpose;
            bool mirrored = _readToWorld.determinant < 0f;
            GrowRemap(Positions.Count);

            int firstSlice = _readSlices.Count;
            int firstVertex = _readVertices.Count;
            int firstIndex = _readIndices.Count;
            int vertexTotal = 0;
            int indexTotal = 0;

            for (int slot = 0; slot < materials.Length; slot++)
            {
                mesh.GetIndices(Indices, Mathf.Min(slot, subMeshCount - 1));

                var slice = new Slice
                {
                    Material = MaterialIndex(materials[slot]),
                    VertexStart = _readVertices.Count,
                    IndexStart = _readIndices.Count
                };

                // A mirrored piece's triangles wind the other way once they are in world space.
                int next = 0;
                for (int t = 0; t + 2 < Indices.Count; t += 3)
                {
                    _readIndices.Add(SliceIndex(Indices[t], ref next));
                    _readIndices.Add(SliceIndex(Indices[mirrored ? t + 2 : t + 1], ref next));
                    _readIndices.Add(SliceIndex(Indices[mirrored ? t + 1 : t + 2], ref next));
                }

                for (int i = 0; i < Touched.Count; i++)
                    _remap[Touched[i]] = -1;
                Touched.Clear();

                slice.VertexCount = next;
                slice.IndexCount = _readIndices.Count - slice.IndexStart;
                if (slice.IndexCount == 0)
                    continue;

                _readSlices.Add(slice);
                vertexTotal += slice.VertexCount;
                indexTotal += slice.IndexCount;
            }

            // Nothing to draw, or more than one cell can index: the piece keeps its own renderer.
            if (_readSlices.Count == firstSlice || vertexTotal > MAX_CELL_VERTICES)
            {
                _readSlices.RemoveRange(firstSlice, _readSlices.Count - firstSlice);
                _readVertices.RemoveRange(firstVertex, _readVertices.Count - firstVertex);
                _readIndices.RemoveRange(firstIndex, _readIndices.Count - firstIndex);
                return;
            }

            piece.Eligible = true;
            piece.SliceStart = firstSlice;
            piece.SliceCount = _readSlices.Count - firstSlice;
            piece.VertexTotal = vertexTotal;
            piece.IndexTotal = indexTotal;
            for (int s = firstSlice; s < _readSlices.Count; s++)
                piece.HasInterior |= _interior[_readSlices[s].Material];
        }

        // The slice-local index of a mesh vertex; its first use copies it, moved into the world, to the store.
        private ushort SliceIndex(int vertex, ref int next)
        {
            int local = _remap[vertex];
            if (local < 0)
            {
                local = next++;
                _remap[vertex] = local;
                Touched.Add(vertex);

                _readVertices.Add(new CellVertex
                {
                    Position = _readToWorld.MultiplyPoint3x4(Positions[vertex]),
                    Normal = _readNormalToWorld.MultiplyVector(Normals[vertex]).normalized
                });
            }

            return (ushort)local;
        }

        private static void GrowRemap(int vertexCount)
        {
            if (_remap.Length >= vertexCount)
                return;

            _remap = new int[Mathf.NextPowerOfTwo(vertexCount)];
            for (int i = 0; i < _remap.Length; i++)
                _remap[i] = -1;
        }

        // Whether a cell can draw the piece exactly as its own renderer does. The settings of the first
        // piece that can become the wall's; a piece set up differently keeps its own renderer.
        private bool CanCombine(MeshRenderer renderer, Mesh mesh)
        {
            if (!renderer.enabled || renderer.forceRenderingOff || !mesh.isReadable || mesh.vertexCount == 0
                || (renderer.lightmapIndex >= 0 && renderer.lightmapIndex < 0xFFFE))
                return false;

            if (!_hasSettings)
            {
                _hasSettings = true;
                _shadowCasting = renderer.shadowCastingMode;
                _receiveShadows = renderer.receiveShadows;
                _lightProbes = renderer.lightProbeUsage;
                _reflectionProbes = renderer.reflectionProbeUsage;
                _renderingLayers = renderer.renderingLayerMask;
                _occlusion = renderer.allowOcclusionWhenDynamic;
                _layer = renderer.gameObject.layer;
                return true;
            }

            return renderer.shadowCastingMode == _shadowCasting
                   && renderer.receiveShadows == _receiveShadows
                   && renderer.lightProbeUsage == _lightProbes
                   && renderer.reflectionProbeUsage == _reflectionProbes
                   && renderer.renderingLayerMask == _renderingLayers
                   && renderer.gameObject.layer == _layer;
        }

        // The material's place in the wall's table, -1 when a cell cannot draw it.
        private int MaterialIndex(Material material)
        {
            int index = _materials.IndexOf(material);
            if (index < 0)
            {
                index = _materials.Count;
                _materials.Add(material);
                _combinable.Add(index < SubMeshes.Length && !HasTexture(material));
                _interior.Add(IsInterior(material));
            }

            return _combinable[index] ? index : -1;
        }

        // RayFire gives the faces it cuts through a wall the wall's interior material: concrete_interior
        // on every wall here. The component installs itself with no settings, so the material is known by
        // RayFire's own reference when the wall has one, and by its name otherwise.
        private bool IsInterior(Material material) =>
            (_root.materials.iMat != null && material == _root.materials.iMat)
            || material.name.IndexOf("interior", StringComparison.OrdinalIgnoreCase) >= 0;

        // The cells carry positions and normals only: enough for the walls' plain materials, not for a
        // texture, so a textured material keeps its pieces on their own renderers.
        private static bool HasTexture(Material material)
        {
            material.GetTexturePropertyNameIDs(TextureIds);
            for (int i = 0; i < TextureIds.Count; i++)
                if (material.GetTexture(TextureIds[i]) != null)
                    return true;

            return false;
        }

        private void FinishReading()
        {
            _slices = _readSlices.ToArray();
            _vertices = _readVertices.ToArray();
            _indices = _readIndices.ToArray();
            _interiorMaterial = _interior.ToArray();

            _readSlices.Clear();
            _readSlices.TrimExcess();
            _readVertices.Clear();
            _readVertices.TrimExcess();
            _readIndices.Clear();
            _readIndices.TrimExcess();

            FindNeighbours();
            SplitIntoCells();
            CreateCellObjects();
        }

        // Neighbours by bounding boxes, grown by the gap: sort and sweep along the wall's longest axis.
        private void FindNeighbours()
        {
            int count = _pieces.Length;
            if (count == 0)
                return;

            Bounds wall = _pieces[0].Bounds;
            for (int i = 1; i < count; i++)
                wall.Encapsulate(_pieces[i].Bounds);
            int axis = LongestAxis(wall.size);

            var boxes = new Bounds[count];
            var order = new int[count];
            var starts = new float[count];
            var found = new List<int>[count];
            for (int i = 0; i < count; i++)
            {
                Bounds box = _pieces[i].Bounds;
                box.Expand(2f * NEIGHBOUR_GAP);
                boxes[i] = box;
                order[i] = i;
                starts[i] = box.min[axis];
                found[i] = new List<int>();
            }

            Array.Sort(starts, order);
            for (int i = 0; i < count; i++)
            {
                int a = order[i];
                float end = boxes[a].max[axis];
                for (int j = i + 1; j < count && starts[j] <= end; j++)
                {
                    int b = order[j];
                    if (!boxes[a].Intersects(boxes[b]))
                        continue;

                    found[a].Add(b);
                    found[b].Add(a);
                }
            }

            for (int i = 0; i < count; i++)
                _pieces[i].Neighbours = found[i].ToArray();
        }

        private void SplitIntoCells()
        {
            var members = new List<int>(_pieces.Length);
            for (int i = 0; i < _pieces.Length; i++)
                if (_pieces[i].Eligible)
                    members.Add(i);

            int[] order = members.ToArray();
            var cells = new List<Cell>();
            Split(order, new float[order.Length], 0, order.Length, cells);
            _cells = cells.ToArray();

            int vertices = 0;
            int indices = 0;
            for (int i = 0; i < _cells.Length; i++)
            {
                vertices = Mathf.Max(vertices, _cells[i].VertexCapacity);
                indices = Mathf.Max(indices, _cells[i].IndexCapacity);
            }

            if (_stagingVertices.Length < vertices)
                _stagingVertices = new CellVertex[vertices];
            if (_stagingIndices.Length < indices)
                _stagingIndices = new ushort[indices];
        }

        // Halves a group of pieces at the median of their centres along its widest spread until each
        // part is small enough for one cell.
        private void Split(int[] order, float[] keys, int start, int count, List<Cell> cells)
        {
            if (count == 0)
                return;

            var spread = new Bounds(_pieces[order[start]].Bounds.center, Vector3.zero);
            int vertices = 0;
            for (int i = start; i < start + count; i++)
            {
                Piece piece = _pieces[order[i]];
                spread.Encapsulate(piece.Bounds.center);
                vertices += piece.VertexTotal;
            }

            int axis = LongestAxis(spread.size);
            bool split = count > 1
                         && (count > MAX_CELL_PIECES
                             || vertices > MAX_CELL_VERTICES
                             || (count > MIN_CELL_PIECES && spread.size[axis] > MAX_CELL_SPREAD));

            if (split)
            {
                for (int i = start; i < start + count; i++)
                    keys[i] = _pieces[order[i]].Bounds.center[axis];
                Array.Sort(keys, order, start, count);

                int half = count / 2;
                Split(order, keys, start, half, cells);
                Split(order, keys, start + half, count - half, cells);
                return;
            }

            var cell = new Cell { Pieces = new int[count] };
            for (int i = 0; i < count; i++)
            {
                Piece piece = _pieces[order[start + i]];
                piece.Cell = cells.Count;
                cell.Pieces[i] = order[start + i];
                cell.VertexCapacity += piece.VertexTotal;
                cell.IndexCapacity += piece.IndexTotal;

                if (i == 0)
                    cell.Bounds = piece.Bounds;
                else
                    cell.Bounds.Encapsulate(piece.Bounds);
            }

            cells.Add(cell);
        }

        // The cells sit outside the wall: a new child would make RayfireConnectivity re-check the
        // whole wall. Their meshes are in world space, so they need no transform of their own.
        private void CreateCellObjects()
        {
            if (_cells.Length == 0)
                return;

            _cellRoot = new GameObject(name + " (combined)");
            Transform parent = _cellRoot.transform;

            for (int c = 0; c < _cells.Length; c++)
            {
                Cell cell = _cells[c];
                var cellObject = new GameObject("Cell " + c) { layer = _layer };
                cellObject.transform.SetParent(parent, false);

                cell.Mesh = new Mesh { name = name + " cell " + c };
                cell.Mesh.MarkDynamic();
                cellObject.AddComponent<MeshFilter>().sharedMesh = cell.Mesh;

                MeshRenderer renderer = cellObject.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = _shadowCasting;
                renderer.receiveShadows = _receiveShadows;
                renderer.lightProbeUsage = _lightProbes;
                renderer.reflectionProbeUsage = _reflectionProbes;
                renderer.renderingLayerMask = _renderingLayers;
                renderer.allowOcclusionWhenDynamic = _occlusion;

                // A cell never moves: only the camera's own motion goes into motion vectors.
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
                renderer.enabled = false;
                cell.Renderer = renderer;
            }
        }

        // Hands the standing pieces over to the cells.
        private void Apply()
        {
            _applied = true;
            _appliedMobile = PerformanceTier.IsMobile;
            _shardCount = ShardCount();

            // Whatever left while the wall was being read or switched off.
            Scan();

            for (int i = 0; i < _pieces.Length; i++)
            {
                Piece piece = _pieces[i];
                piece.Batched = Combines(piece, _appliedMobile);
                if (piece.InWall && piece.Batched)
                    Hide(piece);
            }

            StopVelocityCache();

            for (int i = 0; i < _cells.Length; i++)
                MarkDirty(_cells[i]);
        }

        // Gives every standing piece its own renderer back and switches the cells off.
        private void Unapply()
        {
            if (!_applied)
                return;

            _applied = false;

            for (int i = 0; i < _pieces.Length; i++)
            {
                Piece piece = _pieces[i];
                Show(piece);
                if (piece.InWall)
                    ResumeVelocityCache(piece);
            }

            for (int i = 0; i < _cells.Length; i++)
            {
                Cell cell = _cells[i];
                cell.Dirty = false;
                if (cell.Renderer != null)
                    cell.Renderer.enabled = false;
            }

            _dirty.Clear();
            _released.Clear();
        }

        // RayFire only shatters a piece someone can see (RayfireRigid.DemolitionState reads
        // Renderer.isVisible), and a piece drawn by its cell is never seen. In the editor RayFire's
        // runtime fracture does shatter the sticky walls' bricks when they are hit, so on desktop such
        // pieces keep their own renderers and play exactly as before. Phones have no runtime fracture.
        private static bool Combines(Piece piece, bool mobile) =>
            piece.Eligible && (mobile || !piece.ShattersOnDamage);

        private static void Hide(Piece piece)
        {
            if (piece.Hidden || piece.Renderer == null)
                return;

            // forceRenderingOff rather than enabled = false: the renderer stays in the scene, so its
            // bounds stay right for RayFire and the game code that read them.
            piece.Renderer.forceRenderingOff = true;
            piece.Hidden = true;
        }

        private static void Show(Piece piece)
        {
            if (!piece.Hidden)
                return;

            piece.Hidden = false;
            if (piece.Renderer != null)
                piece.Renderer.forceRenderingOff = false;
        }

        private void Hook()
        {
            if (_hooked || _stage != Stage.Ready)
                return;

            _hooked = true;
            _root.activationEvent.LocalEventMeshRoot += _onPieceActivated;
            _connectivity.connectivityEvent.LocalEvent += _onConnectivity;
            RFDemolitionEvent.GlobalEvent += _onDemolished;
        }

        private void Unhook()
        {
            if (!_hooked)
                return;

            _hooked = false;
            _root.activationEvent.LocalEventMeshRoot -= _onPieceActivated;
            _connectivity.connectivityEvent.LocalEvent -= _onConnectivity;
            RFDemolitionEvent.GlobalEvent -= _onDemolished;
        }

        // RayFire raises it on the wall for every piece it activates, and for every chunk connectivity
        // cuts off.
        private void OnPieceActivated(RayfireRigid rigid, RayfireRigid meshRoot)
        {
            if (rigid is not null && _pieceOf.TryGetValue(rigid, out Piece piece))
            {
                Release(piece);
                return;
            }

            // Not a piece: a chunk being put together. Its pieces are already flagged as activated but
            // not yet moved under it, and setting the chunk up reads their bounds right after this.
            for (int i = 0; i < _pieces.Length; i++)
            {
                Piece candidate = _pieces[i];
                if (candidate.InWall && (candidate.Rigid.act.activated || candidate.Rigid.lim.demolished))
                    Release(candidate);
            }

            _scanPending = true;
        }

        // After a connectivity check: the pieces it set loose on their own and in chunks.
        private void OnConnectivity(RayfireConnectivity connectivity, List<RFShard> solo, List<RFCluster> clusters)
        {
            if (solo != null)
                for (int i = 0; i < solo.Count; i++)
                    if (solo[i] != null)
                        Release(solo[i].rigid);

            if (clusters == null)
                return;

            for (int c = 0; c < clusters.Count; c++)
            {
                List<RFShard> shards = clusters[c]?.shards;
                if (shards == null)
                    continue;

                for (int s = 0; s < shards.Count; s++)
                    if (shards[s] != null)
                        Release(shards[s].rigid);
            }
        }

        // RayFire's demolition leaves no activation behind; nothing demolishes a standing piece today,
        // but a piece that is demolished has left.
        private void OnDemolished(RayfireRigid rigid)
        {
            if (rigid is not null && rigid.meshRoot == _root)
                Release(rigid);
        }

        // Lets go of every piece that no longer passes the in-wall test.
        private void Scan()
        {
            _scanPending = false;

            for (int i = 0; i < _pieces.Length; i++)
            {
                Piece piece = _pieces[i];
                if (piece.InWall && !StaysInWall(piece))
                    Release(piece);
            }
        }

        /// <summary>
        /// The in-wall test: RayFire has neither activated nor demolished the piece, it is kinematic,
        /// switched on, still right under the wall (not moved into a chunk) and has not moved since it
        /// was read.
        /// </summary>
        private bool StaysInWall(Piece piece)
        {
            RayfireRigid rigid = piece.Rigid;
            if (rigid == null || rigid.act.activated || rigid.lim.demolished
                || rigid.simTp == SimType.Dynamic || rigid.simTp == SimType.Sleeping)
                return false;

            Transform pieceTransform = piece.Transform;
            if (pieceTransform.parent != _wall || !piece.GameObject.activeInHierarchy)
                return false;

            pieceTransform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
            if (position != piece.Position || rotation != piece.Rotation)
                return false;

            Rigidbody body = rigid.physics.rb;
            return body == null || body.isKinematic;
        }

        private bool Release(RayfireRigid rigid) =>
            rigid is not null && _pieceOf.TryGetValue(rigid, out Piece piece) && Release(piece);

        // The piece draws itself again right away, before anything reads its bounds or draws it; its
        // cell drops it, and the cells of its neighbours show the faces it was covering.
        private bool Release(Piece piece)
        {
            if (!piece.InWall)
                return false;

            piece.InWall = false;
            Show(piece);

            int[] neighbours = piece.Neighbours;
            for (int i = 0; i < neighbours.Length; i++)
            {
                Piece neighbour = _pieces[neighbours[i]];
                if (!neighbour.InWall || neighbour.InteriorShown)
                    continue;

                neighbour.InteriorShown = true;
                if (_applied && neighbour.HasInterior && neighbour.Cell >= 0)
                    MarkDirty(_cells[neighbour.Cell]);
            }

            // Switched off, the cells are rebuilt in full when they come back on.
            if (!_applied)
                return true;

            if (piece.Cell >= 0)
                MarkDirty(_cells[piece.Cell]);

            ResumeVelocityCache(piece);
            _released.Add(piece);
            return true;
        }

        private void MarkDirty(Cell cell)
        {
            if (cell.Dirty)
                return;

            cell.Dirty = true;
            _dirty.Add(cell);
        }

        // Pieces that combine on phones only follow the tier, which the editor's quality level can
        // change mid-game.
        private void FollowTier()
        {
            if (Time.frameCount % VALIDATE_FRAMES != 0)
                return;

            bool mobile = PerformanceTier.IsMobile;
            if (mobile == _appliedMobile)
                return;

            _appliedMobile = mobile;
            for (int i = 0; i < _pieces.Length; i++)
            {
                Piece piece = _pieces[i];
                bool batched = Combines(piece, mobile);
                if (batched == piece.Batched)
                    continue;

                piece.Batched = batched;
                if (!piece.InWall)
                    continue;

                if (batched)
                    Hide(piece);
                else
                    Show(piece);

                MarkDirty(_cells[piece.Cell]);
            }
        }

        // A way out of the wall the hooks miss would leave a ghost piece in its cell while the real one
        // flies off unseen. Every standing piece goes through the in-wall test once every
        // VALIDATE_FRAMES frames; one that fails is let go, and reported in development builds so the
        // missed way shows up in testing.
        private void Validate()
        {
            int count = _pieces.Length;
            if (count == 0)
                return;

            int perFrame = (count + VALIDATE_FRAMES - 1) / VALIDATE_FRAMES;
            for (int i = 0; i < perFrame; i++)
            {
                Piece piece = _pieces[_validateCursor];
                _validateCursor = (_validateCursor + 1) % count;

                if (!piece.InWall || StaysInWall(piece))
                    continue;

                if (piece.Hidden && Debug.isDebugBuild)
                    Debug.LogError("[IntactWallRenderer] " + name + ": "
                                   + (piece.GameObject != null ? piece.GameObject.name : "a destroyed piece")
                                   + " left the wall unnoticed and was still drawn by its cell.", this);

                Release(piece);
            }
        }

        // RayfireMan reads the velocity of every piece's rigidbody each frame, for demolition and
        // velocity activation (no wall activates by velocity). A standing piece is kinematic and never
        // moves, so it comes off that list, and goes back on when it leaves the wall.
        private void StopVelocityCache()
        {
            RayfireMan manager = RayfireMan.inst;
            if (manager == null)
                return;

            for (int i = 0; i < _pieces.Length; i++)
            {
                Piece piece = _pieces[i];
                if (!piece.InWall)
                    continue;

                RFPhysic physics = piece.Rigid.physics;
                physics.velCache = false;
                _standingPhysics.Add(physics);
            }

            manager.physicList.RemoveAll(_isStandingPhysics);
            _standingPhysics.Clear();
        }

        private static void ResumeVelocityCache(Piece piece)
        {
            RayfireMan manager = RayfireMan.inst;
            if (manager != null && piece.Rigid != null)
                manager.AddToPhysicCor(piece.Rigid);
        }

        private void Flush()
        {
            if (!_applied)
                return;

            if (_scanPending)
                Scan();

            for (int i = 0; i < _dirty.Count; i++)
            {
                Cell cell = _dirty[i];
                cell.Dirty = false;
                Rebuild(cell);
            }

            _dirty.Clear();
            DropSmallShadows();
        }

        // Concatenates the slices of the cell's standing pieces, material by material, into the shared
        // staging buffers and uploads them in one go: one submesh per material that has triangles. The
        // mesh buffers are sized once for every slice of the cell, so a rebuild only rewrites data.
        private void Rebuild(Cell cell)
        {
            if (cell.Renderer == null)
                return;

            int vertexCount = 0;
            int indexCount = 0;
            int subMeshCount = 0;
            int mask = 0;

            for (int m = 0; m < _interiorMaterial.Length; m++)
            {
                int firstVertex = vertexCount;
                int firstIndex = indexCount;
                bool interior = _interiorMaterial[m];

                for (int p = 0; p < cell.Pieces.Length; p++)
                {
                    Piece piece = _pieces[cell.Pieces[p]];
                    if (!piece.InWall || !piece.Batched || (interior && !piece.InteriorShown))
                        continue;

                    int end = piece.SliceStart + piece.SliceCount;
                    for (int s = piece.SliceStart; s < end; s++)
                    {
                        Slice slice = _slices[s];
                        if (slice.Material != m)
                            continue;

                        Array.Copy(_vertices, slice.VertexStart, _stagingVertices, vertexCount, slice.VertexCount);
                        for (int i = 0; i < slice.IndexCount; i++)
                            _stagingIndices[indexCount + i] = (ushort)(_indices[slice.IndexStart + i] + vertexCount);

                        vertexCount += slice.VertexCount;
                        indexCount += slice.IndexCount;
                    }
                }

                if (indexCount == firstIndex)
                    continue;

                SubMeshes[subMeshCount++] = new SubMeshDescriptor(firstIndex, indexCount - firstIndex)
                {
                    firstVertex = firstVertex,
                    vertexCount = vertexCount - firstVertex,
                    bounds = cell.Bounds
                };
                mask |= 1 << m;
            }

            if (subMeshCount == 0)
            {
                cell.Renderer.enabled = false;
                return;
            }

            Mesh mesh = cell.Mesh;
            if (!cell.Allocated)
            {
                mesh.SetVertexBufferParams(cell.VertexCapacity, VertexLayout);
                mesh.SetIndexBufferParams(cell.IndexCapacity, IndexFormat.UInt16);
                cell.Allocated = true;
            }

            mesh.SetVertexBufferData(_stagingVertices, 0, 0, vertexCount, 0, UPDATE_FLAGS);
            mesh.SetIndexBufferData(_stagingIndices, 0, 0, indexCount, UPDATE_FLAGS);
            mesh.subMeshCount = subMeshCount;
            for (int i = 0; i < subMeshCount; i++)
                mesh.SetSubMesh(i, SubMeshes[i], UPDATE_FLAGS);

            // Fixed: the box of all the cell's pieces, however many are left.
            mesh.bounds = cell.Bounds;

            if (mask != cell.MaterialMask)
            {
                cell.Renderer.sharedMaterials = MaterialsFor(mask);
                cell.MaterialMask = mask;
            }

            cell.Renderer.enabled = true;
        }

        private Material[] MaterialsFor(int mask)
        {
            if (_materialSets.TryGetValue(mask, out Material[] set))
                return set;

            int count = 0;
            for (int m = 0; m < _interiorMaterial.Length; m++)
                if ((mask & (1 << m)) != 0)
                    count++;

            set = new Material[count];
            count = 0;
            for (int m = 0; m < _interiorMaterial.Length; m++)
                if ((mask & (1 << m)) != 0)
                    set[count++] = _materials[m];

            _materialSets.Add(mask, set);
            return set;
        }

        // Mobile tier: a small loose body casts no shadow, a draw call in every shadow cascade for a
        // speck nobody notices. A piece that came off in a chunk is as big as the chunk. Desktop keeps
        // every shadow.
        private void DropSmallShadows()
        {
            if (_released.Count == 0)
                return;

            if (PerformanceTier.IsMobile)
            {
                for (int i = 0; i < _released.Count; i++)
                {
                    Piece piece = _released[i];
                    if (piece.Rigid != null && piece.Renderer != null && BodySize(piece) < SMALL_BODY_SIZE)
                        piece.Renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            _released.Clear();
        }

        private float BodySize(Piece piece)
        {
            Transform parent = piece.Transform.parent;
            if (parent != null && parent != _wall && parent.TryGetComponent(out RayfireRigid chunk) && chunk.IsCluster)
                return chunk.lim.bboxSize;

            return piece.Rigid.lim.bboxSize;
        }

        private static int LongestAxis(Vector3 size) =>
            size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;

        // The world box around a mesh's local bounds, the way Renderer.bounds is made.
        private static Bounds WorldBounds(Bounds local, Matrix4x4 toWorld)
        {
            Vector3 extents = local.extents;
            Vector3 x = toWorld.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            Vector3 y = toWorld.MultiplyVector(new Vector3(0f, extents.y, 0f));
            Vector3 z = toWorld.MultiplyVector(new Vector3(0f, 0f, extents.z));
            var worldExtents = new Vector3(
                Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));

            return new Bounds(toWorld.MultiplyPoint3x4(local.center), worldExtents * 2f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Walls.Clear();
            Camera.onPreCull -= FlushBeforeCulling;
            _readFrame = -1;
            _readSpentMs = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void WatchSceneLoads()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // Also for a scene that was already open without being loaded again (play mode without scene reload).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallInLoadedScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Install(SceneManager.GetSceneAt(i));
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Install(scene);

        // Every RayFire MeshRoot wall with connectivity gets one, unless it has it already.
        private static void Install(Scene scene)
        {
            // The sceneLoaded subscription outlives play mode in the editor (no domain reload when
            // play stops); a component added to an edit-mode scene would be saved with it.
            if (!Enabled || !scene.isLoaded || !Application.isPlaying)
                return;

            scene.GetRootGameObjects(SceneRoots);
            for (int r = 0; r < SceneRoots.Count; r++)
            {
                SceneRoots[r].GetComponentsInChildren(true, SceneRigids);
                for (int i = 0; i < SceneRigids.Count; i++)
                {
                    RayfireRigid rigid = SceneRigids[i];
                    if (rigid.objTp == ObjectType.MeshRoot
                        && rigid.TryGetComponent(out RayfireConnectivity _)
                        && !rigid.TryGetComponent(out IntactWallRenderer _))
                        rigid.gameObject.AddComponent<IntactWallRenderer>();
                }
            }

            SceneRoots.Clear();
            SceneRigids.Clear();
        }
    }
}
