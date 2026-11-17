using System;
using System.Collections.Generic;
using System.Globalization;

namespace Saving
{
    /// <summary>
    /// meta.json of a slot: what the slot menu shows without reading the save itself. Name, creation time and scene
    /// came later: an older meta.json has none of them.
    /// </summary>
    public sealed class SlotMeta
    {
        public const string TIME_FORMAT = "yyyy-MM-dd'T'HH:mm:ss'Z'";

        public int FormatVersion;
        public string GameVersion;
        public string Name;
        public string CreatedAtUtc;
        public string SavedAtUtc;
        public string Scene;
        public double PlayTimeSeconds;
        public Dictionary<string, string> Summary = new();

        public static string FormatTime(DateTime utc) =>
            utc.ToUniversalTime().ToString(TIME_FORMAT, CultureInfo.InvariantCulture);

        public static DateTime ParseTime(string text) =>
            DateTime.TryParseExact(text, TIME_FORMAT, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime utc)
                ? utc
                : DateTime.MinValue;
    }
}
