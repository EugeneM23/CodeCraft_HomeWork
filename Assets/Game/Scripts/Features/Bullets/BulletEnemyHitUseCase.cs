using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A bullet or shell meets an enemy without a collider (a horde monster) on its swept path
    /// (<see cref="BulletMoveBehaviour"/>): a piercing shell deals its damage and flies on, an explosive one bursts
    /// there (its blast reaches the horde through <see cref="EnemyTargets.DamageAreaNonPhysical"/>), a plain bullet
    /// deals its damage and is spent.
    /// </summary>
    public static class BulletEnemyHitUseCase
    {
        /// <summary>True when the bullet is spent (returned to the pool); false — it flies on.</summary>
        public static bool Hit(IEntity bullet, IEnemyTargets targets, in EnemyRef target, Vector3 point,
            GameFactory factory)
        {
            float damage = bullet.TryGetDamage(out var value) ? value.Value : 0f;

            if (BulletShotUseCase.TryPierce(bullet))
            {
                targets.Damage(target, damage, point);
                return false;
            }

            if (ExplosionUseCase.IsExplosive(bullet))
            {
                // No area damage (WeaponSettings.shellAreaDamage off): the monster hit takes the shell's damage, and
                // if that kills it, it is torn apart rather than falling whole.
                if (!ExplosionUseCase.AreaDamage)
                {
                    Vector3 shot = bullet.TryGetMoveDirection(out var flew) ? flew.Value : Vector3.zero;
                    targets.Tear(target, shot);
                    targets.Damage(target, damage, point);
                }

                ExplosionUseCase.Explode(bullet, point, null, factory);
            }
            else
            {
                // A plain bullet (the machine gun) goes clean through: the monster takes its damage, blood bursts out
                // along the shot, and the bullet flies on to the next one.
                targets.Damage(target, damage, point);
                Vector3 shot = bullet.TryGetMoveDirection(out var direction) ? direction.Value : Vector3.forward;
                SprayThrough(point, shot);
                return false;
            }

            SpawnUseCase.UnSpawnEntity(factory, bullet);
            return true;
        }

        private const int THROUGH_DROPS = 14;
        private const float THROUGH_SPEED = 5f;
        private const float THROUGH_DROP_SIZE = 0.07f;

        private static readonly Color32[] Blood =
        {
            Voxels.VoxelBlood.Colour(0, 0), Voxels.VoxelBlood.Colour(1, 0), Voxels.VoxelBlood.Colour(2, 0)
        };

        /// <summary>A bullet through a monster: an exit-wound jet of blood along the shot, a little up and aside.</summary>
        public static void SprayThrough(Vector3 point, Vector3 shot) =>
            Voxels.VoxelSpray.ShotGoo(point, shot, THROUGH_DROPS, THROUGH_SPEED, 0.5f, Blood, THROUGH_DROP_SIZE, default, 0.15f);
    }
}
