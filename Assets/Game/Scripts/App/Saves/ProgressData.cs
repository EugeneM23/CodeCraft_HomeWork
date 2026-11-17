using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Game
{
    /// <summary>
    /// The "progress" save section. Everything is keyed by string ids from the game's configs; no design numbers here.
    /// The section version lives in the save file (ProgressSerializer). <see cref="extensions"/> holds the teams' own
    /// data, each under its key as a JSON value.
    /// </summary>
    [Serializable]
    public sealed class ProgressData
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
