using System;
using System.Collections.Generic;

namespace Game
{
    /// <summary>STUB save file of StubHangarSource (progress_m1.json), a cut of GDD §14 until the core's save.</summary>
    [Serializable]
    public sealed class HangarSaveData
    {
        public int version;
        public int marks;
        public int samples;
        public string selectedTank;
        /// <summary>Owned modules; a module not listed is not owned.</summary>
        public List<HangarModuleSave> modules = new();
        /// <summary>The selected tank's loadout (milestone 1 has one tank).</summary>
        public List<HangarLoadoutSave> loadout = new();
        public List<HangarChapterSave> chapters = new();
    }

    [Serializable]
    public sealed class HangarModuleSave
    {
        public string id;
        public int level;
    }

    [Serializable]
    public sealed class HangarLoadoutSave
    {
        /// <summary>ModuleSlot name.</summary>
        public string slot;
        public string module;
    }

    [Serializable]
    public sealed class HangarChapterSave
    {
        public string id;
        public bool unlocked;
        public bool completed;
        public int bestArena;
        public float bestTime;
    }
}
