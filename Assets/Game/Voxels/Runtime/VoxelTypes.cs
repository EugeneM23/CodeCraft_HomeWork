using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxels
{
    /// <summary>
    /// What keeps the rest of a body in place once a blast cuts it in two.
    /// </summary>
    public enum VoxelAnchor
    {
        /// <summary>The biggest piece stays, every other piece becomes a body of its own (vehicles, loose pieces).</summary>
        Largest,

        /// <summary>Every piece touching the bottom layer stands; the rest falls (buildings on the ground).</summary>
        Ground
    }

    /// <summary>One vertex of a chunk mesh: 28 bytes, colour baked in, no texture.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VoxelVertex
    {
        public float3 Position;
        public float3 Normal;
        public Color32 Color;

        public static readonly VertexAttributeDescriptor[] Layout =
        {
            new(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4)
        };
    }

    /// <summary>A solid box of voxels, [Min, Max) in grid coordinates: one box collider.</summary>
    public struct VoxelBox
    {
        public int3 Min;
        public int3 Max;
    }

    /// <summary>A voxel a carve took out: where it was and what it was made of.</summary>
    public struct RemovedVoxel
    {
        public int3 Position;
        public byte Material;
    }

    /// <summary>One connected piece of a body, found by the flood fill.</summary>
    public struct VoxelComponent
    {
        public int Count;
        public int3 Min;
        public int3 Max;
        public bool Grounded;

        /// <summary>The fracture cell or load island it lies in (LabelComponentsJob.UseCells); 0 otherwise.</summary>
        public int Cell;
    }

    public struct VoxelBodySettings
    {
        /// <summary>Edge of one voxel in the body's local units.</summary>
        public float VoxelSize;

        /// <summary>Local position of the min corner of voxel (0,0,0).</summary>
        public float3 Origin;

        public VoxelAnchor Anchor;

        /// <summary>Build box colliders. Off for vehicle skins that already have colliders of their own.</summary>
        public bool Colliders;

        public int Layer;

        /// <summary>Multiplies the radius of every carve: below 1 for armour.</summary>
        public float Toughness;

        /// <summary>kg per cubic metre, for the mass of pieces that break off.</summary>
        public float Density;

        /// <summary>
        /// Height of a crater as a share of its width: 1 — a ball is cut out; ground uses about
        /// 0.55, a bowl a tank can drive down into and out of.
        /// </summary>
        public float CraterDepth;

        /// <summary>Edge of a chunk in voxels; 0 — the default 16. Big flat bodies (ground) take 32: a
        /// quarter as many meshes to draw, at a higher cost per remesh.</summary>
        public int ChunkSize;

        /// <summary>
        /// Draw (and collide with) a smooth surface over the voxels instead of cubes — for ground
        /// (see SurfaceNetsJob). The voxels underneath are the same and break the same.
        /// </summary>
        public bool Smooth;

        /// <summary>
        /// Look for pieces that lost their hold after a carve. Off for ground, which is one body
        /// too big to flood-fill on every shot, and holds on anyway.
        /// </summary>
        public bool Split;

        /// <summary>The rolling ground: driven on, never rammed (see VoxelRammer).</summary>
        public bool Terrain;

        /// <summary>Edge (voxels) of a collision cell; 0 or 1 — every voxel. Ragged bodies (trees) take 3.</summary>
        public int ColliderCell;

        /// <summary>Null — the shared voxel material.</summary>
        public Material Material;

        /// <summary>
        /// Once loose, the body turns kinematic after lying still a second (VoxelSettle) and then
        /// costs only its drawing. A carve harder than VoxelBody.WAKE_FORCE lets it go again.
        /// </summary>
        public bool Settle;

        /// <summary>Debris this body throws (carves, clods, fracture crumbs) lies for good (VoxelDebris.SetKeep).</summary>
        public bool KeepDebris;

        /// <summary>Slowest impact (m/s) that breaks the body once loose; 0 — VoxelImpactFracture's own.</summary>
        public float FractureSpeed;

        public static VoxelBodySettings Default(float voxelSize) => new()
        {
            VoxelSize = voxelSize,
            Origin = float3.zero,
            Anchor = VoxelAnchor.Largest,
            Colliders = true,
            Layer = 0,
            Toughness = 1f,
            Density = 1800f,
            CraterDepth = 1f,
            Split = true
        };
    }

    internal static class VoxelMath
    {
        public static int Index(int3 p, int3 size) => p.x + size.x * (p.y + size.y * p.z);

        public static int3 Coord(int index, int3 size)
        {
            int x = index % size.x;
            int yz = index / size.x;
            return new int3(x, yz % size.y, yz / size.y);
        }

        public static bool Inside(int3 p, int3 size) => math.all(p >= 0) && math.all(p < size);

        /// <summary>A stable 0..1 number per cell, for rough blast edges and colour variation.</summary>
        public static float Hash01(int3 p, uint seed) =>
            (math.hash(new uint4((uint)p.x, (uint)p.y, (uint)p.z, seed)) & 0xFFFFFF) / 16777216f;
    }
}
