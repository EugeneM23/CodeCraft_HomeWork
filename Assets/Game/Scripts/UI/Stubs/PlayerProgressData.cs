using System;
using System.Collections.Generic;

namespace Game
{
    /// <summary>What JsonPlayerProgress keeps in its file: plain data for JsonUtility. Stub detail.</summary>
    [Serializable]
    public sealed class PlayerProgressData
    {
        public int credits;
        public List<OwnedModule> modules = new();
        public List<InstalledModule> installed = new();
        public List<string> unlockedLevels = new();
        public List<string> completedLevels = new();

        [Serializable]
        public sealed class OwnedModule
        {
            public string id;
            public int level;
        }

        [Serializable]
        public sealed class InstalledModule
        {
            public string slot;
            public string id;
        }
    }
}
