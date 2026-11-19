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
                targets.Damage(target, damage, point);
                // A plain bullet's hot point cools where it struck the monster, as on any other hit.
                Vector3 back = bullet.TryGetMoveDirection(out var direction) ? -direction.Value : Vector3.up;
                BulletHotSpots.Emit(point, back);
            }

            SpawnUseCase.UnSpawnEntity(factory, bullet);
            return true;
        }
    }
}
