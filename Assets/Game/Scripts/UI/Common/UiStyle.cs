using UnityEngine;

namespace Game
{
    /// <summary>
    /// The one look of every screen: colours and sizes. Prefab builders read it when they build the
    /// screens; screens read the state colours (good, bad, disabled) at run time.
    /// </summary>
    public static class UiStyle
    {
        public static readonly Color Background = new(0.06f, 0.07f, 0.09f, 1f);
        public static readonly Color Panel = new(0.08f, 0.094f, 0.11f, 0.8f);
        public static readonly Color PanelLight = new(0.16f, 0.18f, 0.21f, 0.9f);
        public static readonly Color Dim = new(0f, 0f, 0f, 0.6f);
        public static readonly Color Text = new(0.92f, 0.93f, 0.95f, 1f);
        public static readonly Color TextMuted = new(0.6f, 0.63f, 0.68f, 1f);
        public static readonly Color Accent = new(0.95f, 0.6f, 0.18f, 1f);
        public static readonly Color AccentText = new(0.1f, 0.07f, 0.03f, 1f);
        public static readonly Color Button = new(0.2f, 0.22f, 0.26f, 1f);
        public static readonly Color ButtonHover = new(0.3f, 0.32f, 0.37f, 1f);
        public static readonly Color ButtonPressed = new(0.14f, 0.15f, 0.18f, 1f);
        public static readonly Color Disabled = new(0.35f, 0.36f, 0.38f, 0.6f);
        public static readonly Color Good = new(0.4f, 0.85f, 0.4f, 1f);
        public static readonly Color Bad = new(0.95f, 0.3f, 0.25f, 1f);
        public static readonly Color Health = new(0.35f, 0.8f, 0.35f, 1f);
        public static readonly Color Armor = new(0.35f, 0.6f, 0.95f, 1f);

        public const float REFERENCE_WIDTH = 1920f;
        public const float REFERENCE_HEIGHT = 1080f;
        public const float BUTTON_HEIGHT = 48f;
        public const float PADDING = 24f;
        public const float SPACING = 12f;
        public const float FONT_TITLE = 44f;
        public const float FONT_HEADER = 28f;
        public const float FONT_BODY = 22f;
        public const float FONT_SMALL = 17f;
    }
}
