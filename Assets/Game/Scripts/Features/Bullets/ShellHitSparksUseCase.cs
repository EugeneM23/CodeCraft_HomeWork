using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// An HE shell's hit throws sparks like a machine-gun bullet's, only many more, faster and longer-burning, and
    /// leaves a hot point whose light is a little brighter and reaches further, cooling out the same way. On top of the
    /// blast, crater and smoke (<see cref="ExplosionUseCase.Explode"/>); the same pools as the bullets'
    /// (<see cref="VoxelSparks"/>, <see cref="BulletHotSpots"/>), under the same light caps.
    /// </summary>
    public static class ShellHitSparksUseCase
    {
        /// <summary>Sparks per HE hit (a bullet's: 26).</summary>
        public const int SPARKS = 140;

        /// <summary>How much faster and longer the sparks fly than a bullet's.</summary>
        public const float SPARK_POWER = 1.7f;

        /// <summary>How much bigger and brighter the hot point and its light are than a bullet's.</summary>
        public const float GLOW_POWER = 1.6f;

        /// <summary>Whether <paramref name="projectile"/> is a typed cannon shell of the HE kind.</summary>
        public static bool IsHighExplosive(IEntity projectile) =>
            AmmoUseCase.IsShell(projectile) && AmmoUseCase.KindOf(projectile) == ShellKind.HighExplosive;

        /// <summary>The burst at <paramref name="point"/> on a surface facing <paramref name="normal"/> (zero: up).</summary>
        public static void Emit(Vector3 point, Vector3 normal)
        {
            Vector3 n = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
            // Struck square on: the spray fans out of the surface all round, not along a ricochet.
            VoxelSparks.Emit(point, n, -n, SPARKS, SPARK_POWER);
            BulletHotSpots.Emit(point, n, GLOW_POWER);
        }
    }
}
