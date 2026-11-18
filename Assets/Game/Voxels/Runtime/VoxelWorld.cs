using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// The scene's voxel bookkeeping, created on first use: every live body, the shared materials,
    /// the debris cubes, and the once-a-frame rebuild. Carves during the frame (bullets, blasts,
    /// rams) only mark chunks dirty; in LateUpdate the dirty chunks are meshed in parallel jobs.
    ///
    /// Blocky bodies (buildings, the tank, loose pieces) are meshed and put in the same frame: a
    /// piece is let go only once its colliders exist. The smooth ground is remeshed without the
    /// frame waiting: each job reads its own copy of the voxels round its chunk, at most
    /// <see cref="ASYNC_PER_FRAME"/> start per frame, and each is put in at the start of the next
    /// frame (or the first LateUpdate) that finds it done. Its collision mesh is then cooked on a
    /// worker and swapped into the collider when ready, at the latest at the start of the frame
    /// after. A track's rut or a bomb's crater shows a frame later, and the frame no longer stops
    /// for dozens of remeshes and PhysX cooks. The tank's skin (no colliders) remeshes the same way.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class VoxelWorld : MonoBehaviour, IVoxelLateUpdate
    {
        /// <summary>Layer for pieces that broke off. Rubble collides with itself (Debris does not).</summary>
        public const int PieceLayer = 16;

        /// <summary>Most smooth (ground) chunk remeshes started in one frame.</summary>
        public const int ASYNC_PER_FRAME = 12;

        /// <summary>
        /// A/B switch for measuring: false remeshes the ground the old way, every chunk in the
        /// frame it was dug, the frame waiting for it and for its colliders.
        /// </summary>
        public static bool AsyncGround = true;

        /// <summary>A remesh still running after this many frames is waited for.</summary>
        private const int ASYNC_MAX_FRAMES = 4;

        /// <summary>
        /// Vertices of finished ground remeshes put into meshes per frame (at least one chunk):
        /// a crater's chunks are uploaded over a couple of frames, not all in one.
        /// </summary>
        private const int ASYNC_APPLY_VERTICES = 40000;

        private const string CHUNK_MATERIAL = "Voxels/VoxelChunk";
        private const string DEBRIS_MATERIAL = "Voxels/VoxelDebris";

        private static VoxelWorld s_instance;

        // Capacities up front: a list growing mid-fight (a blast dirties a hundred chunks at once) is a spike of
        // garbage in that frame — 11.6 KB was measured in idle. A list that outgrows these still grows as before.
        private const int BODIES_CAPACITY = 1024;
        private const int JOBS_CAPACITY = 256;
        private const int BAKES_CAPACITY = 256;

        private static readonly List<VoxelBody> s_bodies = new(BODIES_CAPACITY);
        private static bool s_quitting;

        private readonly List<VoxelBody.ChunkJob> _jobs = new(JOBS_CAPACITY);
        private readonly List<VoxelBody.ChunkJob> _asyncJobs = new(JOBS_CAPACITY);
        private readonly List<VoxelBody> _snapshot = new(BODIES_CAPACITY);
        private int _bodyCursor;
        private Camera _viewCamera;
        private float _nextCameraLook;

        /// <summary>Collision meshes written this frame, to be cooked.</summary>
        private static readonly List<Mesh> s_bakeMeshes = new(BAKES_CAPACITY);
        private static readonly List<VoxelBody.Chunk> s_bakeChunks = new(BAKES_CAPACITY);

        /// <summary>Cooks in flight.</summary>
        private readonly List<BakeBatch> _bakes = new();

        private sealed class BakeBatch
        {
            public NativeArray<EntityId> Ids;
            public Mesh[] Meshes;
            public VoxelBody.Chunk[] Chunks;
            public JobHandle Handle;
        }

        public Material ChunkMaterial { get; private set; }

        /// <summary>Main-thread time of the last frame's rebuild (schedule, wait, upload), ms.</summary>
        public float LastRebuildMs { get; private set; }

        /// <summary>Chunks remeshed in the last frame that had any to remesh.</summary>
        public int LastRebuildChunks { get; private set; }

        /// <summary>Ground remeshes running on workers right now.</summary>
        public int AsyncInFlight => _asyncJobs.Count;

        public VoxelDebris Debris { get; private set; }

        /// <summary>Cached once in Awake: subscribing them allocates nothing per frame.</summary>
        private System.Action<Vector3, float, int> _onCarved;
        private System.Action<Vector3, float, int> _onAirBlasted;

        public static IReadOnlyList<VoxelBody> Bodies => s_bodies;

        /// <summary>
        /// A chunk got new colliders (a crater, a rut, a wall knocked down), with the chunk's world
        /// bounds. Navigation listens to re-read the ground there. Raised on the main thread: for a
        /// standing body's box colliders after they are set and when its chunk is emptied; for mesh
        /// colliders (smooth and terrain ground) when the cooked mesh lands in <see cref="FinishBakes"/>,
        /// whichever body it is. Loose pieces with box colliders do not raise it. Cleared when Play starts.
        /// </summary>
        public static event System.Action<Bounds> ChunkCollidersChanged;

        internal static void RaiseChunkCollidersChanged(Bounds worldBounds) => ChunkCollidersChanged?.Invoke(worldBounds);

        /// <summary>Listeners of the last Play session are dropped (with domain reload off they would survive).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetChunkCollidersChanged() => ChunkCollidersChanged = null;

        public static VoxelWorld Instance
        {
            get
            {
                if (s_instance == null && !s_quitting && Application.isPlaying)
                    new GameObject("[VoxelWorld]").AddComponent<VoxelWorld>();
                return s_instance;
            }
        }

        /// <summary>The world if there is one; never creates it.</summary>
        internal static VoxelWorld Current => s_instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_instance = null;
            s_bodies.Clear();
            s_structures.Clear();
            s_queuedFrames.Clear();
            s_bakeMeshes.Clear();
            s_bakeChunks.Clear();
            s_quitting = false;
            SetEarlyHook(false);
        }

        /// <summary>Marks our entry in the player loop.</summary>
        private struct VoxelDebrisSync
        {
        }

        /// <summary>
        /// Puts (or takes out) a step at the very start of each frame, before physics, that waits
        /// for the debris step started at the end of the last frame: its raycasts run on workers
        /// while the frame renders, never while colliders move.
        /// </summary>
        private static void SetEarlyHook(bool on)
        {
            UnityEngine.LowLevel.PlayerLoopSystem loop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
            if (loop.subSystemList == null)
                return;

            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                if (loop.subSystemList[i].type != typeof(UnityEngine.PlayerLoop.EarlyUpdate))
                    continue;

                var early = loop.subSystemList[i];
                var systems = new List<UnityEngine.LowLevel.PlayerLoopSystem>(early.subSystemList ?? System.Array.Empty<UnityEngine.LowLevel.PlayerLoopSystem>());
                bool had = systems.RemoveAll(s => s.type == typeof(VoxelDebrisSync)) > 0;
                if (on)
                    systems.Insert(0, new UnityEngine.LowLevel.PlayerLoopSystem
                    {
                        type = typeof(VoxelDebrisSync),
                        updateDelegate = CompleteDebrisStep
                    });
                else if (!had)
                    return;

                early.subSystemList = systems.ToArray();
                loop.subSystemList[i] = early;
                UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(loop);
                return;
            }
        }

        /// <summary>
        /// The very start of a frame, before physics: the debris step and the collision cooks
        /// started at the end of the last frame have had its whole render to run and are waited
        /// for (normally done already); ground remeshes that are done go in and their colliders
        /// start cooking. A crater's collider thus follows its mesh by at most a frame.
        /// </summary>
        private static void CompleteDebrisStep()
        {
            if (s_instance == null)
                return;

            s_instance.Debris?.Complete();
            s_instance.FinishBakes(true);
            if (s_instance.ApplyFinished(Time.frameCount) > 0)
                s_instance.StartBakes(false);
        }

        internal static void Register(VoxelBody body)
        {
            if (!s_bodies.Contains(body))
                s_bodies.Add(body);
        }

        internal static void Unregister(VoxelBody body) => s_bodies.Remove(body);

        /// <summary>A chunk's collision mesh, freshly written, is cooked and then handed to its collider.</summary>
        internal static void QueueBake(Mesh mesh, VoxelBody.Chunk chunk)
        {
            s_bakeMeshes.Add(mesh);
            s_bakeChunks.Add(chunk);
        }

        /// <summary>
        /// Starts cooking the collision meshes written this frame, in parallel on workers; with
        /// <paramref name="wait"/> they are cooked and handed over now (the first build).
        /// </summary>
        private void StartBakes(bool wait)
        {
            if (s_bakeMeshes.Count == 0)
                return;

            var batch = new BakeBatch
            {
                Ids = new NativeArray<EntityId>(s_bakeMeshes.Count, Allocator.Persistent),
                Meshes = s_bakeMeshes.ToArray(),
                Chunks = s_bakeChunks.ToArray()
            };
            for (int i = 0; i < batch.Ids.Length; i++)
                batch.Ids[i] = batch.Meshes[i].GetEntityId();
            batch.Handle = new BakeMeshesJob { MeshIds = batch.Ids }.Schedule(batch.Ids.Length, 1);
            JobHandle.ScheduleBatchedJobs();

            s_bakeMeshes.Clear();
            s_bakeChunks.Clear();
            _bakes.Add(batch);

            if (wait)
                FinishBakes(true);
        }

        /// <summary>
        /// Hands every cooked collision mesh to its collider; with <paramref name="force"/> waits
        /// for the ones still cooking.
        /// </summary>
        internal void FinishBakes(bool force)
        {
            for (int b = 0; b < _bakes.Count; b++)
            {
                BakeBatch batch = _bakes[b];
                if (!force && !batch.Handle.IsCompleted)
                    continue;

                batch.Handle.Complete();
                batch.Ids.Dispose();
                for (int i = 0; i < batch.Chunks.Length; i++)
                {
                    VoxelBody.Chunk chunk = batch.Chunks[i];
                    chunk.Cooking = false;
                    if (chunk.Collider != null && batch.Meshes[i] != null)
                        chunk.Collider.sharedMesh = batch.Meshes[i];
                    if (chunk.Collider != null && batch.Meshes[i] != null)
                        RaiseChunkCollidersChanged(chunk.Renderer.bounds);
                }

                _bakes.RemoveAt(b);
                b--;
            }
        }

        private void Awake()
        {
            _onCarved = OnCarved;
            _onAirBlasted = OnAirBlasted;
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            ChunkMaterial = LoadMaterial(CHUNK_MATERIAL, false);
            Debris = new VoxelDebris(LoadMaterial(DEBRIS_MATERIAL, true), 24000, VoxelDebris.LYING_DEFAULT);
            VoxelBody.Prewarm();
            SetEarlyHook(true);
        }

        private static Material LoadMaterial(string path, bool instanced)
        {
            Material material = Resources.Load<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Game/Voxel");
                material = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"));
            }

            if (instanced && !material.enableInstancing)
            {
                material = new Material(material) { enableInstancing = true };
            }

            return material;
        }

        private void OnEnable()
        {
            VoxelTicks.Add(this);
            VoxelSignals.Carved += OnGroundCarved;
            VoxelSignals.AirBlasted += OnGroundCarved;
            VoxelSignals.Carved += _onCarved;
            VoxelSignals.AirBlasted += _onAirBlasted;
        }

        private void OnDisable()
        {
            VoxelTicks.Remove(this);
            VoxelSignals.Carved -= OnGroundCarved;
            VoxelSignals.AirBlasted -= OnGroundCarved;
            VoxelSignals.Carved -= _onCarved;
            VoxelSignals.AirBlasted -= _onAirBlasted;
        }

        /// <summary>Buildings waiting for a load solve this frame (see VoxelBody.SolveStructure).</summary>
        private static readonly List<VoxelBody> s_structures = new(64);

        /// <summary>How far past a crater's radius (m) a building is looked at again: the ground under it may be gone.</summary>
        private const float STRUCTURE_REACH = 1.5f;

        internal static void QueueStructure(VoxelBody body)
        {
            if (s_structures.Contains(body))
                return;
            s_structures.Add(body);
            s_queuedFrames.Add(Time.frameCount);
        }

        /// <summary>The frame each queued building was queued in (parallel to s_structures).</summary>
        private static readonly List<int> s_queuedFrames = new(64);

        internal static void UnqueueStructure(VoxelBody body)
        {
            int i = s_structures.IndexOf(body);
            if (i < 0)
                return;
            s_structures.RemoveAt(i);
            s_queuedFrames.RemoveAt(i);
        }

        /// <summary>A crater near a building may have taken the ground from under it.</summary>
        private static void OnGroundCarved(Vector3 point, float radius, int voxels)
        {
            // A bullet that chipped nothing (a miss, a hit on the tank) moves no ground.
            if (voxels <= 0)
                return;
            float reach = radius + STRUCTURE_REACH;
            for (int i = 0; i < s_bodies.Count; i++)
            {
                VoxelBody body = s_bodies[i];
                if (body != null && body.HasStrength && body.IsStatic && body.WorldBounds.SqrDistance(point) <= reach * reach)
                    body.MarkStructureDirty(point, reach);
            }
        }

        /// <summary>Last frame's load solves: main-thread ms and how many bodies.</summary>
        public float LastStructureMs { get; private set; }
        public int LastStructureBodies { get; private set; }

        /// <summary>
        /// Works out the load of every marked building once, before the rebuild: pieces it lets
        /// fall get their colliders in this frame's rebuild. A body that is still settling marks
        /// itself again and is solved next frame.
        /// </summary>
        private void SolveStructures()
        {
            if (s_structures.Count == 0)
                return;

            // One building a frame, and never in the frame it was hit: the blast (carve, debris,
            // rebuild) has its frame, the collapse the next, a second building the one after.
            int frame = Time.frameCount;
            int pick = -1;
            for (int i = 0; i < s_structures.Count; i++)
            {
                if (s_structures[i] == null)
                {
                    s_structures.RemoveAt(i);
                    s_queuedFrames.RemoveAt(i);
                    i--;
                    continue;
                }

                if (s_queuedFrames[i] < frame)
                {
                    pick = i;
                    break;
                }
            }

            if (pick < 0)
                return;

            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            VoxelBody body = s_structures[pick];
            s_structures.RemoveAt(pick);
            s_queuedFrames.RemoveAt(pick);
            body.SolveStructure();

            LastStructureBodies = 1;
            LastStructureMs = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000f /
                              System.Diagnostics.Stopwatch.Frequency;
        }

        /// <summary>A shell or a blast dug the ground: what lay there flies, what lay round the hole falls in.</summary>
        private void OnCarved(Vector3 point, float radius, int voxels)
        {
            Debris?.Blow(point, radius * 1.5f, 4f + radius * 3f);
            Debris?.Loosen(point, radius + 0.3f);
        }

        /// <summary>An air bomb: everything lying within twice the crater flies.</summary>
        private void OnAirBlasted(Vector3 point, float radius, int voxels)
        {
            Debris?.Blow(point, radius * 2f, 14f);
            Debris?.Loosen(point, radius + 0.3f);
        }

        private void LateUpdate() // perf-ok: ticks itself without the game loop; draws (dt 0) while the loop is paused
        {
            if (!VoxelTicks.Driven)
                VoxelLateUpdate(Time.deltaTime);
            else if (VoxelTicks.Paused)
                VoxelLateUpdate(0f);
        }

        public void VoxelLateUpdate(float deltaTime)
        {
            SolveStructures();
            RebuildDirty();
            if (Debris != null)
            {
                // Eviction counts distance from the viewer and lying cubes are culled by its frustum;
                // look for the camera again once a second if there is none.
                if (_viewCamera == null && Time.unscaledTime >= _nextCameraLook)
                {
                    _viewCamera = Camera.main;
                    _nextCameraLook = Time.unscaledTime + 1f;
                }
                if (_viewCamera != null)
                    Debris.ViewPoint = _viewCamera.transform.position;
                Debris.ViewCamera = _viewCamera;
            }
            Debris?.Tick(deltaTime);
        }

        private void RebuildDirty()
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            int frame = Time.frameCount;
            int remeshed = 0;

            // Cooked collision meshes from earlier frames go into their colliders.
            FinishBakes(false);
            remeshed += ApplyFinished(frame);

            _snapshot.Clear();
            _snapshot.AddRange(s_bodies);

            bool firstBuild = false;
            int budget = ASYNC_PER_FRAME;
            // Round the bodies from a different one each frame: one dirtied every frame (the
            // tank's gear being washed) cannot take the whole async budget from the ground.
            int bodies = _snapshot.Count;
            int first = bodies > 0 ? _bodyCursor++ % bodies : 0;
            for (int k = 0; k < bodies; k++)
            {
                VoxelBody body = _snapshot[(first + k) % bodies];
                if (body == null || !body.IsCreated || !body.HasDirtyChunks)
                    continue;

                if (body.RebuildsAsync && AsyncGround)
                {
                    if (budget > 0)
                        body.ScheduleAsync(_asyncJobs, ref budget, frame);
                    continue;
                }

                firstBuild |= body.IsTerrain || !AsyncGround;
                body.ScheduleRebuild(_jobs);
            }

            JobHandle.ScheduleBatchedJobs();

            if (_jobs.Count > 0)
            {
                for (int i = 0; i < _jobs.Count; i++)
                {
                    VoxelBody.ChunkJob job = _jobs[i];
                    job.Handle.Complete();
                    if (job.Body != null)
                        job.Body.ApplyChunk(job);
                    job.Dispose();
                }

                remeshed += _jobs.Count;
                _jobs.Clear();
            }

            // The first build of the ground waits for its colliders: the tank stands on them.
            StartBakes(firstBuild);

            if (remeshed > 0)
            {
                LastRebuildChunks = remeshed;
                LastRebuildMs = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000f /
                                System.Diagnostics.Stopwatch.Frequency;
            }

            foreach (VoxelBody body in _snapshot)
                if (body != null)
                    body.FinishRebuild();
        }

        /// <summary>
        /// Puts in the ground remeshes that are done, oldest first, within one upload budget; one
        /// running too long is waited for. Returns how many went in.
        /// </summary>
        private int ApplyFinished(int frame)
        {
            int applied = 0;
            int uploads = ASYNC_APPLY_VERTICES;
            for (int i = 0; i < _asyncJobs.Count; i++)
            {
                VoxelBody.ChunkJob job = _asyncJobs[i];
                // With the A/B switch off everything still running lands now, before the in-step pass.
                bool overdue = !AsyncGround || frame - job.Frame >= ASYNC_MAX_FRAMES;
                if (!job.Handle.IsCompleted && !overdue)
                    continue;
                if (uploads <= 0 && !overdue)
                    break;

                job.Handle.Complete();
                uploads -= job.Vertices.Length;
                if (job.Body != null)
                    job.Body.ApplyChunk(job);
                job.Dispose();
                applied++;
                _asyncJobs.RemoveAt(i);
                i--;
            }

            return applied;
        }

        private void OnApplicationQuit() => s_quitting = true;

        private void OnDestroy()
        {
            if (s_instance != this)
                return;

            SetEarlyHook(false);
            foreach (VoxelBody.ChunkJob job in _asyncJobs)
            {
                job.Handle.Complete();
                // A body that outlives the world remeshes that chunk again under the next one.
                if (job.Body != null)
                    job.Body.CancelAsync(job.Chunk, job.Shape);
                job.Dispose();
            }

            _asyncJobs.Clear();
            FinishBakes(true);
            s_bakeMeshes.Clear();
            s_bakeChunks.Clear();

            Debris?.Dispose();
            Debris = null;
            s_instance = null;
        }
    }
}
