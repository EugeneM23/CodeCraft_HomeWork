using UnityEngine;

namespace Game
{
    /// <summary>The tank's stats from the base and the installed modules at their levels.</summary>
    public static class TankLoadoutUseCase
    {
        private static readonly TankSlot[] Slots = (TankSlot[])System.Enum.GetValues(typeof(TankSlot));

        public static TankStats Compute(ShopCatalogConfig catalog, IPlayerProgress progress) =>
            Compute(catalog, progress, null);

        /// <summary>
        /// The stats as if <paramref name="replacement"/> were installed in its slot (at its owned level,
        /// or level 1 when not owned): the preview of a module in the hangar and the shop.
        /// </summary>
        public static TankStats Compute(ShopCatalogConfig catalog, IPlayerProgress progress, TankModuleConfig replacement)
        {
            TankStats stats = catalog.baseStats;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (replacement != null && Slots[i] == replacement.slot)
                    continue;

                TankModuleConfig module = catalog.Find(progress.GetInstalledModule(Slots[i].ToString()));
                if (module != null)
                    stats += module.StatsAt(progress.GetModuleLevel(module.id));
            }

            if (replacement != null)
                stats += replacement.StatsAt(Mathf.Max(1, progress.GetModuleLevel(replacement.id)));

            return stats;
        }
    }
}
