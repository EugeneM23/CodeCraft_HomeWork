using System.Collections.Generic;

namespace Saving
{
    /// <summary>A whole slot as one JSON string: what ExportSlot gives and ImportSlot takes (servers, databases).</summary>
    public sealed class SlotPackage
    {
        public const int CURRENT_FORMAT_VERSION = 1;

        public int FormatVersion;
        public Dictionary<string, string> Files = new();
    }
}
