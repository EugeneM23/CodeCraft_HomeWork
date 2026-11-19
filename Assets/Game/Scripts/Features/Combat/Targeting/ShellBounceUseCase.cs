using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The ricochet skill (GDD §3): after a cannon shell's blast, while its tank's
    /// <see cref="CombatModifiers.RicochetBounces"/> is above the bounces the shell has made, a new shell of the same
    /// prefab and damage leaves the blast 0.5 m up towards the nearest enemy within <see cref="Range"/> that stands
    /// outside the blast radius. The new shell carries the bounce count, so the chain stops at the skill's level.
    /// The shooter is known through the shell's <c>BulletWeapon</c> and the weapon's <c>WeaponOwner</c>.
    /// </summary>
    public static class ShellBounceUseCase
    {
        private const float LIFT = 0.5f;

        /// <summary>Search range (m) of the bounce; TargetingSetup sets it from WeaponSettings.ricochetRange.</summary>
        public static float Range { get; set; } = 15f;

        public static void TryBounce(IEntity shell, Vector3 point, float radius, GameFactory factory)
        {
            if (shell == null || factory == null)
                return;
            if (!shell.TryGetBulletWeapon(out IVariable<IEntity> weaponHolder) || weaponHolder.Value == null)
                return;

            IEntity weapon = weaponHolder.Value;
            if (!weapon.TryGetWeaponOwner(out IEntity tank) || tank == null ||
                !tank.TryGetCombatModifiers(out CombatModifiers modifiers) || modifiers == null)
                return;

            int done = shell.TryGetShellBounces(out IVariable<int> bounces) ? bounces.Value : 0;
            if (!ShouldBounce(modifiers.RicochetBounces, done))
                return;

            if (!GameContext.TryGetInstance(out GameContext context) ||
                !context.TryGetEnemyTargets(out IEnemyTargets targets) || targets is not EnemyTargets enemyTargets)
                return;

            Vector3 start = point + Vector3.up * LIFT;
            if (!enemyTargets.TryFindNearestBeyond(start, radius, Range, out _, out Vector3 aim))
                return;

            Vector3 direction = aim - start;
            if (direction.sqrMagnitude < 0.0001f || !weapon.TryGetBulletPrefab(out MonoEntity prefab) || prefab == null)
                return;
            direction.Normalize();

            IEntity next = factory.Create(prefab);
            if (shell.TryGetDamage(out IReactiveVariable<int> damage))
                next.SetDamage(damage);
            next.GetTransform().SetPositionAndRotation(start, Quaternion.LookRotation(direction));
            next.GetMoveDirection().Value = direction;
            // A plain int key, kept boxed: written only when it is not 0 already, so as not to box a new one per shot.
            if (next.HasRicochetCount() && next.GetRicochetCount() != 0)
                next.SetRicochetCount(0);

            SpawnUseCase.PassThroughShooter(next, weapon);
            BulletShotUseCase.Prepare(next, weapon, done + 1);
        }

        /// <summary>The rule alone: bounces are left while the skill allows more than were made.</summary>
        public static bool ShouldBounce(int allowed, int done) => allowed > done;
    }
}
