using System;
using System.Collections.Generic;

namespace Saving.Objects
{
    /// <summary>A component of a Saveable that is not saved the Auto way: its key, mode and picked members.</summary>
    [Serializable]
    internal sealed class ComponentRule
    {
        public string Key;
        public SaveMode Mode;
        public List<string> Members = new();
    }
}
