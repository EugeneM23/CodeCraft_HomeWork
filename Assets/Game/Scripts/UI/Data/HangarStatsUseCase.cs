namespace Game
{
    /// <summary>
    /// STUB: the hangar's numbers from the tank's base and its modules at their levels, until the combat team's
    /// TankStatsUseCase: (base + Σ bonus) × (1 + Σ percent / 100), per stat.
    /// </summary>
    public static class HangarStatsUseCase
    {
        /// <summary>Adds one module at its level (1-based; 0 adds nothing) to the running sums.</summary>
        public static void Add(HangarModuleStats module, int level, ref HangarTankStats bonus, ref HangarTankStats percent)
        {
            if (module == null || level < 1)
                return;

            bonus += module.bonus;
            percent += module.percent;
            for (int i = 1; i < level; i++)
                percent += module.levelPercent;
        }

        public static HangarTankStats Finish(HangarTankStats baseStats, HangarTankStats bonus, HangarTankStats percent) =>
            HangarTankStats.Scale(baseStats + bonus, percent);
    }
}
