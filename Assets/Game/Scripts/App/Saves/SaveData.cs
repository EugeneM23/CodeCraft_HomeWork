using System;
using System.Collections.Generic;

namespace Game
{
    /// <summary>
    /// The "progress" save section (GDD §9, §14): money, tanks with their loadouts, modules with their levels,
    /// chapters, tutorial flags and statistics. Only ids, as strings; the numbers live in the catalog. The save
    /// version is the section version (<see cref="ProgressSerializer.VERSION"/>). <see cref="legacy"/> keeps what the
    /// old menu writes under ids the catalog does not know.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public int marks;
        public int samples;
        public string selectedTank;
        public List<TankSaveData> tanks = new();
        public List<ModuleSaveData> modules = new();
        public List<ChapterSaveData> chapters = new();
        public List<string> tutorialFlags = new();
        public StatsSaveData stats = new();
        public LegacyProgressSaveData legacy = new();

        /// <summary>Replaces nulls (an explicit null or an old file) with empty values; negative money with 0.</summary>
        public void Normalize()
        {
            marks = Math.Max(0, marks);
            samples = Math.Max(0, samples);
            selectedTank ??= string.Empty;
            tanks ??= new();
            modules ??= new();
            chapters ??= new();
            tutorialFlags ??= new();
            stats ??= new();
            legacy ??= new();
            legacy.Normalize();

            tanks.RemoveAll(tank => tank == null || string.IsNullOrEmpty(tank.id));
            modules.RemoveAll(module => module == null || string.IsNullOrEmpty(module.id));
            chapters.RemoveAll(chapter => chapter == null || string.IsNullOrEmpty(chapter.id));
            tutorialFlags.RemoveAll(string.IsNullOrEmpty);
            foreach (TankSaveData tank in tanks)
                tank.loadout ??= new();
        }

        public TankSaveData FindTank(string id)
        {
            foreach (TankSaveData tank in tanks)
            {
                if (tank.id == id)
                    return tank;
            }

            return null;
        }

        public ModuleSaveData FindModule(string id)
        {
            foreach (ModuleSaveData module in modules)
            {
                if (module.id == id)
                    return module;
            }

            return null;
        }

        public ChapterSaveData FindChapter(string id)
        {
            foreach (ChapterSaveData chapter in chapters)
            {
                if (chapter.id == id)
                    return chapter;
            }

            return null;
        }
    }
}
