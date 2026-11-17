using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// Put on the parent of the ground tiles (VoxelStructure, Ground): the tiles then rise and
    /// fall with <see cref="VoxelTerrain"/> instead of lying flat. Holds the shape of the hills and
    /// the flat pads the town stands on.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoxelTerrainSettings : MonoBehaviour
    {
        [SerializeField] private TerrainShape _shape = TerrainShape.Default;

        [Tooltip("Draw the ground as a smooth surface over the voxels (no 10 cm terraces). Off: plain cubes.")]
        [SerializeField] private bool _smooth;

        [Tooltip("Streets: centre lines (x0, z0, x1, z1), cobbled, with a kerb either side and a frayed verge beyond.")]
        [SerializeField] private Vector4[] _streets = new Vector4[0];
        [Tooltip("Half the width (m) of a street's road.")]
        [SerializeField] private float _roadHalfWidth = 1.6f;
        [Tooltip("Width (m) of the frayed strip beyond each kerb, where cobbles and earth give way to the ground.")]
        [SerializeField] private float _vergeWidth = 1.2f;

        [Tooltip("Flat pads: x, z, radius (m), level (world height). They blend into the hills over a few metres.")]
        [SerializeField] private Vector4[] _pads = new Vector4[0];

        private static VoxelTerrainSettings s_active;

        public TerrainShape Shape => _shape;
        public Vector4[] Pads => _pads;
        public bool Smooth => _smooth;
        public Vector4[] Streets => _streets;
        public float RoadHalfWidth => _roadHalfWidth;
        public float VergeWidth => _vergeWidth;

        /// <summary>The terrain of the scene, if it has one.</summary>
        public static VoxelTerrainSettings Active => s_active;

        public float HeightAt(Vector3 world) => VoxelTerrain.Height(new Vector2(world.x, world.z), _shape, _pads);

        /// <summary>The soil the ground was made with at a point (a crater shows earth, but still counts as its soil).</summary>
        public TerrainSoil SoilAt(Vector3 world)
        {
            var xz = new Vector2(world.x, world.z);
            int zone = VoxelTerrain.StreetZone(xz, _streets, _roadHalfWidth, _vergeWidth);
            return zone is VoxelTerrain.STREET_ROAD or VoxelTerrain.STREET_KERB
                ? TerrainSoil.Paved
                : VoxelTerrain.Soil(xz, _shape, _pads);
        }

        public void Set(TerrainShape shape, Vector4[] pads, Vector4[] streets = null)
        {
            _shape = shape;
            _pads = pads;
            _streets = streets ?? new Vector4[0];
        }

        private void OnEnable() => s_active = this;

        private void OnDisable()
        {
            if (s_active == this)
                s_active = null;
        }
    }
}
