using System;
using System.Collections.Generic;

namespace Game
{
    /// <summary>
    /// What the old menu (IPlayerProgress) writes under ids the catalog does not know: its own modules and levels,
    /// slots that do not map onto a ModuleSlot (its "Hull"), level ids that are not chapters. Kept so the old menu
    /// keeps its state until it moves to IMetaProgress; the run and the new screens never read it.
    /// </summary>
    [Serializable]
    public sealed class LegacyProgressSaveData
    {
        public Dictionary<string, int> modules = new();   // module id -> level, 1 and up when bought
        public Dictionary<string, string> installed = new(); // slot -> module id
        public List<string> unlockedLevels = new();
        public List<string> completedLevels = new();

        public void Normalize()
        {
            modules ??= new();
            installed ??= new();
            unlockedLevels ??= new();
            completedLevels ??= new();
        }
    }
}
