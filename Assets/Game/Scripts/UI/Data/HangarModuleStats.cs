using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// STUB: what one module does to the hangar's numbers, until the combat team's TankStatsUseCase. A flat bonus
    /// (the armor-piercing gun: +100 damage), a percent (screens: +40 % health, −8 % speed) and a percent added by
    /// every level above 1 (GDD §6: +15 % to the two main parameters).
    /// </summary>
    [Serializable]
    public sealed class HangarModuleStats
    {
        [Tooltip("ModuleIds value, e.g. gun_ap")]
        public string moduleId;
        [Tooltip("Added to the tank's base numbers")]
        public HangarTankStats bonus;
        [Tooltip("Percent of the sum of base and bonuses; negative lowers (reload −15 = faster)")]
        public HangarTankStats percent;
        [Tooltip("Percent added by each level above 1")]
        public HangarTankStats levelPercent;
    }
}
