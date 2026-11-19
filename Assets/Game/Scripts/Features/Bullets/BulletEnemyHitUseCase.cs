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
                ExplosionUseCase.Explode(bullet, point, null, factory);
            else
                targets.Damage(target, damage, point);

            SpawnUseCase.UnSpawnEntity(factory, bullet);
            return true;
        }
    }
}
