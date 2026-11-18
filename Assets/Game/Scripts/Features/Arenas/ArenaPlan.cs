using System.Collections.Generic;
using Game.Voxels;
using UnityEngine;

namespace Game.Arenas
{
    /// <summary>A house to build: a <see cref="VoxelStructure"/> standing on a flat pad.</summary>
    public struct HousePlan
    {
        public VoxelStructure.Shape Shape;

        /// <summary>Grid size in voxels (X width, Y height, Z depth); the front faces -Z, toward the entry.</summary>
        public Vector3Int Size;

        /// <summary>Middle of the footprint, on the ground (world).</summary>
        public Vector3 Position;

        public int Seed;

        /// <summary>The footprint on the ground (world X/Z), for spacing and burrows.</summary>
        public Rect Footprint;
    }

    /// <summary>A tree: a <see cref="VoxelStructure"/> of shape Tree.</summary>
    public struct TreePlan
    {
        public Vector3 Position;
        public Vector3Int Size;
        public int Seed;

        /// <summary>Outside the playable square: scenery round the edge.</summary>
        public bool Outside;
    }

    /// <summary>A straight run of wooden fence along X or Z.</summary>
    public struct FencePlan
    {
        /// <summary>One end of the run, on the ground (world).</summary>
        public Vector3 Start;

        /// <summary>Length (m) along +X, or along +Z when <see cref="AlongZ"/>.</summary>
        public float Length;

        public bool AlongZ;
        public int Seed;
    }

    /// <summary>The gate: a wall across a gap in the far edge, and the way out behind it.</summary>
    public struct GatePlan
    {
        /// <summary>Middle of the wall's footprint, on the ground (world).</summary>
        public Vector3 Position;

        /// <summary>Wall grid size in voxels, along X.</summary>
        public Vector3Int Size;

        /// <summary>Where the tank counts as gone through: a little past the wall, outside the arena.</summary>
        public Vector3 Exit;
    }

    /// <summary>
    /// Everything one arena is made of, worked out from its definition and the run's seed by
    /// <see cref="ArenaPlanner"/>. Positions are in the world: the arena's middle is moved about
    /// the map with the seed, so the ground's mud and grass (noise in world metres) differ each run.
    /// </summary>
    public sealed class ArenaPlan
    {
        /// <summary>Edge (m) of the playable square.</summary>
        public const float SIZE = 40f;

        /// <summary>Ground (m) beyond each edge of the playable square: scenery, and the way out past the gate.</summary>
        public const float MARGIN = 6f;

        public const float VOXEL = 0.1f;

        /// <summary>Earth (m) under the lowest dip, for craters to go into.</summary>
        public const float DEPTH = 2.6f;

        /// <summary>The horde comes out no nearer the tank than this (GDD §13).</summary>
        public const float BURROW_MIN_DISTANCE = 15f;

        public ArenaDefinition Definition;
        public int Seed;

        /// <summary>Middle of the playable square at the ground's base level (world).</summary>
        public Vector3 Centre;

        public TerrainShape Terrain;

        /// <summary>Flat pads (x, z, radius, level) under the houses, the entry and the gate.</summary>
        public Vector4[] Pads;

        public readonly List<HousePlan> Houses = new();
        public readonly List<TreePlan> Trees = new();
        public readonly List<FencePlan> Fences = new();
        public readonly List<Vector3> Burrows = new();

        /// <summary>Spots behind houses, away from the entry: where Spitters take cover.</summary>
        public readonly List<Vector3> Cover = new();

        public GatePlan Gate;

        /// <summary>Where the tank starts, on the ground, facing +Z (toward the gate).</summary>
        public Vector3 Entry;

        /// <summary>The playable square (world), from the ground's base up 20 m.</summary>
        public Bounds Bounds => new(Centre + Vector3.up * 8f, new Vector3(SIZE, 20f, SIZE));

        /// <summary>The ground's height at a point (world), as the ground tile will be built.</summary>
        public float HeightAt(float x, float z) => VoxelTerrain.Height(new Vector2(x, z), Terrain, Pads);

        /// <summary>A point lifted onto the ground.</summary>
        public Vector3 OnGround(float x, float z) => new(x, HeightAt(x, z), z);
    }
}
