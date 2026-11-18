using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// Cuts a voxelized body (SkinnedVoxelizer) into bodies, one per cluster of parts: a body that
    /// burst into its head, a chest with both arms and a pair of legs comes out as four VoxelBodies,
    /// each standing exactly where its voxels were. Where two clusters met, the faces torn apart are
    /// listed — that is where the blood comes from.
    /// </summary>
    public static class VoxelCarcass
    {
        /// <summary>Torn faces kept per piece: they only tell where blood comes from.</summary>
        public const int MAX_TORN = 256;

        public struct TornFace
        {
            public Vector3 Point;

            /// <summary>Out of the piece, towards the piece it was torn from.</summary>
            public Vector3 Normal;
        }

        public sealed class Piece
        {
            public VoxelBody Body;
            public int Cluster;

            /// <summary>How many parts of the body the piece holds.</summary>
            public int Parts;

            /// <summary>Where it was torn: the first <see cref="TornCount"/> entries (the whole array from <see cref="Build(in SkinnedVoxelizer.Result, IReadOnlyList{int}, VoxelBodySettings, string)"/>).</summary>
            public TornFace[] Torn;

            public int TornCount;
        }

        private struct TornVoxel
        {
            public int3 Position;
            public int3 Direction;
        }

        /// <summary>Pieces handed back by the game once their bodies are on their way (<see cref="Release"/>), with their torn-face buffers.</summary>
        private static readonly Stack<Piece> PiecePool = new();

        /// <summary>
        /// Bodies made ahead (<see cref="AddSpareBodies"/>): a switched-off GameObject with a VoxelBody never
        /// initialised, and the impact fracture and settle a loose piece takes (VoxelBody.MakeDynamic finds them
        /// instead of adding them). A piece takes one instead of making its GameObject and components. Used once:
        /// a body cannot be emptied and filled again (see the remarks of <see cref="AddSpareBodies"/>).
        /// </summary>
        private static readonly Stack<VoxelBody> SpareBodies = new();
        private static bool s_spareSettle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            PiecePool.Clear();
            SpareBodies.Clear();
        }

        /// <summary>Bodies made ahead and not taken yet (some may have gone with their scene).</summary>
        public static int SpareBodyCount => SpareBodies.Count;

        /// <summary>
        /// Makes <paramref name="count"/> bodies ahead, in the active scene, switched off. Meant for level load,
        /// a few a frame: a death then makes no GameObject, VoxelBody, VoxelImpactFracture or VoxelSettle of its own.
        /// </summary>
        /// <param name="settle">Whether they carry a VoxelSettle: they are only taken for settings with the same Settle.</param>
        /// <remarks>
        /// The bodies are not recycled: the flesh of a death lies all game, and a VoxelBody cannot yet be filled
        /// again (its chunks and colliders would be left behind). Once the spares run out, pieces are made as before.
        /// </remarks>
        public static void AddSpareBodies(int count, bool settle)
        {
            if (SpareBodies.Count > 0 && settle != s_spareSettle)
                return;
            s_spareSettle = settle;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject(SPARE_NAME);
                go.SetActive(false);
                VoxelBody body = go.AddComponent<VoxelBody>();
                go.AddComponent<VoxelImpactFracture>();
                if (settle)
                    go.AddComponent<VoxelSettle>();
                SpareBodies.Push(body);
            }
        }

        private const string SPARE_NAME = "Flesh (spare)";

        /// <summary>A spare body for these settings, or a new one: on a switched-off GameObject if <paramref name="inactive"/>.</summary>
        private static VoxelBody TakeBody(string name, in VoxelBodySettings settings, bool inactive)
        {
            while (SpareBodies.Count > 0 && settings.Settle == s_spareSettle)
            {
                VoxelBody spare = SpareBodies.Pop();
                if (spare == null)
                    continue; // gone with its scene

                GameObject spareObject = spare.gameObject;
                spareObject.name = name;
                spareObject.layer = settings.Layer;
                if (!inactive)
                    spareObject.SetActive(true);
                return spare;
            }

            var go = new GameObject(name) { layer = settings.Layer };
            if (inactive)
                go.SetActive(false);
            return go.AddComponent<VoxelBody>();
        }

        /// <summary>Makes <paramref name="count"/> pooled pieces ahead (level load), so a death takes them without allocating.</summary>
        public static void Reserve(int count)
        {
            while (PiecePool.Count < count)
                PiecePool.Push(new Piece { Torn = new TornFace[MAX_TORN] });
        }

        /// <summary>Hands pieces from <see cref="Build(in SkinnedVoxelizer.Result, IReadOnlyList{int}, VoxelBodySettings, IReadOnlyList{string}, bool, List{Piece})"/> back for the next death; the list is cleared.</summary>
        public static void Release(List<Piece> pieces)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                Piece piece = pieces[i];
                piece.Body = null;
                piece.TornCount = 0;
                PiecePool.Push(piece);
            }

            pieces.Clear();
        }

        /// <param name="clusterOfPart">Cluster of each part, by part index; any ids below the part count.</param>
        /// <param name="settings">For every body; its Origin and VoxelSize are set here.</param>
        public static List<Piece> Build(in SkinnedVoxelizer.Result grid, IReadOnlyList<int> clusterOfPart,
            VoxelBodySettings settings, string name)
        {
            // Cluster ids are below the part count.
            var names = new string[clusterOfPart?.Count ?? 0];
            for (int c = 0; c < names.Length; c++)
                names[c] = $"{name}_{c}";

            var pooled = new List<Piece>();
            Build(grid, clusterOfPart, settings, names, false, pooled);

            // Pieces of their own, the torn faces cut to length: the pooled ones go back.
            var pieces = new List<Piece>(pooled.Count);
            foreach (Piece piece in pooled)
            {
                var torn = new TornFace[piece.TornCount];
                Array.Copy(piece.Torn, torn, torn.Length);
                pieces.Add(new Piece { Body = piece.Body, Cluster = piece.Cluster, Parts = piece.Parts, Torn = torn, TornCount = torn.Length });
            }

            Release(pooled);
            return pieces;
        }

        /// <summary>
        /// The pieces of a body, one per cluster, added to <paramref name="into"/> (pooled: hand them back with
        /// <see cref="Release"/>). Allocates nothing of its own beyond what each new VoxelBody does.
        /// </summary>
        /// <param name="names">Name of the piece of each cluster, by cluster id (made once by the caller).</param>
        /// <param name="inactive">The pieces are made switched off: their meshes and colliders are built at the end of
        /// the frame all the same, and they show when the caller switches them on (and launches them).</param>
        public static void Build(in SkinnedVoxelizer.Result grid, IReadOnlyList<int> clusterOfPart,
            VoxelBodySettings settings, IReadOnlyList<string> names, bool inactive, List<Piece> into)
        {
            if (!grid.Voxels.IsCreated || clusterOfPart == null || clusterOfPart.Count == 0)
                return;

            int clusters = 0;
            for (int i = 0; i < clusterOfPart.Count; i++)
                clusters = math.max(clusters, clusterOfPart[i] + 1);

            var map = new NativeArray<int>(256, Allocator.TempJob);
            for (int i = 0; i < 256; i++)
                map[i] = i < clusterOfPart.Count ? clusterOfPart[i] : -1;

            var lo = new NativeArray<int3>(clusters, Allocator.TempJob);
            var hi = new NativeArray<int3>(clusters, Allocator.TempJob);
            new ClusterBoundsJob { Parts = grid.Parts, Map = map, Size = grid.Size, Min = lo, Max = hi }.Run();
            var torn = new NativeList<TornVoxel>(MAX_TORN * 4, Allocator.TempJob);

            for (int c = 0; c < clusters; c++)
            {
                if (hi[c].x < lo[c].x)
                    continue;

                int3 size = hi[c] - lo[c] + 1;
                var target = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent);
                torn.Clear();
                new ExtractClusterJob
                {
                    Voxels = grid.Voxels, Parts = grid.Parts, Map = map, Size = grid.Size,
                    Cluster = c, Min = lo[c], TargetSize = size, Target = target, Torn = torn
                }.Run();

                VoxelBody body = TakeBody(c < names.Count ? names[c] : NAME_FALLBACK, settings, inactive);
                Transform space = body.transform;
                space.SetPositionAndRotation(grid.Position, grid.Rotation);

                VoxelBodySettings own = settings;
                own.VoxelSize = grid.VoxelSize;
                own.Origin = grid.Origin + (float3)lo[c] * grid.VoxelSize;

                body.Init(size, target, new NativeArray<Color32>(grid.Palette, Allocator.Persistent), own);

                Piece piece = PiecePool.Count > 0 ? PiecePool.Pop() : new Piece { Torn = new TornFace[MAX_TORN] };
                piece.Body = body;
                piece.Cluster = c;
                piece.Parts = CountParts(clusterOfPart, c);
                piece.TornCount = Faces(space, grid, torn, piece.Torn);
                into.Add(piece);
            }

            torn.Dispose();
            map.Dispose();
            lo.Dispose();
            hi.Dispose();
        }

        private const string NAME_FALLBACK = "Flesh";

        private static int CountParts(IReadOnlyList<int> clusterOfPart, int cluster)
        {
            int count = 0;
            for (int i = 0; i < clusterOfPart.Count; i++)
                if (clusterOfPart[i] == cluster)
                    count++;
            return count;
        }

        /// <summary>At most MAX_TORN faces, picked evenly along the list so the whole cut bleeds.</summary>
        private static int Faces(Transform space, in SkinnedVoxelizer.Result grid, NativeList<TornVoxel> torn, TornFace[] faces)
        {
            int count = math.min(torn.Length, math.min(MAX_TORN, faces.Length));
            int stride = torn.Length > MAX_TORN ? torn.Length / MAX_TORN : 1;
            for (int i = 0; i < count; i++)
            {
                TornVoxel voxel = torn[i * stride];
                float3 local = grid.Origin + ((float3)voxel.Position + 0.5f + (float3)voxel.Direction * 0.5f) * grid.VoxelSize;
                faces[i] = new TornFace
                {
                    Point = space.TransformPoint(local),
                    Normal = space.TransformDirection((float3)voxel.Direction)
                };
            }

            return count;
        }

        [BurstCompile]
        private struct ClusterBoundsJob : IJob
        {
            [ReadOnly] public NativeArray<byte> Parts;
            [ReadOnly] public NativeArray<int> Map;
            public int3 Size;
            public NativeArray<int3> Min;
            public NativeArray<int3> Max;

            public void Execute()
            {
                for (int c = 0; c < Min.Length; c++)
                {
                    Min[c] = new int3(int.MaxValue);
                    Max[c] = new int3(-1);
                }

                for (int i = 0; i < Parts.Length; i++)
                {
                    byte part = Parts[i];
                    if (part == SkinnedVoxelizer.NO_PART)
                        continue;
                    int c = Map[part];
                    if (c < 0)
                        continue;
                    int3 cell = VoxelMath.Coord(i, Size);
                    Min[c] = math.min(Min[c], cell);
                    Max[c] = math.max(Max[c], cell);
                }
            }
        }

        [BurstCompile]
        private struct ExtractClusterJob : IJob
        {
            [ReadOnly] public NativeArray<byte> Voxels;
            [ReadOnly] public NativeArray<byte> Parts;
            [ReadOnly] public NativeArray<int> Map;
            public int3 Size;
            public int Cluster;
            public int3 Min;
            public int3 TargetSize;
            public NativeArray<byte> Target;
            public NativeList<TornVoxel> Torn;

            public void Execute()
            {
                for (int z = 0; z < TargetSize.z; z++)
                for (int y = 0; y < TargetSize.y; y++)
                for (int x = 0; x < TargetSize.x; x++)
                {
                    int3 cell = Min + new int3(x, y, z);
                    int index = VoxelMath.Index(cell, Size);
                    byte part = Parts[index];
                    if (part == SkinnedVoxelizer.NO_PART || Map[part] != Cluster)
                        continue;

                    Target[VoxelMath.Index(new int3(x, y, z), TargetSize)] = Voxels[index];
                    Check(cell, new int3(1, 0, 0));
                    Check(cell, new int3(-1, 0, 0));
                    Check(cell, new int3(0, 1, 0));
                    Check(cell, new int3(0, -1, 0));
                    Check(cell, new int3(0, 0, 1));
                    Check(cell, new int3(0, 0, -1));
                }
            }

            /// <summary>A neighbour of another cluster: this face was torn.</summary>
            private void Check(int3 cell, int3 direction)
            {
                int3 next = cell + direction;
                if (!VoxelMath.Inside(next, Size))
                    return;
                byte part = Parts[VoxelMath.Index(next, Size)];
                if (part == SkinnedVoxelizer.NO_PART)
                    return;
                int other = Map[part];
                if (other >= 0 && other != Cluster)
                    Torn.Add(new TornVoxel { Position = cell, Direction = direction });
            }
        }
    }
}
