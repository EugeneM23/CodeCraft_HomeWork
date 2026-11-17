using UnityEngine;

namespace Game
{
    /// <summary>The tank's stats from the base and the installed modules at their levels.</summary>
    public static class TankLoadoutUseCase
    {
        public static TankStats Compute(ShopCatalogConfig catalog, PlayerProgressData data) =>
            Compute(catalog, data, null);

        /// <summary>
        /// The stats as if <paramref name="replacement"/> were installed in its slot (at its owned level,
        /// or level 1 when not owned): the preview of a module in the hangar and the shop.
        /// </summary>
        public static TankStats Compute(ShopCatalogConfig catalog, PlayerProgressData data, TankModuleConfig replacement)
        {
            TankStats stats = catalog.baseStats;
            for (int i = 0; i < data.installed.Count; i++)
            {
                PlayerProgressData.InstalledModule entry = data.installed[i];
                if (replacement != null && entry.slot == replacement.slot)
                    continue;

                TankModuleConfig module = catalog.Find(entry.id);
                if (module != null)
                    stats += module.StatsAt(data.ModuleLevel(module.id));
            }

            if (replacement != null)
                stats += replacement.StatsAt(Mathf.Max(1, data.ModuleLevel(replacement.id)));

            return stats;
        }
    }
}
