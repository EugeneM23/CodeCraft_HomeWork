using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Voxels
{
    /// <summary>
    /// A building's window glass (user, 2026-11-20: a high-explosive shell into a house blows its windows out).
    /// The house generator says which palette slots are glass (<see cref="SetGlass"/>); the body lists every
    /// glass voxel once, when built, so a blast looks only at those few thousand cells, not the whole grid
    /// (<see cref="ShatterGlass"/>). Shattered glass goes as small glinting shards that fly out and are gone in
    /// a few seconds; the frames stay.
    /// </summary>
    public sealed partial class VoxelBody
    {
        /// <summary>The voxels that were glass when the body was built (grid indices); broken ones drop out. Owned.</summary>
        private NativeList<int> _glass;

        private byte _glassFirst, _glassLast;

        /// <summary>Glass voxels still in the windows (tests, the debug HUD).</summary>
        public int GlassCount => _glass.IsCreated ? _glass.Length : 0;

        /// <summary>Main-thread ms the last glass listing took (SetGlass), for the cost.</summary>
        public static float LastGlassScanMs { get; private set; }

        /// <summary>
        /// Palette slots <paramref name="first"/> .. <paramref name="first"/> + <paramref name="count"/> − 1 are the
        /// building's window glass: every voxel of them is listed now, for <see cref="ShatterGlass"/>.
        /// </summary>
        public void SetGlass(int first, int count)
        {
            if (!_voxels.IsCreated || count <= 0 || first <= 0 || first + count - 1 > 255)
                return;

            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            _glassFirst = (byte)first;
            _glassLast = (byte)(first + count - 1);
            if (_glass.IsCreated)
                _glass.Clear();
            else
                _glass = new NativeList<int>(1024, Allocator.Persistent);
            new ListSlotsJob { Voxels = _voxels, First = _glassFirst, Last = _glassLast, Cells = _glass }.Run();
            LastGlassScanMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 /
                                      System.Diagnostics.Stopwatch.Frequency);
        }

        /// <summary>Glass shards a shattering throws at most (the rest is too small to see); crumbs, within the debris budget.</summary>
        private const int GLASS_SHARDS = 240;

        /// <summary>A shard's edge, as a share of a voxel's.</summary>
        private const float GLASS_SHARD_SIZE = 0.8f;

        /// <summary>Seconds (±30 %) a shard lies before it goes: glass does not pile up.</summary>
        private const float GLASS_LIFE = 3f;

        /// <summary>Shards fly out of the window at this speed (m/s) at the blast, less further off.</summary>
        private const float GLASS_SPEED = 6f;

        /// <summary>
        /// The colours shards glint in: pale blue-grey, glossy (the alpha is dryness, MeshVoxelizer.WithWetness),
        /// brighter than the dark panes they were, so they catch the light as they fall.
        /// </summary>
        private static readonly Color32[] GlassGlint =
        {
            new(178, 206, 222, 110), new(206, 226, 236, 110), new(150, 176, 190, 130), new(226, 238, 244, 100)
        };

        /// <summary>
        /// A blast's shock wave through the windows: every glass voxel within <paramref name="innerRadius"/> of
        /// <paramref name="worldPoint"/> breaks, past it fewer and fewer up to <paramref name="outerRadius"/>. The
        /// panes go as shards thrown out away from the blast; the frames stay; broken glass stays broken.
        /// </summary>
        /// <returns>How many glass voxels broke.</returns>
        public int ShatterGlass(Vector3 worldPoint, float innerRadius, float outerRadius, int shardBudget = GLASS_SHARDS)
        {
            if (!_glass.IsCreated || _glass.Length == 0 || !_voxels.IsCreated || outerRadius <= 0f)
                return 0;

            using var marker = GlassMarker.Auto();
            float edge = VoxelSize * Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
            float3 center = ((float3)transform.InverseTransformPoint(worldPoint) - Origin) / VoxelSize;
            var removed = new NativeList<RemovedVoxel>(256, Allocator.TempJob);
            var bounds = new NativeArray<int3>(2, Allocator.TempJob);
            new ShatterGlassJob
            {
                Voxels = _voxels, Size = Size, Cells = _glass, First = _glassFirst, Last = _glassLast, Center = center,
                Inner = innerRadius / edge, Outer = math.max(innerRadius, outerRadius) / edge, Seed = _seed++,
                Removed = removed, Bounds = bounds
            }.Run();

            int broken = removed.Length;
            if (broken > 0)
            {
                SolidCount -= broken;
                MarkDirty(bounds[0], bounds[1]);
                ThrowShards(removed, worldPoint, outerRadius, edge, shardBudget);
                VoxelSignals.RaiseGlassShattered(worldPoint, broken);
            }

            bounds.Dispose();
            removed.Dispose();
            return broken;
        }

        /// <summary>A share of the broken panes as shards: out of the window away from the blast, up a little, falling.</summary>
        private void ThrowShards(NativeList<RemovedVoxel> removed, Vector3 blast, float reach, float edge, int budget)
        {
            VoxelDebris debris = VoxelWorld.Instance != null ? VoxelWorld.Instance.Debris : null;
            if (debris == null || budget <= 0)
                return;

            int step = Mathf.Max(1, Mathf.CeilToInt(removed.Length / (float)budget));
            Matrix4x4 toWorld = transform.localToWorldMatrix;
            for (int i = Random.Range(0, step); i < removed.Length; i += step)
            {
                RemovedVoxel voxel = removed[i];
                Vector3 at = toWorld.MultiplyPoint3x4(Origin + ((float3)voxel.Position + 0.5f) * VoxelSize);
                Vector3 away = at - blast;
                float distance = away.magnitude;
                away = distance > 0.01f ? away / distance : Vector3.up;
                float push = GLASS_SPEED * Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(distance / reach));
                Vector3 velocity = away * (push * Random.Range(0.6f, 1.2f)) + Vector3.up * Random.Range(0.5f, 2f) +
                                   Random.insideUnitSphere * 0.8f;
                int shard = debris.Spawn(at, velocity, GlassGlint[Random.Range(0, GlassGlint.Length)],
                    edge * GLASS_SHARD_SIZE);
                if (shard < 0)
                    return; // the store is full for dirt: the gore's share is kept
                debris.SetLife(shard, GLASS_LIFE * Random.Range(0.7f, 1.3f));
            }
        }

        private static readonly Unity.Profiling.ProfilerMarker GlassMarker = new("Voxels.Glass.Shatter");

        private void ReleaseGlass()
        {
            if (_glass.IsCreated)
                _glass.Dispose();
        }
    }

    /// <summary>Lists every voxel of the palette slots First..Last (a building's window glass, VoxelBody.SetGlass).</summary>
    [BurstCompile]
    internal struct ListSlotsJob : IJob
    {
        [ReadOnly] public NativeArray<byte> Voxels;
        public byte First, Last;
        public NativeList<int> Cells;

        public void Execute()
        {
            for (int i = 0; i < Voxels.Length; i++)
            {
                byte v = Voxels[i];
                if (v >= First && v <= Last)
                    Cells.Add(i);
            }
        }
    }

    /// <summary>
    /// Breaks the listed glass voxels round a point (VoxelBody.ShatterGlass): all within Inner, a hashed share falling
    /// to none at Outer. Broken ones, and ones no longer glass (blown out by a carve since), drop out of the list.
    /// </summary>
    [BurstCompile]
    internal struct ShatterGlassJob : IJob
    {
        public NativeArray<byte> Voxels;
        public int3 Size;
        public NativeList<int> Cells;
        public byte First, Last;

        /// <summary>The blast, in grid units.</summary>
        public float3 Center;

        public float Inner, Outer;
        public uint Seed;
        public NativeList<RemovedVoxel> Removed;

        /// <summary>Min and max of what broke.</summary>
        public NativeArray<int3> Bounds;

        public void Execute()
        {
            int3 lo = Size, hi = new int3(-1);
            for (int i = Cells.Length - 1; i >= 0; i--)
            {
                int index = Cells[i];
                byte material = Voxels[index];
                if (material < First || material > Last)
                {
                    Cells.RemoveAtSwapBack(i);
                    continue;
                }

                int3 p = VoxelMath.Coord(index, Size);
                float distance = math.distance((float3)p + 0.5f, Center);
                if (distance > Outer)
                    continue;
                float chance = distance <= Inner ? 1f : 1f - math.smoothstep(Inner, Outer, distance);
                if (VoxelMath.Hash01(p, Seed) > chance)
                    continue;

                Voxels[index] = 0;
                Removed.Add(new RemovedVoxel { Position = p, Material = material });
                Cells.RemoveAtSwapBack(i);
                lo = math.min(lo, p);
                hi = math.max(hi, p);
            }

            Bounds[0] = lo;
            Bounds[1] = hi;
        }
    }
}
