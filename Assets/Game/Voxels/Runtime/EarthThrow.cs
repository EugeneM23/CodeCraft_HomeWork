using UnityEngine;

namespace Game.Voxels
{
    /// <summary>What to throw with <see cref="VoxelDebris.ThrowEarth"/>.</summary>
    public struct EarthThrow
    {
        /// <summary>Where the earth leaves the ground, world space.</summary>
        public Vector3 Point;
        /// <summary>Mean velocity of the throw, m/s.</summary>
        public Vector3 Velocity;
        /// <summary>m³ of earth thrown; about 0.008 m³ per 20 cm cube.</summary>
        public float Volume;
        /// <summary>0 - one tight clump, 1 - a wide fan of small clumps and crumbs.</summary>
        public float Spread;
        /// <summary>Turf / surface colour (top cubes).</summary>
        public Color32 Top;
        /// <summary>Earth under it (dry or wet mud).</summary>
        public Color32 Body;
    }
}
