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
        private readonly int _debris;

        public DigEarthThrow(int debrisPerBite) => _debris = debrisPerBite;

        public int Excavate(VoxelBody ground, Vector3 point, float radius, Vector3 velocity, float spread,
            float volumeShare) =>
            ground.Dig(point, radius, velocity, Mathf.RoundToInt(_debris * volumeShare));
    }
}
