using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A tank's combat numbers with a loadout (GDD §4, §6), before run skills. Pure: no scene needed, so the hangar
    /// compares a loadout with and without a module ("before → after"). The short overloads take the base and the
    /// module effects from <see cref="CombatConfig.Load"/>; the long one takes them as given (the tests).
    /// </summary>
    public static class TankStatsUseCase
    {
        /// <summary>The tank's base (catalog) with these modules at these levels.</summary>
        public static TankCombatStats Compute(TankConfig tank, IReadOnlyList<ModuleLevel> loadout)
        {
            CombatConfig config = CombatConfig.Load();
            return Compute(tank, loadout, config != null ? config.Tiger : null, config != null ? config.Modules : null);
        }

        /// <summary>The tank with what is installed on it in the save and the modules' saved levels.</summary>
        public static TankCombatStats Compute(TankConfig tank, IMetaProgress progress)
        {
            List<ModuleLevel> loadout = new(TankConfig.SLOT_COUNT);
            ResolveLoadout(tank, progress, loadout);
            return Compute(tank, loadout);
        }

        /// <summary>
        /// The tank (or, without one, <paramref name="baseSettings"/>'s health, speed and turret) with the loadout.
        /// Module effects come from <paramref name="modules"/>, else their code defaults; an unknown module changes
        /// nothing. Every Set of the loadout goes first, then every Multiply.
        /// </summary>
        public static TankCombatStats Compute(TankConfig tank, IReadOnlyList<ModuleLevel> loadout,
            TankBaseSettings baseSettings, IReadOnlyList<ModuleEffectConfig> modules)
        {
            TankBaseSettings b = baseSettings ?? new TankBaseSettings();

            TankCombatStats stats = default;
            stats.Health = tank != null ? tank.Health : b.Health;
            stats.Speed = tank != null ? tank.Speed : b.Speed;
            stats.BoostSpeed = b.Speed > 0f ? stats.Speed * (b.BoostSpeed / b.Speed) : stats.Speed;
            stats.TurretSpeed = tank != null ? tank.TurretSpeed : b.TurretSpeed;

            stats.CannonDamage = b.CannonDamage;
            stats.CannonRadius = b.CannonRadius;
            stats.CannonReload = b.CannonReload;
            stats.CannonPierce = 0;

            stats.MgDamage = b.MgDamage;
            stats.MgInterval = b.MgInterval;
            stats.MgSpread = b.MgSpread;
            stats.MgRange = b.MgRange;

            stats.CrushSpeed = b.CrushSpeed;
            stats.CrushWidthFactor = b.CrushWidthFactor;

            stats.SpecialDuration = 0f;
            stats.SpecialCooldown = 0f;

            if (loadout == null)
                return stats;

            ApplyPass(ref stats, loadout, modules, ModuleStatOp.Set);
            ApplyPass(ref stats, loadout, modules, ModuleStatOp.Multiply);
            return stats;
        }

        /// <summary>
        /// The tank's loadout into <paramref name="into"/> (cleared first): what the save has in each slot at its saved
        /// level (at least 1), the tank's default for a slot the save leaves empty, and the standard module without
        /// either — so with no save it is the standard loadout at level 1.
        /// </summary>
        public static void ResolveLoadout(TankConfig tank, IMetaProgress progress, List<ModuleLevel> into)
        {
            into.Clear();
            TankId tankId = tank != null ? tank.Id : progress != null ? progress.SelectedTank : TankIds.Tiger;

            for (int slot = 0; slot < TankConfig.SLOT_COUNT; slot++)
            {
                ModuleSlot moduleSlot = (ModuleSlot)slot;
                ModuleId module = progress != null ? progress.GetInstalled(tankId, moduleSlot) : default;
                if (module.IsNone && tank != null)
                    module = tank.DefaultModule(moduleSlot);
                if (module.IsNone)
                    module = StandardModule(moduleSlot);
                if (module.IsNone)
                    continue;

                int level = progress != null ? progress.GetModuleLevel(module) : 1;
                into.Add(new ModuleLevel(module, Mathf.Clamp(level, 1, ModuleConfig.MAX_LEVEL)));
            }
        }

        /// <summary>The standard module of a slot (GDD §6), what a new tank has.</summary>
        public static ModuleId StandardModule(ModuleSlot slot)
        {
            switch (slot)
            {
                case ModuleSlot.Gun: return ModuleIds.Gun88He;
                case ModuleSlot.MachineGun: return ModuleIds.MachineGun;
                case ModuleSlot.Armor: return ModuleIds.ArmorStandard;
                case ModuleSlot.Engine: return ModuleIds.EngineStandard;
                case ModuleSlot.Tracks: return ModuleIds.TracksStandard;
                case ModuleSlot.Special: return ModuleIds.SpecialSmoke;
                default: return default;
            }
        }

        /// <summary>Whether the loadout holds the module.</summary>
        public static bool Contains(IReadOnlyList<ModuleLevel> loadout, ModuleId module)
        {
            if (loadout == null)
                return false;
            for (int i = 0; i < loadout.Count; i++)
            {
                if (loadout[i].Id == module)
                    return true;
            }

            return false;
        }

        private static void ApplyPass(ref TankCombatStats stats, IReadOnlyList<ModuleLevel> loadout,
            IReadOnlyList<ModuleEffectConfig> modules, ModuleStatOp op)
        {
            for (int i = 0; i < loadout.Count; i++)
            {
                ModuleLevel module = loadout[i];
                ModuleEffectConfig config = ModuleEffectConfig.Find(modules, module.Id);
                if (config == null || config.Effects == null)
                    continue;

                for (int e = 0; e < config.Effects.Length; e++)
                {
                    ModuleStatEffect effect = config.Effects[e];
                    if (effect.Op != op)
                        continue;
                    float value = effect.ValueAt(module.Level);
                    float current = Get(in stats, effect.Stat);
                    Set(ref stats, effect.Stat, op == ModuleStatOp.Set ? value : current * value);
                }
            }
        }

        private static float Get(in TankCombatStats stats, ModuleStat stat)
        {
            switch (stat)
            {
                case ModuleStat.Health: return stats.Health;
                case ModuleStat.Speed: return stats.Speed;
                case ModuleStat.BoostSpeed: return stats.BoostSpeed;
                case ModuleStat.TurretSpeed: return stats.TurretSpeed;
                case ModuleStat.CannonDamage: return stats.CannonDamage;
                case ModuleStat.CannonRadius: return stats.CannonRadius;
                case ModuleStat.CannonReload: return stats.CannonReload;
                case ModuleStat.CannonPierce: return stats.CannonPierce;
                case ModuleStat.MgDamage: return stats.MgDamage;
                case ModuleStat.MgInterval: return stats.MgInterval;
                case ModuleStat.MgSpread: return stats.MgSpread;
                case ModuleStat.MgRange: return stats.MgRange;
                case ModuleStat.CrushSpeed: return stats.CrushSpeed;
                case ModuleStat.CrushWidthFactor: return stats.CrushWidthFactor;
                case ModuleStat.SpecialDuration: return stats.SpecialDuration;
                case ModuleStat.SpecialCooldown: return stats.SpecialCooldown;
                default: return 0f;
            }
        }

        private static void Set(ref TankCombatStats stats, ModuleStat stat, float value)
        {
            switch (stat)
            {
                case ModuleStat.Health: stats.Health = value; break;
                case ModuleStat.Speed: stats.Speed = value; break;
                case ModuleStat.BoostSpeed: stats.BoostSpeed = value; break;
                case ModuleStat.TurretSpeed: stats.TurretSpeed = value; break;
                case ModuleStat.CannonDamage: stats.CannonDamage = value; break;
                case ModuleStat.CannonRadius: stats.CannonRadius = value; break;
                case ModuleStat.CannonReload: stats.CannonReload = value; break;
                case ModuleStat.CannonPierce: stats.CannonPierce = Mathf.RoundToInt(value); break;
                case ModuleStat.MgDamage: stats.MgDamage = value; break;
                case ModuleStat.MgInterval: stats.MgInterval = value; break;
                case ModuleStat.MgSpread: stats.MgSpread = value; break;
                case ModuleStat.MgRange: stats.MgRange = value; break;
                case ModuleStat.CrushSpeed: stats.CrushSpeed = value; break;
                case ModuleStat.CrushWidthFactor: stats.CrushWidthFactor = value; break;
                case ModuleStat.SpecialDuration: stats.SpecialDuration = value; break;
                case ModuleStat.SpecialCooldown: stats.SpecialCooldown = value; break;
            }
        }
    }
}
