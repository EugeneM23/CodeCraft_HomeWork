using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Saving;

namespace Game
{
    /// <summary>
    /// "progress" version 1 (the old PlayerProgress: currencies, modules, installedModules, unlocked and completed
    /// levels) to version 2 (<see cref="SaveData"/>). Starts from a new game of the catalog and lays the old data over
    /// it; nothing is lost:
    /// - currencies "credits" become marks, "samples" samples;
    /// - a module with level > 0 that the catalog knows is owned at that level (1..3); an unknown one goes to
    ///   <see cref="SaveData.legacy"/> (the old menu's own ids);
    /// - an installed module that the catalog knows, is owned and fits the slot goes into the selected tank's loadout;
    ///   any other entry goes to legacy, under its old slot name;
    /// - level ids go to legacy as they are: v1 had no chapters, and the old menu's level ids (e.g. "quarry") must
    ///   not open a chapter past its condition;
    /// - the teams' extensions are dropped: nothing read them.
    /// </summary>
    public static class ProgressMigration
    {
        public const string CREDITS = "credits";
        public const string SAMPLES = "samples";

        public static JToken FromV1(JToken data, GameCatalog catalog)
        {
            ProgressDataV1 old = SaveJson.FromToken<ProgressDataV1>(data) ?? new ProgressDataV1();
            old.Normalize();
            return SaveJson.ToToken(Convert(old, catalog));
        }

        public static SaveData Convert(ProgressDataV1 old, GameCatalog catalog)
        {
            if (old == null)
                throw new ArgumentNullException(nameof(old));

            old.Normalize();
            SaveData result = SaveDataDefaults.Create(catalog);
            result.marks = old.currencies.TryGetValue(CREDITS, out int credits) ? Math.Max(0, credits) : 0;
            result.samples = old.currencies.TryGetValue(SAMPLES, out int samples) ? Math.Max(0, samples) : 0;

            foreach (KeyValuePair<string, int> entry in old.modules)
            {
                string id = entry.Key;
                int level = entry.Value;
                if (string.IsNullOrEmpty(id) || level <= 0)
                    continue;

                ModuleSaveData module = catalog != null && catalog.FindModule(new ModuleId(id)) != null
                    ? result.FindModule(id)
                    : null;
                if (module == null)
                {
                    result.legacy.modules[id] = level;
                    continue;
                }

                module.owned = true;
                module.level = Math.Clamp(Math.Max(level, module.level), 1, ModuleConfig.MAX_LEVEL);
            }

            TankSaveData selected = result.FindTank(result.selectedTank);
            foreach (KeyValuePair<string, string> entry in old.installedModules)
            {
                string slotName = entry.Key;
                string moduleId = entry.Value;
                if (string.IsNullOrEmpty(slotName) || string.IsNullOrEmpty(moduleId))
                    continue;

                ModuleConfig config = catalog != null ? catalog.FindModule(new ModuleId(moduleId)) : null;
                if (selected != null && config != null && ModuleSlotNames.TryParse(slotName, out ModuleSlot slot)
                    && config.Slot == slot && result.FindModule(moduleId) is { owned: true })
                    selected.loadout[ModuleSlotNames.Key(slot)] = moduleId;
                else
                    result.legacy.installed[slotName] = moduleId;
            }

            foreach (string levelId in old.unlockedLevels)
            {
                if (!string.IsNullOrEmpty(levelId) && !result.legacy.unlockedLevels.Contains(levelId))
                    result.legacy.unlockedLevels.Add(levelId);
            }

            foreach (string levelId in old.completedLevels)
            {
                if (!string.IsNullOrEmpty(levelId) && !result.legacy.completedLevels.Contains(levelId))
                    result.legacy.completedLevels.Add(levelId);
            }

            SaveDataDefaults.Reconcile(result, catalog);
            return result;
        }

    }
}
