using Game.Voxels;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A straight run of a village wooden picket fence, built of voxels and breakable: posts sunk in
    /// the ground, two rails behind, weathered pickets in front. The posts' bottom layer is the
    /// anchor, so whatever a shell or a tank cuts off from the posts falls down.
    /// </summary>
    /// <remarks>
    /// The object's position is one end of the run, on the ground; the run goes along +X, or +Z
    /// when built along Z. Only whole-voxel, grid-aligned boxes: no slopes, no rotations.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ArenaFence : MonoBehaviour
    {
        private const float VOXEL_SIZE = 0.1f;
        private const int HEIGHT = 13;
        private const int DEPTH = 3;

        /// <summary>Distance between posts in voxels (about 1.8 m).</summary>
        private const int POST_SPACING = 18;
        private const int POST_WIDTH = 2;

        /// <summary>Pickets stand at w = 0, rails at w = 1, posts fill w = 1..2.</summary>
        private const int PICKET_W = 0;
        private const int RAIL_W = 1;

        private const int LOW_RAIL = 3;
        private const int HIGH_RAIL = 9;
        private const int RAIL_HEIGHT = 2;
        private const int PICKET_BOTTOM = 1;

        /// <summary>Share of pickets missing altogether, and of those snapped off short (weathered look).</summary>
        private const uint MISSING_PERCENT = 8;
        private const uint BROKEN_PERCENT = 6;

        private const int PICKET = 0;
        private const int POST = 1;
        private const int RAIL = 2;

        private static readonly Color PicketColor = new(0.42f, 0.36f, 0.28f);
        private static readonly Color PostColor = new(0.3f, 0.24f, 0.17f);
        private static readonly Color RailColor = new(0.36f, 0.29f, 0.2f);

        private VoxelBody _body;
        private int _length;
        private int _height;
        private bool _alongZ;

        /// <summary>How many voxels each column of the run is lifted to follow the ground.</summary>
        private int[] _lift;

        /// <summary>
        /// Builds the fence once: <paramref name="length"/> metres long, along +X or +Z, its pickets
        /// varied by <paramref name="seed"/>, its colliders on <paramref name="layer"/>.
        /// </summary>
        /// <param name="groundAt">
        /// Optional: the ground height (world Y) at a distance (m) along the run. When given, the
        /// fence steps with the ground column by column and the object is moved down to the lowest
        /// ground, its posts sunk half a voxel.
        /// </param>
        public void Build(float length, bool alongZ, int seed, int layer, System.Func<float, float> groundAt = null)
        {
            if (_body != null)
                return;

            _length = math.max(4, Mathf.RoundToInt(length / VOXEL_SIZE));
            _alongZ = alongZ;
            _lift = new int[_length];
            int maxLift = 0;
            if (groundAt != null)
            {
                var ground = new float[_length];
                float minGround = float.MaxValue;
                for (int u = 0; u < _length; u++)
                {
                    ground[u] = groundAt((u + 0.5f) * VOXEL_SIZE);
                    minGround = math.min(minGround, ground[u]);
                }

                for (int u = 0; u < _length; u++)
                {
                    _lift[u] = math.max(0, Mathf.RoundToInt((ground[u] - minGround) / VOXEL_SIZE));
                    maxLift = math.max(maxLift, _lift[u]);
                }

                Vector3 position = transform.position;
                transform.position = new Vector3(position.x, minGround - VOXEL_SIZE * 0.5f, position.z);
            }

            _height = HEIGHT + maxLift;
            int3 size = alongZ ? new int3(DEPTH, _height, _length) : new int3(_length, _height, DEPTH);

            var voxels = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent);
            var palette = new NativeArray<Color32>(256, Allocator.Persistent);
            MeshVoxelizer.FillShades(palette, 1 + PICKET * MeshVoxelizer.VARIANTS, PicketColor, 0.12f);
            MeshVoxelizer.FillShades(palette, 1 + POST * MeshVoxelizer.VARIANTS, PostColor, 0.12f);
            MeshVoxelizer.FillShades(palette, 1 + RAIL * MeshVoxelizer.VARIANTS, RailColor, 0.12f);

            Fill(voxels, size, (uint)seed);

            var go = new GameObject("Voxels") { layer = layer };
            go.transform.SetParent(transform, false);

            VoxelBodySettings settings = VoxelBodySettings.Default(VOXEL_SIZE);
            // The run starts at the object; across the run the fence is centred on it.
            settings.Origin = alongZ
                ? new float3(-DEPTH * 0.5f * VOXEL_SIZE, 0f, 0f)
                : new float3(0f, 0f, -DEPTH * 0.5f * VOXEL_SIZE);
            settings.Anchor = VoxelAnchor.Ground;
            settings.Colliders = true;
            settings.Layer = layer;
            settings.Density = 600f;
            settings.CraterDepth = 1f;
            settings.Split = true;
            settings.ColliderCell = 1;

            _body = go.AddComponent<VoxelBody>();
            _body.Init(size, voxels, palette, settings);
        }

        /// <summary>
        /// Fills the grid: posts at both ends and about every 1.8 m between, rails from post to post,
        /// pickets on every other column in front of the rails. Everything is face-connected: rails
        /// butt onto the posts, every picket lies on the low rail. On uneven ground each column is
        /// lifted by its own step; posts reach down to the bottom (anchor) layer, and a rail at a
        /// step spans the heights of its neighbours' rails too, so the two still share a face.
        /// </summary>
        private void Fill(NativeArray<byte> voxels, int3 size, uint seed)
        {
            int last = _length - POST_WIDTH;
            int spans = math.max(1, (int)math.round(last / (float)POST_SPACING));
            var isPost = new bool[_length];
            for (int i = 0; i <= spans; i++)
            {
                int start = (int)math.round(i * last / (float)spans);
                for (int u = start; u < start + POST_WIDTH; u++)
                    isPost[u] = true;
            }

            for (int u = 0; u < _length; u++)
            {
                int lift = _lift[u];
                if (isPost[u])
                {
                    for (int y = 0; y < lift + HEIGHT; y++)
                    for (int w = RAIL_W; w < DEPTH; w++)
                        Set(voxels, size, u, y, w, POST, seed);
                    continue;
                }

                int lowLift = math.min(lift, math.min(_lift[math.max(0, u - 1)], _lift[math.min(_length - 1, u + 1)]));
                int highLift = math.max(lift, math.max(_lift[math.max(0, u - 1)], _lift[math.min(_length - 1, u + 1)]));
                for (int y = lowLift + LOW_RAIL; y < highLift + LOW_RAIL + RAIL_HEIGHT; y++)
                    Set(voxels, size, u, y, RAIL_W, RAIL, seed);
                for (int y = lowLift + HIGH_RAIL; y < highLift + HIGH_RAIL + RAIL_HEIGHT; y++)
                    Set(voxels, size, u, y, RAIL_W, RAIL, seed);

                // One picket wide, one voxel gap: pickets on odd columns.
                if ((u & 1) == 0)
                    continue;
                uint roll = Hash(seed, u, 7, 0) % 100;
                if (roll < MISSING_PERCENT)
                    continue;
                int top = roll < MISSING_PERCENT + BROKEN_PERCENT
                    ? LOW_RAIL + RAIL_HEIGHT + (int)(Hash(seed, u, 11, 0) % 3) // snapped off above the low rail
                    : 10 + (int)(Hash(seed, u, 13, 0) % 3); // 10..12: tops above the high rail
                for (int y = lift + PICKET_BOTTOM; y < lift + top; y++)
                    Set(voxels, size, u, y, PICKET_W, PICKET, seed);
            }
        }

        /// <summary>Writes one voxel of a material, a shade picked by the hash; (u, y, w) is run, height, depth.</summary>
        private void Set(NativeArray<byte> voxels, int3 size, int u, int y, int w, int material, uint seed)
        {
            int x = _alongZ ? w : u;
            int z = _alongZ ? u : w;
            int shade = (int)(Hash(seed, u, y, w) % MeshVoxelizer.VARIANTS);
            voxels[x + size.x * (y + size.y * z)] = (byte)(1 + material * MeshVoxelizer.VARIANTS + shade);
        }

        /// <summary>A cheap integer hash of a grid cell and the seed.</summary>
        private static uint Hash(uint seed, int a, int b, int c)
        {
            uint h = seed * 0x9E3779B1u ^ (uint)a * 0x85EBCA77u ^ (uint)b * 0xC2B2AE3Du ^ (uint)c * 0x27D4EB2Fu;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return h;
        }

        private void OnDrawGizmosSelected()
        {
            if (_length == 0)
                return;
            Gizmos.matrix = transform.localToWorldMatrix;
            float run = _length * VOXEL_SIZE;
            float height = _height * VOXEL_SIZE;
            float depth = DEPTH * VOXEL_SIZE;
            Vector3 extent = _alongZ ? new Vector3(depth, height, run) : new Vector3(run, height, depth);
            Vector3 centre = _alongZ ? new Vector3(0f, height * 0.5f, run * 0.5f) : new Vector3(run * 0.5f, height * 0.5f, 0f);
            Gizmos.DrawWireCube(centre, extent);
        }
    }
}
