using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// What a fresh shot carries besides SpawnUseCase's damage and direction: the weapon that fired it (the ricochet
    /// skill reads its owner), the shell's pierce count copied from the weapon's <c>ShellPierce</c>, and no skill
    /// bounces yet. Bullets come from a pool: every value is written on every shot, the holders made once per bullet.
    /// </summary>
    public static class BulletShotUseCase
    {
        /// <summary>SpawnUseCase.SpawnBullet and <see cref="Prepare"/>.</summary>
        public static IEntity Fire(IEntity weapon, GameFactory factory, Transform firePoint)
        {
            IEntity bullet = SpawnUseCase.SpawnBullet(weapon, factory, firePoint);
            Prepare(bullet, weapon, 0);
            return bullet;
        }

        public static void Prepare(IEntity bullet, IEntity weapon, int bounces)
        {
            if (bullet == null)
                return;

            int pierce = weapon != null && weapon.TryGetShellPierce(out IVariable<int> shellPierce)
                ? Mathf.Max(0, shellPierce.Value)
                : 0;
            SetInt(bullet, TargetingAPI.BulletPierce, pierce);

            // The loadout's blast radius (modules area: the cannon's ShellRadius) over the prefab's. ExplosionRadius is
            // a plain float key: the entity keeps it boxed and every write boxes a new one, so it is written only
            // when it differs (a pooled shell keeps the radius of its last shot, nearly always the same).
            if (weapon != null && bullet.HasExplosionRadius() &&
                weapon.TryGetShellRadius(out IVariable<float> shellRadius) && shellRadius.Value > 0f &&
                bullet.GetExplosionRadius() != shellRadius.Value)
                bullet.SetExplosionRadius(shellRadius.Value);
            SetInt(bullet, TargetingAPI.ShellBounces, bounces);

            if (bullet.TryGetBulletWeapon(out IVariable<IEntity> holder))
                holder.Value = weapon;
            else
                bullet.AddBulletWeapon(new Variable<IEntity>(weapon));
        }

        /// <summary>Enemies the bullet may still pass through.</summary>
        public static int GetPierce(IEntity bullet) =>
            bullet.TryGetBulletPierce(out IVariable<int> pierce) ? pierce.Value : 0;

        /// <summary>
        /// Uses one pierce if the bullet has any left: true — it flies on through the enemy it hit, false — it stops
        /// there (bursts or is spent).
        /// </summary>
        public static bool TryPierce(IEntity bullet)
        {
            if (!bullet.TryGetBulletPierce(out IVariable<int> pierce))
                return false;
            return TryUse(pierce);
        }

        /// <summary>The pierce rule alone: a count above zero is used up by one and lets the shell through.</summary>
        public static bool TryUse(IVariable<int> pierce)
        {
            if (pierce == null || pierce.Value <= 0)
                return false;
            pierce.Value--;
            return true;
        }

        private static void SetInt(IEntity bullet, ValueKey<IEntity, IVariable<int>> key, int value)
        {
            if (bullet.TryGetValue(key, out IVariable<int> holder))
                holder.Value = value;
            else
                bullet.AddValue(key, new Variable<int>(value));
        }
    }
}
