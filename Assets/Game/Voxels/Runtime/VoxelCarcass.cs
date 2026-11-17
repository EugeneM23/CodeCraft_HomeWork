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

            public TornFace[] Torn;
        }

        private struct TornVoxel
        {
            public int3 Position;
            public int3 Direction;
        }

        /// <param name="clusterOfPart">Cluster of each part, by part index; any ids below the part count.</param>
        /// <param name="settings">For every body; its Origin and VoxelSize are set here.</param>
        public static List<Piece> Build(in SkinnedVoxelizer.Result grid, IReadOnlyList<int> clusterOfPart,
            VoxelBodySettings settings, string name)
        {
            var pieces = new List<Piece>();
            if (!grid.Voxels.IsCreated || clusterOfPart == null || clusterOfPart.Count == 0)
                return pieces;

            int clusters = 0;
            foreach (int c in clusterOfPart)
                clusters = math.max(clusters, c + 1);

            var map = new NativeArray<int>(256, Allocator.TempJob);
            for (int i = 0; i < 256; i++)
                map[i] = i < clusterOfPart.Count ? clusterOfPart[i] : -1;

            var lo = new NativeArray<int3>(clusters, Allocator.TempJob);
            var hi = new NativeArray<int3>(clusters, Allocator.TempJob);
            new ClusterBoundsJob { Parts = grid.Parts, Map = map, Size = grid.Size, Min = lo, Max = hi }.Run();

            for (int c = 0; c < clusters; c++)
            {
                if (hi[c].x < lo[c].x)
                    continue;

                int3 size = hi[c] - lo[c] + 1;
                var target = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent);
                var torn = new NativeList<TornVoxel>(MAX_TORN * 4, Allocator.TempJob);
                new ExtractClusterJob
                {
                    Voxels = grid.Voxels, Parts = grid.Parts, Map = map, Size = grid.Size,
                    Cluster = c, Min = lo[c], TargetSize = size, Target = target, Torn = torn
                }.Run();

                var go = new GameObject($"{name}_{c}") { layer = settings.Layer };
                go.transform.SetPositionAndRotation(grid.Position, grid.Rotation);

                VoxelBodySettings own = settings;
                own.VoxelSize = grid.VoxelSize;
                own.Origin = grid.Origin + (float3)lo[c] * grid.VoxelSize;

                VoxelBody body = go.AddComponent<VoxelBody>();
                body.Init(size, target, new NativeArray<Color32>(grid.Palette, Allocator.Persistent), own);

                pieces.Add(new Piece
                {
                    Body = body,
                    Cluster = c,
                    Parts = CountParts(clusterOfPart, c),
                    Torn = Faces(go.transform, grid, torn)
                });
                torn.Dispose();
            }

            map.Dispose();
            lo.Dispose();
            hi.Dispose();
            return pieces;
        }

        private static int CountParts(IReadOnlyList<int> clusterOfPart, int cluster)
        {
            int count = 0;
            foreach (int c in clusterOfPart)
                if (c == cluster)
                    count++;
            return count;
        }

        /// <summary>At most MAX_TORN faces, picked evenly along the list so the whole cut bleeds.</summary>
        private static TornFace[] Faces(Transform space, in SkinnedVoxelizer.Result grid, NativeList<TornVoxel> torn)
        {
            int count = math.min(torn.Length, MAX_TORN);
            var faces = new TornFace[count];
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

            return faces;
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
