using System.Collections.Generic;

namespace Saving
{
    /// <summary>The save.json envelope: format version, game version and the sections by key.</summary>
    public sealed class SaveFile
    {
        public const int CURRENT_FORMAT_VERSION = 1;

        // No initializer: a file without the field reads as 0, which no build ever wrote
        public int FormatVersion;
        public string GameVersion;
        public Dictionary<string, SaveSection> Sections = new();
    }
}
