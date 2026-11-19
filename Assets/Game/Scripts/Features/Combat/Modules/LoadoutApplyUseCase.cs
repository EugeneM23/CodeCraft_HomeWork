using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Writes a tank's loadout stats, with the run skills' multipliers over them, onto the tank and its weapons:
    /// max health (a gain heals by the gain), speed and boost, turret, the cannon's damage, blast radius
    /// (ModulesAPI.ShellRadius), pierce (TargetingAPI.ShellPierce) and reload, the machine gun's damage, interval,
    /// spread and range (ModulesAPI.WeaponRange), the crush threshold and width, and the special module's numbers.
    /// Runs on events (attach, run start, skills changed), never per frame.
    /// </summary>
    public static class LoadoutApplyUseCase
    {
        public static void Apply(IEntity tank, TankCombatStats stats, CombatModifiers modifiers)
        {
            if (tank == null)
                return;

            float healthFactor = modifiers != null ? modifiers.MaxHealthFactor : 1f;
            float reloadFactor = modifiers != null ? modifiers.CannonReloadFactor : 1f;

            ApplyHealth(tank, Mathf.Max(1, Mathf.RoundToInt(stats.Health * healthFactor)));
            ApplyDrive(tank, stats);

            if (tank.TryGetTurretRotationSpeed(out IValue<float> turret) && turret is IVariable<float> turretSpeed)
                turretSpeed.Value = stats.TurretSpeed;

            if (tank.TryGetWeapon(out IReactiveVariable<IEntity> cannon) && cannon.Value != null)
                ApplyCannon(cannon.Value, stats, reloadFactor);

            if (tank.TryGetSecondaryWeapon(out IReactiveVariable<IEntity> mg) && mg.Value != null)
                ApplyMachineGun(mg.Value, stats);

            if (tank.TryGetBehaviour(out TankCrushBehaviour crush))
            {
                crush.MinSpeed = stats.CrushSpeed;
                crush.WidthFactor = stats.CrushWidthFactor;
            }

            if (tank.TryGetSpecialModule(out ISpecialModule special) && special is SmokeLauncherModule smoke)
            {
                smoke.Duration = stats.SpecialDuration;
                smoke.Cooldown = stats.SpecialCooldown;
            }
        }

        /// <summary>Max health to the value; when it grows, the tank heals by the gain (an alive tank only).</summary>
        public static void ApplyHealth(IEntity tank, int max)
        {
            if (!tank.TryGetHealth(out Health health))
                return;

            int gain = max - health.GetMax();
            health.SetMax(max);
            if (gain > 0 && health.Exists())
                health.Add(gain);
        }

        private static void ApplyDrive(IEntity tank, TankCombatStats stats)
        {
            if (tank.TryGetMoveSpeed(out IReactiveVariable<float> speed))
                speed.Value = stats.Speed;

            if (stats.Speed > 0f && tank.TryGetBehaviour(out TankDriveBehaviour drive))
                drive.BoostMultiplier = stats.BoostSpeed / stats.Speed;
        }

        private static void ApplyCannon(IEntity cannon, TankCombatStats stats, float reloadFactor)
        {
            if (cannon.TryGetDamage(out IReactiveVariable<int> damage))
                damage.Value = Mathf.RoundToInt(stats.CannonDamage);

            if (cannon.TryGetWeaponCooldown(out CooldownTimer cooldown))
                cooldown.SetDuration(Mathf.Max(0.05f, stats.CannonReload * reloadFactor));

            if (cannon.TryGetShellRadius(out IVariable<float> radius))
                radius.Value = stats.CannonRadius;
            else
                cannon.AddShellRadius(new Variable<float>(stats.CannonRadius));

            if (cannon.TryGetShellPierce(out IVariable<int> pierce))
                pierce.Value = stats.CannonPierce;
            else
                cannon.AddShellPierce(new Variable<int>(stats.CannonPierce));
        }

        private static void ApplyMachineGun(IEntity mg, TankCombatStats stats)
        {
            if (mg.TryGetDamage(out IReactiveVariable<int> damage))
                damage.Value = Mathf.Max(1, Mathf.RoundToInt(stats.MgDamage));

            if (mg.TryGetWeaponCooldown(out CooldownTimer cooldown))
                cooldown.SetDuration(Mathf.Max(0.01f, stats.MgInterval));

            // RangeWeaponInstaller puts a constant there; SpawnUseCase reads it on every shot.
            if (mg.TryGetSpread(out IValue<float> spread) && spread is IVariable<float> spreadVariable)
                spreadVariable.Value = stats.MgSpread;
            else if (mg.HasSpread())
                mg.SetSpread(new Variable<float>(stats.MgSpread));
            else
                mg.AddSpread(new Variable<float>(stats.MgSpread));

            if (mg.TryGetWeaponRange(out IVariable<float> range))
                range.Value = stats.MgRange;
            else
                mg.AddWeaponRange(new Variable<float>(stats.MgRange));
        }
    }
}
