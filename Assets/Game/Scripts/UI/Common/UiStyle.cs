using UnityEngine;

namespace Game
{
    /// <summary>
    /// The one look of every screen: colours and sizes. Prefab builders read it when they build the
    /// screens; screens read the state colours (good, bad, disabled) at run time. Dark graphite with an orange
    /// accent; the SCI-FI UI Pack Pro's frames and buttons are drawn from grey copies tinted with FrameTint
    /// (graphite) or Accent (orange).
    /// </summary>
    public static class UiStyle
    {
        public static readonly Color Background = new(0.075f, 0.072f, 0.07f, 1f);
        public static readonly Color Panel = new(0.11f, 0.105f, 0.1f, 0.88f);
        public static readonly Color PanelLight = new(0.17f, 0.16f, 0.15f, 0.93f);
        public static readonly Color Dim = new(0.02f, 0.018f, 0.016f, 0.72f);
        public static readonly Color Text = new(0.93f, 0.91f, 0.87f, 1f);
        public static readonly Color TextMuted = new(0.62f, 0.6f, 0.57f, 1f);
        public static readonly Color Accent = new(1f, 0.55f, 0.12f, 1f);
        public static readonly Color AccentText = new(0.12f, 0.07f, 0.02f, 1f);
        /// <summary>Tint of the pack's grey frames, panels and secondary buttons: graphite with a warm edge.</summary>
        public static readonly Color FrameTint = new(0.6f, 0.57f, 0.54f, 1f);
        public static readonly Color Button = new(0.2f, 0.19f, 0.18f, 1f);
        public static readonly Color ButtonHover = new(0.3f, 0.26f, 0.21f, 1f);
        public static readonly Color ButtonPressed = new(0.13f, 0.12f, 0.11f, 1f);
        public static readonly Color Disabled = new(0.36f, 0.35f, 0.34f, 0.6f);
        public static readonly Color Good = new(0.4f, 0.88f, 0.42f, 1f);
        public static readonly Color Bad = new(1f, 0.32f, 0.28f, 1f);
        public static readonly Color Health = new(0.38f, 0.86f, 0.4f, 1f);
        /// <summary>Second highlight next to the orange accent (samples, the armour slot): a cool blue-grey.</summary>
        public static readonly Color Armor = new(0.55f, 0.72f, 0.95f, 1f);

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
