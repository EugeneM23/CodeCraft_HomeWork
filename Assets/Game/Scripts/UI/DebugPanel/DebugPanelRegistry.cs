using System;
using System.Collections.Generic;

namespace Game
{
    /// <summary>
    /// What the debug panel (F2) shows: any feature adds its buttons, toggles and sliders under a category, and the
    /// panel draws one tab per category. Entries live for the whole game (they are registered once, from
    /// <see cref="DebugCommands"/> or a feature's own static setup) and look the level's objects up when pressed, so
    /// they work in whatever level is loaded. A press that throws is caught and logged by the panel.
    /// <code>
    /// DebugPanelRegistry.AddButton("Танк", "Полное здоровье", HealTank);
    /// DebugPanelRegistry.AddToggle("Танк", "Бессмертие", () => God, v => God = v);
    /// DebugPanelRegistry.AddSlider("Мир", "Время", 0f, 2f, () => Time.timeScale, v => Time.timeScale = v);
    /// </code>
    /// </summary>
    public static class DebugPanelRegistry
    {
        private static readonly List<DebugEntry> s_entries = new();
        private static readonly List<string> s_categories = new();
        private static int s_sequence;

        /// <summary>An entry was added or removed: the open panel rebuilds its tab.</summary>
        public static event Action Changed;

        /// <summary>
        /// Every frame while the panel object lives (open or closed), unscaled: for cheats that must hold a state
        /// (god mode, the hidden HUD). Keep handlers cheap and return at once when off.
        /// </summary>
        public static event Action Ticked;

        public static IReadOnlyList<DebugEntry> Entries => s_entries;

        internal static void Tick() => Ticked?.Invoke();

        /// <summary>The categories in the order they first appeared (the panel's tabs).</summary>
        public static IReadOnlyList<string> Categories => s_categories;

        public static DebugEntry AddButton(string category, string label, Action pressed, int order = 0) =>
            AddButtons(category, null, new[] { label }, _ => pressed(), order);

        /// <summary>A caption and a row of buttons; <paramref name="pressed"/> gets the index of the button.</summary>
        public static DebugEntry AddButtons(string category, Func<string> label, string[] buttons, Action<int> pressed,
            int order = 0, Func<int, string> buttonText = null) =>
            Add(new DebugEntry
            {
                Kind = DebugEntryKind.Buttons, Category = category, Label = label, Buttons = buttons, Pressed = pressed,
                ButtonText = buttonText, Order = order
            });

        public static DebugEntry AddToggle(string category, string label, Func<bool> get, Action<bool> set, int order = 0) =>
            Add(new DebugEntry
            {
                Kind = DebugEntryKind.Toggle, Category = category, Label = () => label, GetToggle = get, SetToggle = set,
                Order = order
            });

        /// <summary>A slider from min to max; the caption shows the current value.</summary>
        public static DebugEntry AddSlider(string category, string label, float min, float max, Func<float> get,
            Action<float> set, int order = 0) =>
            Add(new DebugEntry
            {
                Kind = DebugEntryKind.Slider, Category = category, Label = () => label, GetValue = get, SetValue = set,
                Min = min, Max = max, Order = order
            });

        /// <summary>A line of live text («Уровень 3, опыт 12/34»).</summary>
        public static DebugEntry AddInfo(string category, Func<string> text, int order = 0) =>
            Add(new DebugEntry { Kind = DebugEntryKind.Info, Category = category, Label = text, Order = order });

        public static DebugEntry Add(DebugEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (string.IsNullOrEmpty(entry.Category))
                throw new ArgumentException("A debug entry needs a category", nameof(entry));

            entry.Sequence = s_sequence++;
            s_entries.Add(entry);
            if (!s_categories.Contains(entry.Category))
                s_categories.Add(entry.Category);
            Changed?.Invoke();
            return entry;
        }

        public static void Remove(DebugEntry entry)
        {
            if (entry == null || !s_entries.Remove(entry))
                return;

            bool used = false;
            for (int i = 0; i < s_entries.Count && !used; i++)
                used = s_entries[i].Category == entry.Category;
            if (!used)
                s_categories.Remove(entry.Category);
            Changed?.Invoke();
        }

        /// <summary>The visible entries of a category, by Order then registration.</summary>
        public static void Collect(string category, List<DebugEntry> into)
        {
            into.Clear();
            for (int i = 0; i < s_entries.Count; i++)
            {
                DebugEntry entry = s_entries[i];
                if (entry.Category == category)
                    into.Add(entry);
            }

            into.Sort(static (a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Sequence.CompareTo(b.Sequence));
        }

        /// <summary>Drops everything (tests; the game registers once and keeps them).</summary>
        public static void Clear()
        {
            s_entries.Clear();
            s_categories.Clear();
            Changed?.Invoke();
        }
    }
}
