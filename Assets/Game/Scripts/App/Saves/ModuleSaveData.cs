using System;

namespace Game
{
    /// <summary>A module in the save: owned or not and its level, 1..3 when owned, 0 otherwise (GDD §14).</summary>
    [Serializable]
    public sealed class ModuleSaveData
    {
        public string id;
        public bool owned;
        public int level;
    }
}
