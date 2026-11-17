namespace Game
{
    /// <summary>Buying, upgrading and installing tank modules, and the reward of a won level, on the player's progress.</summary>
    public static class ShopUseCase
    {
        /// <summary>
        /// Gives a new player the start credits and modules and opens the levels that need nothing, once:
        /// a player who owns none of the start modules is new. A catalog without start modules grants nothing
        /// (false), or every call would pay the start credits again.
        /// </summary>
        public static bool EnsureStarted(IPlayerProgress progress, ShopCatalogConfig catalog, LevelCatalog levels = null)
        {
            if (levels != null)
            {
                for (int i = 0; i < levels.Levels.Count; i++)
                {
                    LevelConfig level = levels.Levels[i];
                    if (level != null && level.RequiredLevel == null && !progress.IsLevelUnlocked(level.Id))
                        progress.UnlockLevel(level.Id);
                }
            }

            // No start modules: nothing tells a new player from an old one, so nothing is granted.
            bool hasStartModule = false;
            foreach (TankModuleConfig module in catalog.startModules)
            {
                if (module == null)
                    continue;
                if (progress.OwnsModule(module.id))
                    return false;
                hasStartModule = true;
            }

            if (!hasStartModule)
                return false;

            progress.AddCredits(catalog.startCredits);
            foreach (TankModuleConfig module in catalog.startModules)
            {
                if (module == null)
                    continue;
                progress.SetModuleLevel(module.id, 1);
                progress.InstallModule(module.slot.ToString(), module.id);
            }
            return true;
        }

        public static bool IsOwned(IPlayerProgress progress, TankModuleConfig module) => progress.OwnsModule(module.id);

        public static bool IsInstalled(IPlayerProgress progress, TankModuleConfig module) =>
            progress.GetInstalledModule(module.slot.ToString()) == module.id;

        public static bool CanBuy(IPlayerProgress progress, TankModuleConfig module) =>
            !IsOwned(progress, module) && progress.Credits >= module.price;

        public static bool TryBuy(IPlayerProgress progress, TankModuleConfig module)
        {
            if (IsOwned(progress, module) || !progress.TrySpendCredits(module.price))
                return false;

            progress.SetModuleLevel(module.id, 1);
            if (progress.GetInstalledModule(module.slot.ToString()) == null)
                progress.InstallModule(module.slot.ToString(), module.id);
            return true;
        }

        /// <summary>Price of the next level, -1 when not owned or at the top.</summary>
        public static int UpgradePrice(IPlayerProgress progress, TankModuleConfig module) =>
            module.UpgradePrice(progress.GetModuleLevel(module.id));

        public static bool CanUpgrade(IPlayerProgress progress, TankModuleConfig module)
        {
            int price = UpgradePrice(progress, module);
            return price >= 0 && progress.Credits >= price;
        }

        public static bool TryUpgrade(IPlayerProgress progress, TankModuleConfig module)
        {
            int price = UpgradePrice(progress, module);
            if (price < 0 || !progress.TrySpendCredits(price))
                return false;

            progress.SetModuleLevel(module.id, progress.GetModuleLevel(module.id) + 1);
            return true;
        }

        public static bool TryInstall(IPlayerProgress progress, TankModuleConfig module)
        {
            if (!IsOwned(progress, module))
                return false;

            progress.InstallModule(module.slot.ToString(), module.id);
            return true;
        }

        /// <summary>
        /// A won level: pays the reward (LevelDesignConfig's), marks it completed and opens the levels that waited for it.
        /// </summary>
        public static void CompleteLevel(IPlayerProgress progress, LevelCatalog levels, LevelConfig level, int reward)
        {
            if (reward > 0)
                progress.AddCredits(reward);
            progress.MarkLevelCompleted(level.Id);

            if (levels == null)
                return;

            for (int i = 0; i < levels.Levels.Count; i++)
            {
                LevelConfig other = levels.Levels[i];
                if (other != null && other.RequiredLevel == level && !progress.IsLevelUnlocked(other.Id))
                    progress.UnlockLevel(other.Id);
            }
        }
    }
}
