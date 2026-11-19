using System;

namespace Game
{
    /// <summary>
    /// One line of the debug panel (<see cref="DebugPanelRegistry"/>): a row of buttons, a toggle, a slider or an
    /// info line, under a category (the panel's tab). Texts given as functions are read again while the panel is
    /// open (a few times a second), so a line can show live state («Рикошет 3/5»).
    /// </summary>
    public sealed class DebugEntry
    {
        public DebugEntryKind Kind { get; internal set; }
        public string Category { get; internal set; }

        /// <summary>The row's caption; null — no caption (a row of buttons only).</summary>
        public Func<string> Label { get; internal set; }

        /// <summary>Buttons: their captions (fixed) and what a press of button i does.</summary>
        public string[] Buttons { get; internal set; }
        public Action<int> Pressed { get; internal set; }

        /// <summary>Buttons: a live caption of button i (overrides <see cref="Buttons"/>); null or an empty string hides it.</summary>
        public Func<int, string> ButtonText { get; internal set; }

        public Func<bool> GetToggle { get; internal set; }
        public Action<bool> SetToggle { get; internal set; }

        public Func<float> GetValue { get; internal set; }
        public Action<float> SetValue { get; internal set; }
        public float Min { get; internal set; }
        public float Max { get; internal set; }

        /// <summary>Shown only while true; null — always.</summary>
        public Func<bool> Visible { get; set; }

        /// <summary>Order inside the category: lower first, then in the order of registration.</summary>
        public int Order { get; internal set; }

        internal int Sequence { get; set; }

        public bool IsVisible
        {
            get
            {
                if (Visible == null)
                    return true;
                try
                {
                    return Visible();
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public string ReadLabel() => Label != null ? Safe(Label) : null;

        public string ReadButton(int index)
        {
            if (ButtonText != null)
            {
                try
                {
                    return ButtonText(index);
                }
                catch (Exception)
                {
                    return null;
                }
            }

            return Buttons != null && index < Buttons.Length ? Buttons[index] : null;
        }

        private static string Safe(Func<string> text)
        {
            try
            {
                return text();
            }
            catch (Exception e)
            {
                return "! " + e.GetType().Name;
            }
        }
    }
}
