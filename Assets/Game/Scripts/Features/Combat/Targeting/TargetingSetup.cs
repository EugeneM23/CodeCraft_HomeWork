using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Auto guns, turrets, air strike and enemy targets: what <see cref="CombatBehaviour"/> installs.
    /// Context: the level's <see cref="EnemyTargets"/> with the entity enemies' source (the horde's source registers
    /// itself, Features/AI/Combat). Tank: the machine gun's <see cref="AutoGunBehaviour"/> and the player's focus
    /// (<see cref="CombatFocus"/>), <see cref="TurretsBehaviour"/>, <see cref="AirStrikeSkillBehaviour"/>,
    /// <see cref="CannonFiredSignalBehaviour"/>, and the weapons' <c>WeaponOwner</c> (the ricochet skill reads it).
    /// </summary>
    public static class TargetingSetup
    {
        private static EntityEnemySource s_entitySource;

        /// <summary>The hull colliders given to the machine gun at attach, taken back at detach.</summary>
        private static Collider[] s_mgShooterColliders;

        /// <summary>The big machine gun built at attach (experiment), taken down at detach.</summary>
        private static MachineGunRig s_mgRig;

        public static void InstallContext(GameContext context, CombatConfig config)
        {
            var targets = new EnemyTargets();
            if (context.HasEnemyTargets())
                context.SetEnemyTargets(targets);
            else
                context.AddEnemyTargets(targets);

            // TEMPORARY: the source also reports entity enemies' deaths until the AI team does.
            context.TryGetEnemyFilter(out EntityFilter filter);
            s_entitySource = new EntityEnemySource(context, filter, true);
            targets.Register(s_entitySource);

            ShellBounceUseCase.Range = config.Weapons.ricochetRange;
            CombatFocus.Enabled = false;
            CombatFocus.Clear();

            // Experiment: the guns' voxel casings. A context restarted in the same scene keeps the field it was given
            // the first time (as XpOrbField).
            if (config.Weapons.casingsOn && !context.TryGetBehaviour(out CasingField _))
                context.AddBehaviour(new CasingField(config.Weapons));
        }

        public static void UninstallContext(GameContext context)
        {
            s_entitySource?.Dispose();
            s_entitySource = null;

            if (context != null && context.HasEnemyTargets())
                context.DelEnemyTargets();

            CombatFocus.Enabled = false;
            CombatFocus.Clear();
        }

        public static void AttachTank(GameContext context, IEntity tank, CombatConfig config)
        {
            WeaponSettings settings = config.Weapons;
            EnemyTargets targets = context.TryGetEnemyTargets(out IEnemyTargets t) ? t as EnemyTargets : null;
            if (targets == null)
            {
                Debug.LogWarning("[TargetingSetup] No EnemyTargets in the GameContext: the tank gets no auto weapons.");
                return;
            }

            if (tank.TryGetWeapon(out IReactiveVariable<IEntity> cannon) && cannon.Value != null)
                cannon.Value.SetWeaponOwner(tank);

            context.TryGetBehaviour(out CasingField casings);

            if (tank.TryGetSecondaryWeapon(out IReactiveVariable<IEntity> mg) && mg.Value != null)
            {
                mg.Value.SetWeaponOwner(tank);

                // Experiment: the big machine gun that turns to its target; the fire point moves to its muzzle.
                s_mgRig?.Dispose();
                s_mgRig = null;
                if (settings.mgBigModel && mg.Value.TryGetFirePoint(out Transform firePoint) && firePoint != null)
                {
                    tank.TryGetTurretVisual(out Transform turretVisual);
                    s_mgRig = MachineGunRig.Build(firePoint, turretVisual, settings);
                }

                mg.Value.AddBehaviour(new AutoGunBehaviour(tank, targets, settings, s_mgRig, casings));
                // Voxel fire and a few sparks at its muzzle on every round.
                mg.Value.AddBehaviour(new MuzzleFireBehaviour(false));

                // The auto gun turns to targets on every side: its bullets fly through the tank's own hull, as the
                // turrets' do. The cannon has none: it fires along its barrel, from a muzzle outside the hull.
                if (!mg.Value.HasShooterColliders() && tank.TryGetTransform(out Transform root) && root != null)
                {
                    Collider[] hull = TurretsBehaviour.HullColliders(root);
                    if (hull.Length > 0)
                    {
                        mg.Value.AddShooterColliders(hull);
                        s_mgShooterColliders = hull;
                    }
                }

                CombatFocus.Enabled = settings.focusMode;
            }

            tank.AddBehaviour(new CannonFiredSignalBehaviour());
            tank.AddBehaviour(new TurretsBehaviour(targets, settings) { Casings = casings });
            // Found once here, not in a tick: the sandbox's air strike, absent in other levels.
            tank.AddBehaviour(new AirStrikeSkillBehaviour(targets, settings,
                Object.FindFirstObjectByType<VoxelAirStrike>())); // perf-ok: once on tank attach, not in a tick
        }

        public static void DetachTank(GameContext context, IEntity tank)
        {
            CombatFocus.Enabled = false;
            CombatFocus.Clear();

            s_mgRig?.Dispose();
            s_mgRig = null;

            if (tank == null)
                return;

            if (tank.TryGetSecondaryWeapon(out IReactiveVariable<IEntity> mg) && mg.Value != null)
            {
                mg.Value.DelBehaviour<AutoGunBehaviour>();
                if (s_mgShooterColliders != null && mg.Value.TryGetShooterColliders(out Collider[] own) &&
                    ReferenceEquals(own, s_mgShooterColliders))
                    mg.Value.DelShooterColliders();
                if (mg.Value.HasWeaponOwner())
                    mg.Value.DelWeaponOwner();
            }

            s_mgShooterColliders = null;

            if (tank.TryGetWeapon(out IReactiveVariable<IEntity> cannon) && cannon.Value != null &&
                cannon.Value.HasWeaponOwner())
                cannon.Value.DelWeaponOwner();

            tank.DelBehaviour<CannonFiredSignalBehaviour>();
            tank.DelBehaviour<TurretsBehaviour>();
            tank.DelBehaviour<AirStrikeSkillBehaviour>();
        }

        /// <summary>
        /// The cannon's shell pierce count (TargetingAPI.ShellPierce), added at 0 when missing. For the modules area:
        /// <c>TargetingSetup.GetOrAddShellPierce(cannon).Value = stats.CannonPierce</c>.
        /// </summary>
        public static IVariable<int> GetOrAddShellPierce(IEntity cannon)
        {
            if (cannon.TryGetShellPierce(out IVariable<int> pierce) && pierce != null)
                return pierce;

            pierce = new Variable<int>(0);
            cannon.SetShellPierce(pierce);
            return pierce;
        }
    }
}
