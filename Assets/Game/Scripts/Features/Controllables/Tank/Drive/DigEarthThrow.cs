using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// For now: VoxelBody.Dig takes the earth out and throws it itself. Goes once the earth team's
    /// VoxelDebris.ThrowEarth is in game/develop (DebrisEarthThrow).
    /// </summary>
    public sealed class DigEarthThrow : IEarthThrow
    {
        public int Excavate(VoxelBody ground, Vector3 point, float radius, Vector3 velocity, float spread,
            float volumeShare, int debrisBudget) =>
            ground.Dig(point, radius, velocity, Mathf.RoundToInt(debrisBudget * volumeShare));
    }
}
