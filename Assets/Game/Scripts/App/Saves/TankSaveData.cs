using System;
using System.Collections.Generic;

namespace Game
{
    /// <summary>A tank in the save: owned or not and its loadout, ModuleSlot name -> module id (GDD §14).</summary>
    [Serializable]
    public sealed class TankSaveData
    {
        public string id;
        public bool owned;
        public Dictionary<string, string> loadout = new();
    }
}
