using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// The SCI-FI UI Pack Pro pieces the screen builders use (cyan V2 set and the common white icons), by asset
    /// path in one place. Only sprites and fonts are taken from the pack, never its scripts: our assemblies do not
    /// depend on the pack's Assembly-CSharp. A skin is a sprite with the pixels-per-unit multiplier it is drawn
    /// with (the pack is drawn for 2560×1440, our canvas is 1920×1080 and our controls are smaller, so the 9-slice
    /// borders are scaled down).
    /// </summary>
    public static class UiPackSprites
    {
        public const string ROOT = "Assets/SCI-FI UI Pack Pro/";
        private const string CYAN = ROOT + "SCI-FI UI Pack_V2 Pro(Cyan)/Textures/DemoScene_Cyan/";
        private const string BUTTONS = CYAN + "Buttons/";
        private const string OPTIONS = CYAN + "Options/";
        private const string PANELS = CYAN + "PopupsAndPanels/";
        private const string DECOR = CYAN + "Decorations/";
        private const string VICTORY = CYAN + "VictoryAndFailed/";
        public const string WHITE_ICONS = ROOT + "Common/White Icons/Textures/";
        public const string ICONS000 = ROOT + "Common/Icons000/";

        // Fonts (the pack's SDF assets are static Latin atlases: UiFactory makes dynamic ones from these ttf)
        public const string FONT_PLAY_REGULAR = ROOT + "Common/Fonts/Play-Regular.ttf";
        public const string FONT_PLAY_BOLD = ROOT + "Common/Fonts/Play-Bold.ttf";

        // ---------- Frames ----------

        /// <summary>Translucent teal panel with chamfered corners: side panels over the 3D hangar, HUD blocks.</summary>
        public static readonly Skin Panel = new(PANELS + "Panel_CharacterCreation.png", 2f);
        /// <summary>Opaque dark panel with chamfers: overlays that cover what is under them.</summary>
        public static readonly Skin PanelSolid = new(PANELS + "Panel_StoreItem.png", 1.5f);
        /// <summary>Card in a grid (shop modules, tanks).</summary>
        public static readonly Skin Card = new(PANELS + "Panel_StoreHero.png", 1.5f);
        /// <summary>Plain row (rebind rows).</summary>
        public static readonly Skin Row = new(OPTIONS + "Panel_Language.png", 1.5f);
        /// <summary>Modal window frame (settings, pause, results, the Yes/No dialog).</summary>
        public static readonly Skin Window = new(PANELS + "Popup_GameTips.png", 2.5f);
        /// <summary>Hexagonal title plate on a window's top edge (only horizontal borders: keep its height).</summary>
        public static readonly Skin WindowTitle = new(PANELS + "DialogBoxTitle.png", 1.75f);
        public const float WINDOW_TITLE_HEIGHT = 98f / 1.75f;
        /// <summary>The menus' top strip under the currency and the title.</summary>
        public static readonly Skin TopBar = new(PANELS + "Panel_StoreTop.png", 1.33f);
        /// <summary>Plate under a currency value.</summary>
        public static readonly Skin CoinPlate = new(DECOR + "CoinBg.png", 1.6f);
        /// <summary>Striped title backdrop of the main menu.</summary>
        public static readonly Skin LargeTitle = new(DECOR + "LargeTitle01.png", 1f);
        public static readonly Skin TitleLineLeft = new(DECOR + "TitleLinePause_Left.png", 1f);
        public static readonly Skin TitleLineRight = new(DECOR + "TitleLinePause_Right.png", 1f);

        // ---------- Buttons (normal, pressed, disabled) ----------

        public static readonly ButtonSkin ButtonAccent = new(BUTTONS + "Btn_Rectangle01Cyan_n.png",
            BUTTONS + "Btn_Rectangle01Cyan_p.png", BUTTONS + "Btn_Rectangle01Gray_n.png", 2.4f);
        public static readonly ButtonSkin ButtonSecondary = new(BUTTONS + "Btn_Rectangle02Cyan_n.png",
            BUTTONS + "Btn_Rectangle02Cyan_p.png", BUTTONS + "Btn_Rectangle02Gray_n.png", 2.4f);
        /// <summary>The main menu's long buttons, label on the left.</summary>
        public static readonly ButtonSkin ButtonMenu = new(BUTTONS + "Btn_MainMenuCyan_n.png",
            BUTTONS + "Btn_MainMenuCyan_P.png", BUTTONS + "Btn_MainMenuCyan_d.png", 1.85f);
        /// <summary>Tabs and list rows (hangar slots, modules, chapters); the selected look is TabSelected over it.</summary>
        public static readonly ButtonSkin Tab = new(BUTTONS + "Btn_WeaponSelect_n.png",
            BUTTONS + "Btn_WeaponSelect_s.png", BUTTONS + "Btn_WeaponSelect_n.png", 2f);
        public static readonly Skin TabSelected = new(BUTTONS + "Btn_WeaponSelect_s.png", 2f);
        /// <summary>Skill choice card.</summary>
        public static readonly ButtonSkin SkillCard = new(BUTTONS + "Btn_Item1_n.png", BUTTONS + "Btn_Item1_p.png",
            BUTTONS + "Btn_Item1_n.png", 1f);

        // ---------- Icons ----------

        /// <summary>Frame of a large icon (skill card, shop card).</summary>
        public static readonly Skin IconFrameLarge = new(DECOR + "Icon_ItemShowCyan.png", 2.2f);
        /// <summary>Frame of a small icon (HUD skills, list rows).</summary>
        public static readonly Skin IconFrame = new(OPTIONS + "AvatarBg2.png", 2f);
        /// <summary>Plate of a skill's level in the icon's corner.</summary>
        public static readonly Skin LevelPlate = new(VICTORY + "Progress_Lv_Textbg.png", 2.5f);
        /// <summary>Round frame of the special module (HUD).</summary>
        public static readonly Skin SpecialFrame = new(BUTTONS + "Btn_Skill_n.png", 1f);
        public static readonly Skin GateArrow = new(OPTIONS + "DropdownArrow_n.png", 1f);

        // ---------- Bars and controls ----------

        /// <summary>White bar pair, tinted: background dark, fill by state (health, ammo, elite).</summary>
        public static readonly Skin BarBack = new(OPTIONS + "Hp_Bg.png", 1f);
        public static readonly Skin BarFill = new(OPTIONS + "Hp_Progress.png", 1f);
        /// <summary>Cyan progress pair (experience).</summary>
        public static readonly Skin ProgressBack = new(VICTORY + "ProgressLv_bg.png", 1f);
        public static readonly Skin ProgressFill = new(VICTORY + "ProgressLv.png", 1f);
        public static readonly Skin SliderBack = new(OPTIONS + "Slider1_bg.png", 1f);
        public static readonly Skin SliderFill = new(OPTIONS + "Slider1.png", 1f);
        public static readonly Skin SliderHandle = new(OPTIONS + "Slider1_Btn.png", 1f);
        public static readonly Skin CheckOff = new(OPTIONS + "Checkbox_Off.png", 1f);
        public static readonly Skin CheckOn = new(OPTIONS + "Checkbox_On.png", 1f);
        public static readonly Skin Field = new(OPTIONS + "Dropdown_n.png", 1f);
        public static readonly Skin FieldSelected = new(OPTIONS + "Dropdown_s.png", 1f);
        public static readonly Skin DropdownArrow = new(OPTIONS + "DropdownArrow_n.png", 1f);

        private static readonly Dictionary<string, Sprite> Cache = new();

        // Drawn orange (UiStyle.Accent); every other pack sprite is graphite (UiStyle.FrameTint).
        private static readonly HashSet<string> AccentPaths = new()
        {
            ButtonAccent.Normal, ButtonAccent.Pressed, TabSelected.Path, ProgressFill.Path, SliderFill.Path,
            SliderHandle.Path, CheckOn.Path, FieldSelected.Path, LevelPlate.Path, IconFrameLarge.Path, GateArrow.Path
        };

        /// <summary>
        /// The colour a pack sprite is drawn with when the caller gives none: its grey copy tinted orange for the
        /// accents, graphite for the rest (the pack's own cyan is gone, see UiPackNeutralizer).
        /// </summary>
        public static Color DefaultTint(string path) =>
            path != null && AccentPaths.Contains(path) ? UiStyle.Accent : UiStyle.FrameTint;

        /// <summary>The sprite at the path (cached); null with a warning when the pack is not there.</summary>
        public static Sprite Get(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            if (Cache.TryGetValue(path, out Sprite sprite) && sprite != null)
                return sprite;

            // The pack's cyan art is drawn from its grey copy (Image colours tint it: graphite and orange);
            // the white icons are neutral already.
            string drawn = path.StartsWith(WHITE_ICONS) || path.StartsWith(ICONS000) ? path : UiPackNeutralizer.Ensure(path);
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(drawn);
            if (sprite == null)
                Debug.LogWarning($"{nameof(UiPackSprites)}: no sprite at {path}");
            Cache[path] = sprite;
            return sprite;
        }

        /// <summary>A white icon of Common/White Icons/Textures (path below it, e.g. "Icons2/Icon_Shield.png").</summary>
        public static Sprite WhiteIcon(string relativePath) =>
            relativePath.StartsWith("Icons000/") ? Get(ROOT + "Common/" + relativePath) : Get(WHITE_ICONS + relativePath);

        public readonly struct Skin
        {
            public readonly string Path;
            /// <summary>Image.pixelsPerUnitMultiplier: larger draws the 9-slice borders smaller.</summary>
            public readonly float Ppu;

            public Skin(string path, float ppu)
            {
                Path = path;
                Ppu = ppu;
            }

            public Sprite Sprite => Get(Path);
        }

        public readonly struct ButtonSkin
        {
            public readonly string Normal;
            public readonly string Pressed;
            public readonly string Disabled;
            public readonly float Ppu;

            public ButtonSkin(string normal, string pressed, string disabled, float ppu)
            {
                Normal = normal;
                Pressed = pressed;
                Disabled = disabled;
                Ppu = ppu;
            }

            public Skin AsSkin => new(Normal, Ppu);
        }
    }
}
