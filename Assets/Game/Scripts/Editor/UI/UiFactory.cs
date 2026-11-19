using System.Collections.Generic;
using ScreenNavigation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Building blocks for the screen prefab builders: every panel, text, button and control in the
    /// one UiStyle, drawn with grey copies of the SCI-FI UI Pack Pro sprites (UiPackSprites, UiPackNeutralizer). Builders compose these,
    /// wire the screen's serialized fields with SetField and save the prefab with SavePrefab.
    /// </summary>
    public static class UiFactory
    {
        public const string PREFAB_FOLDER = "Assets/Game/Prefabs/UI";
        private const string FONT_FOLDER = "Assets/Game/Fonts/Play";
        private const string FALLBACK_FONT_FOLDER = "Assets/Game/Fonts/Roboto";

        /// <summary>Inner top padding of a window with a title plate on its top edge.</summary>
        public const float WINDOW_TOP = UiStyle.PADDING + UiPackSprites.WINDOW_TITLE_HEIGHT / 2f + 4f;
        private const float WINDOW_TITLE_WIDTH = 440f;

        private static TMP_FontAsset s_regular;
        private static TMP_FontAsset s_bold;
        private static TMP_FontAsset s_fallbackRegular;
        private static TMP_FontAsset s_fallbackBold;

        /// <summary>Play (the pack's font, Latin and Cyrillic), dynamic; Roboto behind it for the signs Play lacks (→, −).</summary>
        public static TMP_FontAsset Regular => s_regular != null ? s_regular
            : s_regular = EnsureFont(UiPackSprites.FONT_PLAY_REGULAR, FONT_FOLDER, "Play_Regular_SDF", FallbackRegular);
        public static TMP_FontAsset Bold => s_bold != null ? s_bold
            : s_bold = EnsureFont(UiPackSprites.FONT_PLAY_BOLD, FONT_FOLDER, "Play_Bold_SDF", FallbackBold);

        public static TMP_FontAsset FallbackRegular => s_fallbackRegular != null ? s_fallbackRegular
            : s_fallbackRegular = EnsureFont($"{FALLBACK_FONT_FOLDER}/Roboto-Regular.ttf", FALLBACK_FONT_FOLDER, "Roboto_Regular_SDF", null);
        public static TMP_FontAsset FallbackBold => s_fallbackBold != null ? s_fallbackBold
            : s_fallbackBold = EnsureFont($"{FALLBACK_FONT_FOLDER}/Roboto-Bold.ttf", FALLBACK_FONT_FOLDER, "Roboto_Bold_SDF", null);

        // ---------- Layout ----------

        public static RectTransform Rect(string name, Transform parent)
        {
            GameObject go = new(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Fills the parent, inset by the margins.</summary>
        public static RectTransform Stretch(RectTransform rect, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        /// <summary>Anchored at one point of the parent (anchor and pivot the same, 0..1), at an offset, of a size.</summary>
        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>Between two anchors of the parent (each 0..1), inset by the margins.</summary>
        public static RectTransform Span(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        public static VerticalLayoutGroup Vertical(RectTransform rect, float spacing = UiStyle.SPACING, int padding = 0,
            TextAnchor align = TextAnchor.UpperLeft, bool expandWidth = true)
        {
            VerticalLayoutGroup group = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.padding = new RectOffset(padding, padding, padding, padding);
            group.childAlignment = align;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = expandWidth;
            group.childForceExpandHeight = false;
            return group;
        }

        public static HorizontalLayoutGroup Horizontal(RectTransform rect, float spacing = UiStyle.SPACING, int padding = 0,
            TextAnchor align = TextAnchor.MiddleLeft, bool expandHeight = true)
        {
            HorizontalLayoutGroup group = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.padding = new RectOffset(padding, padding, padding, padding);
            group.childAlignment = align;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = expandHeight;
            return group;
        }

        public static GridLayoutGroup Grid(RectTransform rect, Vector2 cell, float spacing = UiStyle.SPACING)
        {
            GridLayoutGroup group = rect.gameObject.AddComponent<GridLayoutGroup>();
            group.cellSize = cell;
            group.spacing = new Vector2(spacing, spacing);
            return group;
        }

        /// <summary>
        /// Preferred size for a layout group parent; negative leaves it free. flexibleWidth 1 fills the row.
        /// A fixed height or width is not flexible unless asked: a row's own layout group would otherwise
        /// report flexible 1 (child force expand) and the row would swallow the parent's spare space.
        /// </summary>
        public static LayoutElement Size(Component component, float height = -1, float width = -1, float flexibleWidth = -1,
            float flexibleHeight = -1)
        {
            LayoutElement element = component.GetComponent<LayoutElement>();
            if (element == null)
                element = component.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            element.preferredWidth = width;
            element.flexibleWidth = flexibleWidth >= 0 ? flexibleWidth : width >= 0 ? 0 : -1;
            element.flexibleHeight = flexibleHeight >= 0 ? flexibleHeight : height >= 0 ? 0 : -1;
            return element;
        }

        /// <summary>Grows with its children (for the content of a scroll list or a self-sized panel).</summary>
        public static ContentSizeFitter FitHeight(RectTransform rect)
        {
            ContentSizeFitter fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return fitter;
        }

        /// <summary>Keeps a decoration out of its parent's layout group.</summary>
        public static void IgnoreLayout(Component component)
        {
            LayoutElement element = component.GetComponent<LayoutElement>();
            if (element == null)
                element = component.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
        }

        // ---------- Elements ----------

        /// <summary>A plain coloured rectangle (markers, dim, lines). Framed panels: Frame.</summary>
        public static Image Panel(string name, Transform parent, Color color)
        {
            RectTransform rect = Rect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>An image drawn with a pack sprite (9-sliced when the sprite has borders), tinted (white keeps its colours).</summary>
        public static Image Frame(string name, Transform parent, UiPackSprites.Skin skin, Color? tint = null)
        {
            Image image = Panel(name, parent, Color.white);
            Skin(image, skin, tint);
            return image;
        }

        /// <summary>Puts a pack sprite on an existing image.</summary>
        public static void Skin(Image image, UiPackSprites.Skin skin, Color? tint = null)
        {
            if (image == null)
                return;
            Sprite sprite = skin.Sprite;
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = skin.Ppu;
            // The pack's art is drawn from grey copies: graphite or orange unless the caller tints it.
            image.color = tint ?? UiPackSprites.DefaultTint(skin.Path);
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size = UiStyle.FONT_BODY,
            TextAlignmentOptions align = TextAlignmentOptions.Left, bool bold = false, Color? color = null)
        {
            RectTransform rect = Rect(name, parent);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = bold ? Bold : Regular;
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = color ?? UiStyle.Text;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        /// <summary>A button with a centred label child named "Label". Accent: the screen's main action (orange).</summary>
        public static Button Button(string name, Transform parent, string label, bool accent = false,
            float fontSize = UiStyle.FONT_BODY)
        {
            Image image = Panel(name, parent, Color.white);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ApplyButtonSkin(button, accent ? UiPackSprites.ButtonAccent : UiPackSprites.ButtonSecondary);
            image.gameObject.AddComponent<SelectOnPointerEnter>();
            image.gameObject.AddComponent<UiButtonSounds>();

            TextMeshProUGUI text = Text("Label", image.transform, label, fontSize, TextAlignmentOptions.Center, true,
                UiStyle.Text);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true; // long Russian labels shrink instead of being cut
            text.fontSizeMin = UiStyle.FONT_SMALL - 2f;
            text.fontSizeMax = fontSize;
            Stretch(text.rectTransform, 16, 16, 0, 0);
            Size(image, UiStyle.BUTTON_HEIGHT);
            return button;
        }

        /// <summary>
        /// Draws a selectable with the pack's state sprites: normal, the pressed sprite on hover, selection and
        /// press, the grey one when disabled.
        /// </summary>
        public static void ApplyButtonSkin(Selectable selectable, UiPackSprites.ButtonSkin skin)
        {
            Image image = selectable.targetGraphic as Image;
            Skin(image, skin.AsSkin);
            Sprite pressed = UiPackSprites.Get(skin.Pressed);
            selectable.transition = Selectable.Transition.SpriteSwap;
            selectable.spriteState = new SpriteState
            {
                highlightedSprite = pressed,
                selectedSprite = pressed,
                pressedSprite = pressed,
                disabledSprite = UiPackSprites.Get(skin.Disabled)
            };
        }

        /// <summary>
        /// Draws a selectable with one pack sprite and a colour tint for the states (list rows and tabs, whose
        /// selected look is a separate overlay).
        /// </summary>
        public static void ApplyTintSkin(Selectable selectable, UiPackSprites.Skin skin)
        {
            Skin(selectable.targetGraphic as Image, skin);
            selectable.transition = Selectable.Transition.ColorTint;
            selectable.colors = TintColors();
        }

        /// <summary>The main menu's long button: the pack's menu sprite, label on the left.</summary>
        public static Button LocalizedMenuButton(string name, Transform parent, string key, float height)
        {
            Button button = LocalizedButton(name, parent, key, false, UiStyle.FONT_HEADER);
            ApplyButtonSkin(button, UiPackSprites.ButtonMenu);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            label.alignment = TextAlignmentOptions.Left;
            Stretch(label.rectTransform, 64, 24, 0, 0);
            Size(button, height);
            return button;
        }

        /// <summary>
        /// A tab or list row: the pack's tab sprite, tinted on hover and press. The returned overlay is its
        /// selected look (behind the label, switched off): the screen shows it on the active one.
        /// </summary>
        public static GameObject TabSkin(Button button)
        {
            ApplyTintSkin(button, UiPackSprites.Tab.AsSkin);
            Image selected = Frame("Selected", button.transform, UiPackSprites.TabSelected);
            Stretch(selected.rectTransform);
            selected.raycastTarget = false;
            selected.transform.SetAsFirstSibling();
            IgnoreLayout(selected);
            selected.gameObject.SetActive(false);
            return selected.gameObject;
        }

        /// <summary>
        /// A modal window frame (the pack's popup) with nothing in it; the caller places it and adds the layout
        /// (WindowLayout leaves room for a title plate).
        /// </summary>
        public static Image Window(string name, Transform parent)
        {
            Image window = Frame(name, parent, UiPackSprites.Window);
            return window;
        }

        /// <summary>A vertical layout inside a window: the pack frame's insets, and room for the title plate when titled.</summary>
        public static VerticalLayoutGroup WindowLayout(RectTransform window, float spacing, bool titled,
            TextAnchor align = TextAnchor.UpperLeft)
        {
            const int inset = (int)UiStyle.PADDING + 8;
            VerticalLayoutGroup group = Vertical(window, spacing, inset, align);
            group.padding = new RectOffset(inset, inset, titled ? (int)WINDOW_TOP + 8 : inset, inset);
            return group;
        }

        /// <summary>The pack's hexagonal title plate on the window's top edge with a text (set by the screen).</summary>
        public static TextMeshProUGUI WindowTitle(Image window, string text, float width = WINDOW_TITLE_WIDTH)
        {
            Image plate = Frame("TitlePlate", window.transform, UiPackSprites.WindowTitle);
            plate.raycastTarget = false;
            IgnoreLayout(plate);
            RectTransform rect = plate.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -6f);
            rect.sizeDelta = new Vector2(width, UiPackSprites.WINDOW_TITLE_HEIGHT);

            TextMeshProUGUI title = Text("Title", plate.transform, text, UiStyle.FONT_HEADER, TextAlignmentOptions.Center,
                true, UiStyle.Text);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true;
            title.fontSizeMin = UiStyle.FONT_SMALL;
            title.fontSizeMax = UiStyle.FONT_HEADER;
            Stretch(title.rectTransform, 48, 48, 4, 4);
            return title;
        }

        /// <summary>The title plate with a static text by key.</summary>
        public static TextMeshProUGUI LocalizedWindowTitle(Image window, string key, float width = WINDOW_TITLE_WIDTH)
        {
            TextMeshProUGUI title = WindowTitle(window, UiStrings.Get(key), width);
            Localize(title, key);
            return title;
        }

        /// <summary>The pack's title lines either side of a centred title, outside its layout.</summary>
        public static void TitleLines(RectTransform title, float gap, float length = 300f)
        {
            Image left = Frame("LineLeft", title, UiPackSprites.TitleLineLeft, UiStyle.Accent);
            left.raycastTarget = false;
            left.preserveAspect = true;
            IgnoreLayout(left);
            Place(left.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(length, 26f));
            left.rectTransform.pivot = new Vector2(1f, 0.5f);
            left.rectTransform.anchoredPosition = new Vector2(-gap, 0f);

            Image right = Frame("LineRight", title, UiPackSprites.TitleLineRight, UiStyle.Accent);
            right.raycastTarget = false;
            right.preserveAspect = true;
            IgnoreLayout(right);
            Place(right.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(length, 26f));
            right.rectTransform.pivot = new Vector2(0f, 0.5f);
            right.rectTransform.anchoredPosition = new Vector2(gap, 0f);
        }

        /// <summary>
        /// An icon place: the pack's frame, a white pack icon in it (Symbol, switched off until a view puts a sprite)
        /// and the glyph shown when there is no icon. Sized by the caller.
        /// </summary>
        public static Image IconFrame(string name, Transform parent, float size, bool large, out Image symbol,
            out TextMeshProUGUI glyph)
        {
            Image frame = Frame(name, parent, large ? UiPackSprites.IconFrameLarge : UiPackSprites.IconFrame);
            frame.raycastTarget = false;
            frame.rectTransform.sizeDelta = new Vector2(size, size);

            float inset = Mathf.Round(size * (large ? 0.22f : 0.18f));
            symbol = Panel("Symbol", frame.transform, UiStyle.Text);
            Stretch(symbol.rectTransform, inset, inset, inset, inset);
            symbol.preserveAspect = true;
            symbol.raycastTarget = false;
            symbol.enabled = false;

            glyph = Text("Glyph", frame.transform, "-", Mathf.Round(size * 0.36f), TextAlignmentOptions.Center, true,
                UiStyle.Text);
            glyph.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(glyph.rectTransform);
            return frame;
        }

        // ---------- Localized (static labels from UiStringTable) ----------

        /// <summary>A static label: text from the table by key, kept in the current language by UiLocalizedText.</summary>
        public static TextMeshProUGUI LocalizedText(string name, Transform parent, string key, float size = UiStyle.FONT_BODY,
            TextAlignmentOptions align = TextAlignmentOptions.Left, bool bold = false, Color? color = null)
        {
            TextMeshProUGUI label = Text(name, parent, UiStrings.Get(key), size, align, bold, color);
            Localize(label, key);
            return label;
        }

        /// <summary>A button whose label is a static text by key (not for labels the screen rewrites).</summary>
        public static Button LocalizedButton(string name, Transform parent, string key, bool accent = false,
            float fontSize = UiStyle.FONT_BODY)
        {
            Button button = Button(name, parent, UiStrings.Get(key), accent, fontSize);
            Localize(button.GetComponentInChildren<TMP_Text>(true), key);
            return button;
        }

        /// <summary>A toggle whose label is a static text by key.</summary>
        public static Toggle LocalizedToggle(string name, Transform parent, string key)
        {
            Toggle toggle = Toggle(name, parent, UiStrings.Get(key));
            Localize(toggle.GetComponentInChildren<TMP_Text>(true), key);
            return toggle;
        }

        /// <summary>Attaches UiLocalizedText with the key to an existing text.</summary>
        public static UiLocalizedText Localize(TMP_Text text, string key)
        {
            if (!UiStrings.Has(key))
                Debug.LogWarning($"{nameof(UiFactory)}: no text for key '{key}' ({text.name})", text);

            UiLocalizedText localized = text.gameObject.AddComponent<UiLocalizedText>();
            SetString(localized, "_key", key);
            return localized;
        }

        /// <summary>Colour states of a plain (sprite-less) selectable in the palette.</summary>
        public static ColorBlock Colors(bool accent)
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = accent ? UiStyle.Accent : UiStyle.Button;
            colors.highlightedColor = accent ? Color.Lerp(UiStyle.Accent, Color.white, 0.25f) : UiStyle.ButtonHover;
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = accent ? Color.Lerp(UiStyle.Accent, Color.black, 0.25f) : UiStyle.ButtonPressed;
            colors.disabledColor = UiStyle.Disabled;
            colors.fadeDuration = 0.08f;
            return colors;
        }

        /// <summary>Colour states over a pack sprite: a little dimmed at rest, full on hover, darker pressed.</summary>
        public static ColorBlock TintColors()
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.68f, 0.68f, 0.68f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            return colors;
        }

        /// <summary>
        /// A horizontal bar: the pack's white bar pair, the background darkened and the filled image named "Fill"
        /// (Image.Type.Filled, horizontal) tinted with the fill colour.
        /// </summary>
        public static Image Bar(string name, Transform parent, Color fill, Vector2 size) =>
            Bar(name, parent, fill, size, UiPackSprites.BarBack, UiPackSprites.BarFill, new Color(0f, 0.05f, 0.07f, 0.85f));

        /// <summary>A horizontal bar with the given pack sprites (the fill is never 9-sliced: Filled ignores borders).</summary>
        public static Image Bar(string name, Transform parent, Color fill, Vector2 size, UiPackSprites.Skin backSkin,
            UiPackSprites.Skin fillSkin, Color backTint)
        {
            Image back = Frame(name, parent, backSkin, backTint);
            back.type = Image.Type.Simple;
            back.rectTransform.sizeDelta = size;
            Image bar = Frame("Fill", back.transform, fillSkin, fill);
            Stretch(bar.rectTransform, 2, 2, 2, 2);
            if (bar.sprite == null)
                bar.sprite = WhiteSprite;
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Horizontal;
            bar.fillAmount = 1f;
            bar.raycastTarget = false;
            back.raycastTarget = false;
            return bar;
        }

        public static Slider Slider(string name, Transform parent)
        {
            GameObject go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            SetLayerRecursive(go);
            Slider slider = go.GetComponent<Slider>();
            SkinChild(go.transform, "Background", UiPackSprites.SliderBack);
            SkinChild(go.transform, "Fill Area/Fill", UiPackSprites.SliderFill);
            if (go.transform.Find("Handle Slide Area/Handle") is RectTransform handle)
            {
                Skin(handle.GetComponent<Image>(), UiPackSprites.SliderHandle);
                handle.sizeDelta = new Vector2(17f, 4f); // 34×58 at half size: as tall as the slider plus a little
            }
            slider.colors = TintColors();
            go.AddComponent<SelectOnPointerEnter>();
            Size(slider, 28, -1, 1);
            return slider;
        }

        public static Toggle Toggle(string name, Transform parent, string label)
        {
            GameObject go = DefaultControls.CreateToggle(new DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            SetLayerRecursive(go);
            Toggle toggle = go.GetComponent<Toggle>();
            Transform background = go.transform.Find("Background");
            ((RectTransform)background).sizeDelta = new Vector2(30, 30);
            ((RectTransform)background).anchoredPosition = new Vector2(15, -16); // centred in the 32-high row
            SkinChild(go.transform, "Background", UiPackSprites.CheckOff);
            // The pack's "on" box is the whole box with the tick: it covers the "off" one
            if (go.transform.Find("Background/Checkmark") is RectTransform check)
            {
                Skin(check.GetComponent<Image>(), UiPackSprites.CheckOn);
                Stretch(check);
            }
            Object.DestroyImmediate(go.transform.Find("Label").gameObject);
            TextMeshProUGUI text = Text("Label", go.transform, label);
            Stretch(text.rectTransform, 44, 0, 0, 0);
            text.alignment = TextAlignmentOptions.Left;
            toggle.colors = TintColors();
            go.AddComponent<SelectOnPointerEnter>();
            go.AddComponent<UiButtonSounds>();
            Size(toggle, 32);
            return toggle;
        }

        public static TMP_Dropdown Dropdown(string name, Transform parent)
        {
            GameObject go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            SetLayerRecursive(go);
            TMP_Dropdown dropdown = go.GetComponent<TMP_Dropdown>();
            Skin(go.GetComponent<Image>(), UiPackSprites.Field);
            dropdown.colors = TintColors();
            foreach (TMP_Text text in go.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = Regular;
                text.fontSize = UiStyle.FONT_BODY;
                text.color = UiStyle.Text;
            }
            SkinChild(go.transform, "Template", UiPackSprites.PanelSolid);
            SkinChild(go.transform, "Template/Viewport/Content/Item/Item Background", UiPackSprites.FieldSelected);
            if (go.transform.Find("Template/Viewport/Content/Item") is Transform item && item.TryGetComponent(out Toggle itemToggle))
                itemToggle.colors = TintColors();
            Recolor(go.transform.Find("Template/Viewport/Content/Item/Item Checkmark"), UiStyle.Accent);
            SkinChild(go.transform, "Template/Scrollbar", UiPackSprites.Field);
            SkinChild(go.transform, "Template/Scrollbar/Sliding Area/Handle", UiPackSprites.SliderFill);
            if (go.transform.Find("Arrow") is RectTransform arrow)
            {
                Skin(arrow.GetComponent<Image>(), UiPackSprites.DropdownArrow);
                arrow.sizeDelta = new Vector2(22, 17);
                arrow.anchoredPosition = new Vector2(-22, 0);
            }

            Transform template = go.transform.Find("Template");
            ((RectTransform)template).sizeDelta = new Vector2(0, 300);
            go.AddComponent<SelectOnPointerEnter>();
            go.AddComponent<UiButtonSounds>();
            Size(dropdown, UiStyle.BUTTON_HEIGHT, -1, 1);
            return dropdown;
        }

        /// <summary>A vertical scroll list; children go into the returned content (vertical layout, grows with them).</summary>
        public static RectTransform ScrollList(string name, Transform parent, out ScrollRect scroll, float spacing = UiStyle.SPACING)
        {
            GameObject go = DefaultControls.CreateScrollView(new DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            SetLayerRecursive(go);
            scroll = go.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            go.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            Object.DestroyImmediate(scroll.horizontalScrollbar.gameObject);
            scroll.horizontalScrollbar = null;
            Skin(scroll.verticalScrollbar.GetComponent<Image>(), UiPackSprites.Field);
            Skin(scroll.verticalScrollbar.handleRect.GetComponent<Image>(), UiPackSprites.SliderFill);
            scroll.verticalScrollbar.colors = TintColors();
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

            RectTransform content = scroll.content;
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            Vertical(content, spacing);
            FitHeight(content);
            return content;
        }

        // ---------- Screens and prefabs ----------

        /// <summary>A canvas for screens: Screen Space Overlay, scaled from 1920×1080, half width half height.</summary>
        public static Canvas Canvas(string name, int sortingOrder = 0)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = LayerMask.NameToLayer("UI");
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UiStyle.REFERENCE_WIDTH, UiStyle.REFERENCE_HEIGHT);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>
        /// A full-screen screen object with CanvasGroup and a fade, switched off (only the stack shows it).
        /// Optional dim background behind overlays.
        /// </summary>
        public static T Screen<T>(string name, Transform parent, bool overlay = false, bool canGoBack = true,
            bool dim = false) where T : UIScreen
        {
            RectTransform rect = Stretch(Rect(name, parent));
            rect.gameObject.AddComponent<CanvasGroup>();
            FadeTransition fade = rect.gameObject.AddComponent<FadeTransition>();
            if (dim)
            {
                Image back = Panel("Dim", rect, UiStyle.Dim);
                Stretch(back.rectTransform);
            }

            T screen = rect.gameObject.AddComponent<T>();
            screen.Configure(null, overlay, canGoBack, fade);
            rect.gameObject.SetActive(false);
            return screen;
        }

        /// <summary>Sets a serialized field (private [SerializeField] too) by name: object reference, list or value.</summary>
        public static void SetField(Object target, string field, Object value)
        {
            SerializedObject so = new(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
                throw new System.ArgumentException($"{target.GetType().Name} has no serialized field {field}");
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Sets a serialized string field (private [SerializeField] too) by name.</summary>
        public static void SetString(Object target, string field, string value)
        {
            SerializedObject so = new(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null || property.propertyType != SerializedPropertyType.String)
                throw new System.ArgumentException($"{target.GetType().Name} has no serialized string {field}");
            property.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Sets a serialized array or list of object references by name.</summary>
        public static void SetArray(Object target, string field, params Object[] values)
        {
            SerializedObject so = new(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null || !property.isArray)
                throw new System.ArgumentException($"{target.GetType().Name} has no serialized array {field}");
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Saves the object as a prefab at Assets/Game/Prefabs/UI/&lt;name&gt;.prefab and destroys the scene copy.</summary>
        public static GameObject SavePrefab(GameObject root, string name)
        {
            EnsureFolder(PREFAB_FOLDER);
            string path = $"{PREFAB_FOLDER}/{name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = System.IO.Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        // ---------- Internals ----------

        private static Sprite s_white;

        /// <summary>The built-in UI sprite, needed by Filled images (a sprite-less Image ignores fillAmount).</summary>
        public static Sprite WhiteSprite => s_white != null ? s_white
            : s_white = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        private static void Recolor(Transform transform, Color color)
        {
            if (transform != null && transform.TryGetComponent(out Image image))
                image.color = color;
        }

        private static void SkinChild(Transform root, string path, UiPackSprites.Skin skin)
        {
            Transform child = root.Find(path);
            if (child != null && child.TryGetComponent(out Image image))
                Skin(image, skin);
        }

        private static void SetLayerRecursive(GameObject go)
        {
            int layer = LayerMask.NameToLayer("UI");
            foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }

        /// <summary>
        /// A dynamic TMP font asset from a ttf: glyphs (Cyrillic too) are added on first use. The fallback (Roboto)
        /// draws the signs the font lacks; it is set on an existing asset too.
        /// </summary>
        private static TMP_FontAsset EnsureFont(string ttfPath, string folder, string assetName, TMP_FontAsset fallback)
        {
            string path = $"{folder}/{assetName}.asset";
            TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (asset == null)
            {
                Font font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
                if (font == null)
                    throw new System.InvalidOperationException($"{nameof(UiFactory)}: no font at {ttfPath}");

                EnsureFolder(folder);
                asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                    AtlasPopulationMode.Dynamic, true);
                asset.name = assetName;
                AssetDatabase.CreateAsset(asset, path);
                asset.atlasTextures[0].name = assetName + " Atlas";
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
                asset.material.name = assetName + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            if (fallback != null && (asset.fallbackFontAssetTable == null || !asset.fallbackFontAssetTable.Contains(fallback)))
            {
                asset.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
                asset.fallbackFontAssetTable.Add(fallback);
                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
