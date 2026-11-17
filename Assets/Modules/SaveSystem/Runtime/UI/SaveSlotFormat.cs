using System;

namespace Saving.UI
{
    /// <summary>How the menu writes a slot: "Slot 1" or its name, and "24.10.2026 12:30 · 1:05:12 · Cave".</summary>
    public static class SaveSlotFormat
    {
        /// <summary>The slot's name, or "Slot N" counted from 1 for people.</summary>
        public static string Title(int slot, SlotInfo info) =>
            info != null && !string.IsNullOrEmpty(info.Name) ? info.Name : $"Slot {slot + 1}";

        public static string Details(SlotInfo info)
        {
            if (info == null)
                return string.Empty;

            string text = $"{info.SavedAtUtc.ToLocalTime():g}  ·  {PlayTime(info.PlayTimeSeconds)}";
            return string.IsNullOrEmpty(info.Scene) ? text : $"{text}  ·  {info.Scene}";
        }

        /// <summary>Play time as 5:07 or 1:05:12.</summary>
        public static string PlayTime(double seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(Math.Max(0, Math.Floor(seconds)));
            return time.TotalHours >= 1
                ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
                : $"{time.Minutes}:{time.Seconds:00}";
        }
    }
}
