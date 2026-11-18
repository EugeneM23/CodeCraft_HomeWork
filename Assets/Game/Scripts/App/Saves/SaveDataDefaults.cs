using System;

namespace Game
{
    /// <summary>
    /// The new game (GDD §6, §14) and the catalog check of a loaded save. A new game: no marks or samples, every
    /// starting tank owned with its default loadout and the first of them selected, every standard module owned at
    /// level 1, the chapters that need no other one unlocked.
    /// </summary>
    public static class SaveDataDefaults
    {
        /// <summary>A new game for the catalog (an empty save when it is null).</summary>
        public static SaveData Create(GameCatalog catalog)
        {
            var data = new SaveData();
            Reconcile(data, catalog);
            return data;
        }

        /// <summary>
        /// Fits a save to the catalog: adds entries for tanks, modules and chapters the save does not know yet (a
        /// grown catalog), owns starting tanks and standard modules, fills empty loadout slots with the tank's
        /// defaults, keeps owned modules within levels 1..3 (0 when not owned), unlocks chapters whose required chapter
        /// is completed and selects an owned tank. Entries of ids the catalog no longer has are kept as they are.
        /// </summary>
        public static void Reconcile(SaveData data, GameCatalog catalog)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            data.Normalize();
            if (catalog == null)
                return;

            foreach (TankConfig config in catalog.Tanks)
            {
                if (config == null || config.Id.IsNone)
                    continue;

                TankSaveData tank = data.FindTank(config.Id.Value);
                if (tank == null)
                {
                    tank = new TankSaveData { id = config.Id.Value };
                    data.tanks.Add(tank);
                }

                if (config.IsStarting)
                    tank.owned = true;

                foreach (ModuleSlot slot in ModuleSlotNames.All)
                {
                    string key = ModuleSlotNames.Key(slot);
                    if (tank.loadout.TryGetValue(key, out string installed) && !string.IsNullOrEmpty(installed))
                        continue;

                    ModuleId fallback = config.DefaultModule(slot);
                    if (!fallback.IsNone)
                        tank.loadout[key] = fallback.Value;
                }
            }

            foreach (ModuleConfig config in catalog.Modules)
            {
                if (config == null || config.Id.IsNone)
                    continue;

                ModuleSaveData module = data.FindModule(config.Id.Value);
                if (module == null)
                {
                    module = new ModuleSaveData { id = config.Id.Value };
                    data.modules.Add(module);
                }

                if (config.IsStandard)
                    module.owned = true;
            }

            foreach (ModuleSaveData module in data.modules)
                module.level = module.owned ? Math.Clamp(module.level, 1, ModuleConfig.MAX_LEVEL) : 0;

            foreach (ChapterConfig config in catalog.Chapters)
            {
                if (config != null && !config.Id.IsNone && data.FindChapter(config.Id.Value) == null)
                    data.chapters.Add(new ChapterSaveData { id = config.Id.Value });
            }

            foreach (ChapterConfig config in catalog.Chapters)
            {
                if (config == null || config.Id.IsNone)
                    continue;

                ChapterSaveData chapter = data.FindChapter(config.Id.Value);
                if (config.RequiredChapter == null)
                {
                    chapter.unlocked = true;
                    continue;
                }

                ChapterSaveData required = data.FindChapter(config.RequiredChapter.Id.Value);
                if (required != null && required.completed)
                    chapter.unlocked = true;
            }

            TankSaveData selected = data.FindTank(data.selectedTank);
            if (selected == null || !selected.owned)
                data.selectedTank = FirstOwnedTank(data, catalog);
        }

        private static string FirstOwnedTank(SaveData data, GameCatalog catalog)
        {
            foreach (TankConfig config in catalog.Tanks)
            {
                if (config != null && config.IsStarting && data.FindTank(config.Id.Value) is { owned: true })
                    return config.Id.Value;
            }

            foreach (TankSaveData tank in data.tanks)
            {
                if (tank.owned)
                    return tank.id;
            }

            return string.Empty;
        }
    }
}
