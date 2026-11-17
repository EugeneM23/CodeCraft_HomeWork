using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>Digs earth out of the ground and throws it: the tank's nose ploughing (TankPloughBehaviour).</summary>
    public interface IEarthThrow
    {
        /// <param name="velocity">Mean velocity of the thrown earth.</param>
        /// <param name="spread">0 one tight clump .. 1 a wide fan of clumps and crumbs.</param>
        /// <param name="volumeShare">Share of what is dug out that flies.</param>
        /// <param name="debrisBudget">Most debris cubes this bite may throw, before the volume share.</param>
        /// <returns>Voxels dug out.</returns>
        int Excavate(VoxelBody ground, Vector3 point, float radius, Vector3 velocity, float spread, float volumeShare,
            int debrisBudget);
    }
}
