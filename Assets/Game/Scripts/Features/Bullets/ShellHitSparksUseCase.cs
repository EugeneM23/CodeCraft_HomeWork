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

        private const int FLESH_BLOOD = 60;
        private const int FLESH_BITS = 14;
        // As far back as the sparks fly (8..29 m/s, ~10 m): goo is damped harder than a spark while it rises, so faster.
        private const float FLESH_SPEED = 22f;
        private const float FLESH_BLOOD_SIZE = 0.07f;
        private const float FLESH_BIT_SIZE = 0.15f;

        private static readonly Color32[] Blood =
        {
            Voxels.VoxelBlood.Colour(0, 0), Voxels.VoxelBlood.Colour(1, 0), Voxels.VoxelBlood.Colour(2, 0)
        };

        private static readonly Color32[] Flesh =
        {
            new(120, 32, 28, 255), new(92, 24, 22, 255), new(104, 98, 62, 255), new(82, 86, 50, 255)
        };

        /// <summary>
        /// The same burst on a monster: blood and bits of flesh instead of sparks and a hot point, thrown out as far as
        /// the sparks would fly, fanning out of the body round <paramref name="normal"/> (zero: up).
        /// </summary>
        public static void EmitFlesh(Vector3 point, Vector3 normal)
        {
            Vector3 n = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
            Vector3 fan = (n + Vector3.up * 0.35f).normalized;
            Voxels.VoxelSpray.Goo(point, fan, FLESH_BLOOD, FLESH_SPEED, 0.9f, Blood, FLESH_BLOOD_SIZE, default, 0.2f);
            Voxels.VoxelSpray.Goo(point, fan, FLESH_BITS, FLESH_SPEED * 0.85f, 0.8f, Flesh, FLESH_BIT_SIZE, default, 0.2f);
        }
    }
}
