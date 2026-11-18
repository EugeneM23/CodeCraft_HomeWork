using System.Collections.Generic;

namespace Game
{
    /// <summary>
    /// A tank's combat numbers with a loadout (GDD §4, §6), before run skills. Pure: no scene needed, so the hangar
    /// compares a loadout with and without a module ("before → after").
    /// </summary>
    public static class TankStatsUseCase
    {
        /// <summary>The tank's base (catalog) with these modules at these levels.</summary>
        public static TankCombatStats Compute(TankConfig tank, IReadOnlyList<ModuleLevel> loadout)
        {
            TankCombatStats stats = default;
            stats.Health = tank != null ? tank.Health : 1000f;
            stats.Speed = tank != null ? tank.Speed : 6f;
            stats.TurretSpeed = tank != null ? tank.TurretSpeed : 90f;
            return stats;
        }

        /// <summary>The tank with what is installed on it in the save and the modules' saved levels.</summary>
        public static TankCombatStats Compute(TankConfig tank, IMetaProgress progress)
        {
            List<ModuleLevel> loadout = new(TankConfig.SLOT_COUNT);
            if (tank != null && progress != null)
            {
                for (int slot = 0; slot < TankConfig.SLOT_COUNT; slot++)
                {
                    ModuleId module = progress.GetInstalled(tank.Id, (ModuleSlot)slot);
                    if (!module.IsNone)
                        loadout.Add(new ModuleLevel(module, progress.GetModuleLevel(module)));
                }
            }

            return Compute(tank, loadout);
        }
    }
}
