using UnityEngine;

namespace Game
{
    /// <summary>
    /// The one look of every screen: colours and sizes. Prefab builders read it when they build the
    /// screens; screens read the state colours (good, bad, disabled) at run time. The palette follows the
    /// SCI-FI UI Pack Pro's cyan set (its frames and buttons carry their own colours; these tint texts,
    /// bars and the plain shapes between them).
    /// </summary>
    public static class UiStyle
    {
        public static readonly Color Background = new(0.004f, 0.07f, 0.09f, 1f);
        public static readonly Color Panel = new(0.0f, 0.1f, 0.13f, 0.85f);
        public static readonly Color PanelLight = new(0.02f, 0.17f, 0.21f, 0.92f);
        public static readonly Color Dim = new(0f, 0.02f, 0.04f, 0.72f);
        public static readonly Color Text = new(0.9f, 0.97f, 1f, 1f);
        public static readonly Color TextMuted = new(0.52f, 0.7f, 0.76f, 1f);
        public static readonly Color Accent = new(0f, 0.894f, 1f, 1f);
        public static readonly Color AccentText = new(0.01f, 0.1f, 0.13f, 1f);
        public static readonly Color Button = new(0.03f, 0.2f, 0.25f, 1f);
        public static readonly Color ButtonHover = new(0.05f, 0.3f, 0.36f, 1f);
        public static readonly Color ButtonPressed = new(0.02f, 0.12f, 0.15f, 1f);
        public static readonly Color Disabled = new(0.3f, 0.36f, 0.38f, 0.6f);
        public static readonly Color Good = new(0.3f, 0.95f, 0.55f, 1f);
        public static readonly Color Bad = new(1f, 0.32f, 0.3f, 1f);
        public static readonly Color Health = new(0.3f, 0.92f, 0.5f, 1f);
        /// <summary>Second highlight next to the cyan accent (samples, the armour slot): the pack's purple.</summary>
        public static readonly Color Armor = new(0.66f, 0.5f, 1f, 1f);

        public const float REFERENCE_WIDTH = 1920f;
        public const float REFERENCE_HEIGHT = 1080f;
        public const float BUTTON_HEIGHT = 52f;
        public const float PADDING = 24f;
        public const float SPACING = 12f;
        public const float FONT_TITLE = 44f;
        public const float FONT_HEADER = 28f;
        public const float FONT_BODY = 22f;
        public const float FONT_SMALL = 17f;
    }
}
