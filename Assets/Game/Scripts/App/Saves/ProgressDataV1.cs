using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Game
{
    /// <summary>
    /// The "progress" section as version 1 wrote it (the old PlayerProgress), kept only to read old saves:
    /// <see cref="ProgressMigration"/> turns it into a <see cref="SaveData"/>. Do not write it any more.
    /// </summary>
    [Serializable]
    public sealed class ProgressDataV1
    {
        public Dictionary<string, int> currencies = new();
        public Dictionary<string, int> modules = new();             // module id -> level, 1 and up when bought
        public Dictionary<string, string> installedModules = new(); // slot -> module id
        public List<string> unlockedLevels = new();
        public List<string> completedLevels = new();
        public Dictionary<string, JToken> extensions = new();

        /// <summary>Replaces nulls (an explicit null or an old file) with empty collections.</summary>
        public void Normalize()
        {
            currencies ??= new();
            modules ??= new();
            installedModules ??= new();
            unlockedLevels ??= new();
            completedLevels ??= new();
            extensions ??= new();
        }
    }
}
