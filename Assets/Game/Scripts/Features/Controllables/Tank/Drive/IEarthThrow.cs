using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>Digs earth out of the ground and throws it: the chunk a tank's nose rips out (TankPloughBehaviour).</summary>
    public interface IEarthThrow
    {
        /// <summary>Takes the earth out of a ball of <paramref name="radius"/> m; throws nothing.</summary>
        /// <returns>Voxels dug out.</returns>
        int Dig(VoxelBody ground, Vector3 point, float radius);

        /// <summary>Throws <paramref name="earth"/>.</summary>
        /// <returns>Debris cubes thrown; 0 for none.</returns>
        int Throw(in EarthThrow earth);
    }
}
