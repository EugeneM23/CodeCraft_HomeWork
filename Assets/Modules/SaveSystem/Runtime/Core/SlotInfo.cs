using System;
using System.Collections.Generic;

namespace Saving
{
    /// <summary>A slot as the slot menu sees it, built from meta.json alone.</summary>
    public sealed class SlotInfo
    {
        public SlotInfo(int slot, SlotMeta meta)
        {
            Slot = slot;
            GameVersion = meta.GameVersion;
            Name = meta.Name ?? string.Empty;
            SavedAtUtc = SlotMeta.ParseTime(meta.SavedAtUtc);
            DateTime created = SlotMeta.ParseTime(meta.CreatedAtUtc);
            CreatedAtUtc = created == DateTime.MinValue ? SavedAtUtc : created;
            Scene = meta.Scene ?? string.Empty;
            PlayTimeSeconds = meta.PlayTimeSeconds;
            Summary = new Dictionary<string, string>(meta.Summary ?? new Dictionary<string, string>());
        }

        public int Slot { get; }
        public string GameVersion { get; }
        public string Name { get; }               // empty when the slot was never named
        public DateTime CreatedAtUtc { get; }     // the first save of the slot
        public DateTime SavedAtUtc { get; }
        public string Scene { get; }              // the active scene at the last save, empty when unknown
        public double PlayTimeSeconds { get; }
        public IReadOnlyDictionary<string, string> Summary { get; }
    }
}
