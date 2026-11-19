using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Voxels
{
    /// <summary>
    /// The bodies creatures' corpses are made of, used over and over (VoxelBody.Recycle): a death takes free
    /// bodies instead of making GameObjects, chunks, meshes and colliders, so it leaves no garbage. The last
    /// <see cref="Capacity"/> corpses lie; a death past that puts the oldest away and takes its bodies.
    /// </summary>
    /// <remarks>
    /// <para>A corpse is every body of one death: its pieces (VoxelCarcass.Build) and what split off them while
    /// <see cref="Serve"/> handed out the pieces (VoxelBody.PieceFactory). They go back together.</para>
    /// <para>The bodies are made at level load (<see cref="Prepare"/>, then <see cref="Prime"/> a few a frame):
    /// each is filled once with a grid of the planned chunk layout, so the voxel world builds its chunk objects,
    /// meshes and box colliders, and given its Rigidbody; then put away. A body's chunks are reused only for a
    /// grid with as many chunks (VoxelBody.Init), so a death takes a free body with the same chunk count.</para>
    /// <para>The bodies are objects of the level's scene: they go with it. The next level makes its own.</para>
    /// </remarks>
    public static class VoxelCorpses
    {
        /// <summary>A free body's name: it is no piece of flesh until a death takes it.</summary>
        public const string POOLED_NAME = "Corpse (pooled)";

        /// <summary>What VoxelBody.Detach names a piece it makes itself: the same for one it takes from here.</summary>
        private const string PIECE_NAME = "VoxelPiece";

        /// <summary>Room for a corpse's bodies: seven pieces and what splits off them, made once.</summary>
        private const int BODIES_CAPACITY = 32;

        /// <summary>Where a body is filled at level load: switched off, out of the way of anything.</summary>
        private static readonly Vector3 PrimePlace = new(0f, -1000f, 0f);

        private static readonly ProfilerMarker PrimeMarker = new("Corpses.Prime");
        private static readonly ProfilerMarker RecycleMarker = new("Corpses.Recycle");

        /// <summary>The bodies of one death.</summary>
        public sealed class Corpse
        {
            internal readonly List<VoxelBody> Bodies = new(BODIES_CAPACITY);

            /// <summary>Its death is still being built or shown: it is not put away, however old.</summary>
            internal bool Busy;

            public int BodyCount => Bodies.Count;

            public VoxelBody GetBody(int index) => Bodies[index];

            /// <summary>Whether the body is one of this corpse's.</summary>
            public bool Holds(VoxelBody body) => Bodies.Contains(body);
        }

        /// <summary>The corpses lying, oldest first.</summary>
        private static readonly List<Corpse> Lying = new();

        private static readonly Stack<Corpse> EmptyCorpses = new();
        private static readonly List<VoxelBody> Free = new();

        /// <summary>Filled this frame with the priming grid: put away next frame, once the world has built them.</summary>
        private static readonly List<VoxelBody> Filling = new();

        /// <summary>Used without being built (a warm-up's): filled again before a death takes them.</summary>
        private static readonly List<VoxelBody> Refill = new();

        private static readonly Func<VoxelBody> Factory = ServePiece;

        private static int s_capacity = 20;
        private static int s_toMake;
        private static int s_made;
        private static int s_scene = -1;
        private static VoxelBodySettings s_settings = Defaults();
        private static int3[] s_layouts = { new(1, 1, 1) };
        private static int s_boxes = 64;
        private static Corpse s_serving;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Lying.Clear();
            EmptyCorpses.Clear();
            Free.Clear();
            Filling.Clear();
            Refill.Clear();
            s_capacity = 20;
            s_toMake = 0;
            s_made = 0;
            s_scene = -1;
            s_settings = Defaults();
            s_layouts = new[] { new int3(1, 1, 1) };
            s_boxes = 64;
            s_serving = null;
        }

        private static VoxelBodySettings Defaults()
        {
            VoxelBodySettings settings = VoxelBodySettings.Default(0.05f);
            settings.Layer = VoxelWorld.PieceLayer;
            settings.Colliders = true;
            settings.Settle = true;
            return settings;
        }

        /// <summary>Corpses that lie at most: a death past it puts the oldest away.</summary>
        public static int Capacity
        {
            get => s_capacity;
            set
            {
                s_capacity = Math.Max(1, value);
                if (Lying.Capacity < s_capacity + 1)
                    Lying.Capacity = s_capacity + 1;
                while (Lying.Count > s_capacity && RecycleOldest())
                {
                }
            }
        }

        /// <summary>Corpses lying now.</summary>
        public static int CorpseCount => Lying.Count;

        /// <summary>Bodies free for the next death (some may have gone with their scene).</summary>
        public static int FreeCount => Free.Count;

        /// <summary>Bodies still to be made or filled at level load.</summary>
        public static int ToPrime => s_toMake + Refill.Count + Filling.Count;

        /// <summary>The corpse whose pieces are being handed out now (<see cref="Serve"/>), if any.</summary>
        public static Corpse Serving => s_serving;

        /// <summary>Whether this scene's bodies are made (or on their way).</summary>
        public static bool IsPreparedFor(Scene scene) => scene.IsValid() && s_scene == scene.handle;

        /// <summary>
        /// Makes ready for a level: <paramref name="bodies"/> bodies are made over the next frames (<see cref="Prime"/>),
        /// each filled once with a grid of the next of <paramref name="layouts"/> (chunks along x, y, z) carrying
        /// <paramref name="boxesPerChunk"/> box colliders a chunk. Another scene's bodies are let go first.
        /// </summary>
        /// <param name="settings">The settings the pieces are made with (layer, chunk size, colliders, settle).</param>
        public static void Prepare(Scene scene, int corpses, int bodies, in VoxelBodySettings settings, int3[] layouts,
            int boxesPerChunk)
        {
            if (s_scene != scene.handle)
                Clear();
            s_scene = scene.handle;
            s_settings = settings;
            if (layouts != null && layouts.Length > 0)
                s_layouts = layouts;
            s_boxes = Math.Max(1, boxesPerChunk);
            Capacity = corpses;
            s_toMake = Math.Max(0, bodies - Free.Count - Filling.Count - Refill.Count);
            if (Free.Capacity < bodies)
                Free.Capacity = bodies;
            if (Filling.Capacity < bodies)
                Filling.Capacity = bodies;
            while (EmptyCorpses.Count + Lying.Count < s_capacity + 1)
                EmptyCorpses.Push(new Corpse());
        }

        /// <summary>
        /// Every body let go (destroyed if it still exists) and the pool forgotten: a level that goes, or tests.
        /// </summary>
        public static void Clear()
        {
            for (int i = 0; i < Lying.Count; i++)
            {
                DestroyAll(Lying[i].Bodies);
                Lying[i].Bodies.Clear();
                Lying[i].Busy = false;
                EmptyCorpses.Push(Lying[i]);
            }

            Lying.Clear();
            DestroyAll(Free);
            DestroyAll(Filling);
            DestroyAll(Refill);
            Free.Clear();
            Filling.Clear();
            Refill.Clear();
            s_toMake = 0;
            s_made = 0;
            s_scene = -1;
        }

        private static void DestroyAll(List<VoxelBody> bodies)
        {
            for (int i = 0; i < bodies.Count; i++)
                if (bodies[i] != null)
                    Object.Destroy(bodies[i].gameObject);
        }

        /// <summary>
        /// One step of the level-load filling: last frame's bodies (built by the world since) are put away, and up
        /// to <paramref name="perFrame"/> more are made and filled.
        /// </summary>
        public static void Prime(int perFrame)
        {
            if (Filling.Count == 0 && Refill.Count == 0 && s_toMake == 0)
                return;

            using (PrimeMarker.Auto())
            {
                bool filled = Filling.Count > 0;
                for (int i = Filling.Count - 1; i >= 0; i--)
                {
                    VoxelBody body = Filling[i];
                    if (body != null && body.HasDirtyChunks)
                        continue; // not built yet (a frame the world skipped)
                    Filling.RemoveAt(i);
                    Put(body, Free);
                }

                if (filled && Filling.Count == 0 && Refill.Count == 0 && s_toMake == 0)
                {
                    WakeAll();
                    return;
                }

                int left = perFrame;
                while (left > 0 && Refill.Count > 0)
                {
                    VoxelBody body = Refill[Refill.Count - 1];
                    Refill.RemoveAt(Refill.Count - 1);
                    if (body == null)
                        continue;
                    // Its own grid handed back cleared, as a death of the same size takes it: the clearing job is
                    // compiled now, not at the first such death (a thousand allocations in the editor).
                    int3 size = body.Size;
                    body.TakeVoxels(size.x * size.y * size.z);
                    Fill(body);
                    left--;
                }

                for (; left > 0 && s_toMake > 0; left--)
                {
                    s_toMake--;
                    Fill(NewBody(s_settings.Settle));
                }
            }
        }

        /// <summary>
        /// The last step of the filling: every free body switched on and off again at once. Their components wake
        /// (Awake) now, and the voxel tick lists (VoxelTicks) grow to hold them all, not a little at each death.
        /// </summary>
        private static void WakeAll()
        {
            for (int i = 0; i < Free.Count; i++)
                if (Free[i] != null)
                    Free[i].gameObject.SetActive(true);
            for (int i = 0; i < Free.Count; i++)
                if (Free[i] != null)
                    Free[i].gameObject.SetActive(false);
        }

        /// <summary>A body filled with the next planned layout: the world builds its chunks and boxes this frame.</summary>
        private static void Fill(VoxelBody body)
        {
            int edge = ChunkEdge(s_settings.ChunkSize);
            int3 chunks = math.max(s_layouts[s_made++ % s_layouts.Length], 1);
            // Each chunk holds a cube of dots 4 voxels apart, one collision cell each: room for this many boxes.
            int side = 4 * (int)math.ceil(math.pow(s_boxes, 1f / 3f) - 1e-4f);
            int3 size = (chunks - 1) * edge + math.min(side, edge);

            NativeArray<byte> voxels = body.TakeVoxels(size.x * size.y * size.z);
            NativeArray<Color32> palette = body.TakePalette(256);
            palette[1] = new Color32(120, 30, 30, 255);
            for (int c = 0; c < chunks.x * chunks.y * chunks.z; c++)
            {
                int3 from = VoxelMath.Coord(c, chunks) * edge;
                int dots = 0;
                for (int z = 0; z < edge && dots < s_boxes; z += 4)
                for (int y = 0; y < edge && dots < s_boxes; y += 4)
                for (int x = 0; x < edge && dots < s_boxes; x += 4)
                {
                    int3 at = from + new int3(x, y, z);
                    if (math.any(at + new int3(1, 0, 0) >= size))
                        continue;
                    // Two voxels: a whole collision cell of 2³ (BoxDecomposeJob takes a third of it).
                    voxels[VoxelMath.Index(at, size)] = 1;
                    voxels[VoxelMath.Index(at + new int3(1, 0, 0), size)] = 1;
                    dots++;
                }
            }

            body.transform.SetPositionAndRotation(PrimePlace, Quaternion.identity);
            VoxelBodySettings settings = s_settings;
            settings.Origin = float3.zero;
            body.Init(size, voxels, palette, settings);
            // Its Rigidbody and what a loose piece carries now, not at a death.
            body.Launch(Vector3.zero, Vector3.zero);
            Filling.Add(body);
        }

        /// <summary>A new corpse: the oldest is put away first if as many lie as may. Hand it back with <see cref="Seal"/>.</summary>
        public static Corpse Begin()
        {
            if (Lying.Count >= s_capacity)
                RecycleOldest();
            Corpse corpse = EmptyCorpses.Count > 0 ? EmptyCorpses.Pop() : new Corpse();
            corpse.Busy = true;
            Lying.Add(corpse);
            return corpse;
        }

        /// <summary>Its death is shown: the corpse lies, to be put away once it is the oldest.</summary>
        public static void Seal(Corpse corpse)
        {
            if (corpse != null)
                corpse.Busy = false;
        }

        /// <summary>
        /// The pieces of the corpses lying (not those still being built or shown) whose middle is inside the box,
        /// widened by a part of each piece's own size: each is taken out of its corpse and put away (free for the
        /// next death). Their bounds go into <paramref name="squashed"/> (for the burst), which is cleared first.
        /// About 150 bounds checks for 20 corpses; no physics query, nothing allocated.
        /// </summary>
        public static int Squash(Vector3 center, Quaternion rotation, Vector3 halfExtents, List<Bounds> squashed)
        {
            squashed.Clear();
            Quaternion inverse = Quaternion.Inverse(rotation);
            for (int c = 0; c < Lying.Count; c++)
            {
                Corpse corpse = Lying[c];
                if (corpse.Busy)
                    continue;

                List<VoxelBody> bodies = corpse.Bodies;
                for (int b = bodies.Count - 1; b >= 0; b--)
                {
                    VoxelBody body = bodies[b];
                    if (body == null || !body.gameObject.activeInHierarchy)
                        continue;
                    Bounds bounds = body.WorldBounds;
                    Vector3 local = inverse * (bounds.center - center);
                    Vector3 reach = halfExtents + bounds.extents * 0.5f;
                    if (Mathf.Abs(local.x) > reach.x || Mathf.Abs(local.y) > reach.y || Mathf.Abs(local.z) > reach.z)
                        continue;

                    squashed.Add(bounds);
                    RemoveAt(bodies, b);
                    Put(body, Free);
                }
            }

            return squashed.Count;
        }

        /// <summary>
        /// A corpse put away now, whatever its age (its death went no further, a warm-up): its bodies are free, or
        /// with <paramref name="refill"/> filled again first (they were never built).
        /// </summary>
        public static void Abandon(Corpse corpse, bool refill = false)
        {
            if (corpse == null || !Lying.Remove(corpse))
                return;
            Put(corpse, refill ? Refill : Free);
        }

        /// <summary>
        /// A body for a piece of this corpse: a free one with <paramref name="chunks"/> chunks if there is one (its
        /// chunks, meshes and boxes are reused), else any free one, else the oldest corpse's, else a new one. It is
        /// switched off; the caller places, names and fills it (TakeVoxels, TakePalette, Init).
        /// </summary>
        public static VoxelBody Take(Corpse corpse, int chunks, bool settle)
        {
            VoxelBody body = TakeFree(chunks);
            if (body is null && RecycleOldest())
                body = TakeFree(chunks);
            body ??= NewBody(settle);
            corpse?.Bodies.Add(body);
            return body;
        }

        /// <summary>
        /// Pieces breaking off (VoxelBody.PieceFactory) are this corpse's bodies until the scope is disposed: set
        /// round the corpse's own launch and split, so houses and tanks breaking elsewhere make their own pieces.
        /// </summary>
        public static Scope Serve(Corpse corpse)
        {
            var scope = new Scope(VoxelBody.PieceFactory, s_serving);
            s_serving = corpse;
            VoxelBody.PieceFactory = corpse != null ? Factory : scope.Factory;
            return scope;
        }

        /// <summary>What <see cref="Serve"/> replaced, put back on Dispose.</summary>
        public readonly struct Scope : IDisposable
        {
            internal readonly Func<VoxelBody> Factory;
            private readonly Corpse _serving;

            internal Scope(Func<VoxelBody> factory, Corpse serving)
            {
                Factory = factory;
                _serving = serving;
            }

            public void Dispose()
            {
                VoxelBody.PieceFactory = Factory;
                s_serving = _serving;
            }
        }

        /// <summary>A piece split off a corpse's body: one of its own, of one chunk (most scraps are small).</summary>
        private static VoxelBody ServePiece()
        {
            if (s_serving == null)
                return null;
            VoxelBody body = Take(s_serving, 1, s_settings.Settle);
            body.gameObject.name = PIECE_NAME;
            return body;
        }

        /// <summary>Chunks a grid of this size is cut into with this chunk edge (VoxelBody.Init).</summary>
        public static int ChunksOf(int3 size, int chunkSize)
        {
            int edge = ChunkEdge(chunkSize);
            int3 count = (size + edge - 1) / edge;
            return count.x * count.y * count.z;
        }

        private static int ChunkEdge(int chunkSize) => chunkSize > 0 ? chunkSize : VoxelBody.CHUNK;

        private static VoxelBody TakeFree(int chunks)
        {
            int edge = s_settings.ChunkSize;
            for (int i = Free.Count - 1; i >= 0; i--)
            {
                VoxelBody body = Free[i];
                if (body == null)
                {
                    RemoveAt(Free, i); // gone with its scene, or carved away
                    continue;
                }

                if (ChunksOf(body.Size, edge) == chunks)
                {
                    RemoveAt(Free, i);
                    return body;
                }
            }

            if (Free.Count == 0)
                return null;
            VoxelBody any = Free[Free.Count - 1];
            Free.RemoveAt(Free.Count - 1);
            return any;
        }

        private static void RemoveAt(List<VoxelBody> list, int index)
        {
            int last = list.Count - 1;
            list[index] = list[last];
            list.RemoveAt(last);
        }

        /// <summary>The oldest corpse whose death is done is put away, its bodies free. False if there is none.</summary>
        private static bool RecycleOldest()
        {
            for (int i = 0; i < Lying.Count; i++)
            {
                Corpse corpse = Lying[i];
                if (corpse.Busy)
                    continue;
                Lying.RemoveAt(i);
                Put(corpse, Free);
                return true;
            }

            return false;
        }

        private static void Put(Corpse corpse, List<VoxelBody> into)
        {
            using (RecycleMarker.Auto())
            {
                for (int i = 0; i < corpse.Bodies.Count; i++)
                    Put(corpse.Bodies[i], into);
            }

            corpse.Bodies.Clear();
            corpse.Busy = false;
            EmptyCorpses.Push(corpse);
        }

        private static void Put(VoxelBody body, List<VoxelBody> into)
        {
            if (body == null)
                return; // carved to nothing, or gone with its scene
            VoxelDrip.Stop(body.transform);
            body.Recycle();
            GameObject go = body.gameObject;
            if (go.activeSelf)
                go.SetActive(false);
            go.name = POOLED_NAME;
            into.Add(body);
        }

        /// <summary>A switched-off body in the active scene with what a loose piece carries (VoxelBody.MakeDynamic).</summary>
        private static VoxelBody NewBody(bool settle)
        {
            var go = new GameObject(POOLED_NAME) { layer = s_settings.Layer };
            go.SetActive(false);
            VoxelBody body = go.AddComponent<VoxelBody>();
            go.AddComponent<VoxelImpactFracture>();
            if (settle)
                go.AddComponent<VoxelSettle>();
            return body;
        }
    }
}
