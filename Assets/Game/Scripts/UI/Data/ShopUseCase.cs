namespace Game
{
    /// <summary>Buying, upgrading and installing tank modules on the progress data. Callers save afterwards.</summary>
    public static class ShopUseCase
    {
        /// <summary>Gives a new player the start credits and modules, once.</summary>
        public static void EnsureStarted(PlayerProgressData data, ShopCatalogConfig catalog)
        {
            if (data.initialized)
                return;

            data.initialized = true;
            data.credits = catalog.startCredits;
            foreach (TankModuleConfig module in catalog.startModules)
            {
                if (module == null)
                    continue;
                data.SetModuleLevel(module.id, 1);
                data.Install(module.slot, module.id);
            }
        }

        public static bool IsOwned(PlayerProgressData data, TankModuleConfig module) => data.ModuleLevel(module.id) > 0;

        public static bool IsInstalled(PlayerProgressData data, TankModuleConfig module) =>
            data.Installed(module.slot) == module.id;

        public static bool CanBuy(PlayerProgressData data, TankModuleConfig module) =>
            !IsOwned(data, module) && data.credits >= module.price;

        public static bool TryBuy(PlayerProgressData data, TankModuleConfig module)
        {
            if (!CanBuy(data, module))
                return false;

            data.credits -= module.price;
            data.SetModuleLevel(module.id, 1);
            if (data.Installed(module.slot) == null)
                data.Install(module.slot, module.id);
            return true;
        }

        public static bool CanUpgrade(PlayerProgressData data, TankModuleConfig module)
        {
            int price = module.UpgradePrice(data.ModuleLevel(module.id));
            return price >= 0 && data.credits >= price;
        }

        public static bool TryUpgrade(PlayerProgressData data, TankModuleConfig module)
        {
            if (!CanUpgrade(data, module))
                return false;

            int level = data.ModuleLevel(module.id);
            data.credits -= module.UpgradePrice(level);
            data.SetModuleLevel(module.id, level + 1);
            return true;
        }

        public static bool TryInstall(PlayerProgressData data, TankModuleConfig module)
        {
            if (!IsOwned(data, module))
                return false;

            data.Install(module.slot, module.id);
            return true;
        }

        /// <summary>Marks the level won and pays its reward.</summary>
        public static void CompleteLevel(PlayerProgressData data, string levelId, int reward)
        {
            data.credits += reward;
            if (!data.completedLevels.Contains(levelId))
                data.completedLevels.Add(levelId);
        }
    }
}
