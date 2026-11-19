using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// VoxelBody.Dig takes the earth out and throws none of it; the earth team's VoxelDebris.ThrowEarth
    /// throws it as clumps, crumbs and dust.
    /// </summary>
    public sealed class DebrisEarthThrow : IEarthThrow
    {
        public int Dig(VoxelBody ground, Vector3 point, float radius) => ground.Dig(point, radius, Vector3.zero, 0);

        public int Throw(in EarthThrow earth)
        {
            VoxelWorld world = VoxelWorld.Instance;
            VoxelDebris debris = world != null ? world.Debris : null;
            return debris != null ? debris.ThrowEarth(earth) : 0;
        }
    }
}
