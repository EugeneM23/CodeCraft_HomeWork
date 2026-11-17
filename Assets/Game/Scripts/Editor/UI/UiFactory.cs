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
    /// one UiStyle. Builders compose these, wire the screen's serialized fields with SetField and save
    /// the prefab with SavePrefab.
    /// </summary>
    public static class UiFactory
    {
        public const string PREFAB_FOLDER = "Assets/Game/Prefabs/UI";
        private const string FONT_FOLDER = "Assets/Game/Fonts/Roboto";

        private static TMP_FontAsset s_regular;
        private static TMP_FontAsset s_bold;

        public static TMP_FontAsset Regular => s_regular != null ? s_regular : s_regular = EnsureFont("Roboto-Regular", "Roboto_Regular_SDF");
        public static TMP_FontAsset Bold => s_bold != null ? s_bold : s_bold = EnsureFont("Roboto-Bold", "Roboto_Bold_SDF");

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

        // ---------- Elements ----------

        public static Image Panel(string name, Transform parent, Color color)
        {
            RectTransform rect = Rect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
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

        /// <summary>A button with a centred label child named "Label". Accent: the screen's main action.</summary>
        public static Button Button(string name, Transform parent, string label, bool accent = false,
            float fontSize = UiStyle.FONT_BODY)
        {
            Image image = Panel(name, parent, Color.white);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = Colors(accent);
            image.gameObject.AddComponent<SelectOnPointerEnter>();
            image.gameObject.AddComponent<UiButtonSounds>();

            TextMeshProUGUI text = Text("Label", image.transform, label, fontSize, TextAlignmentOptions.Center, accent,
                accent ? UiStyle.AccentText : UiStyle.Text);
            Stretch(text.rectTransform, 12, 12, 0, 0);
            Size(image, UiStyle.BUTTON_HEIGHT);
            return button;
        }

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

        /// <summary>A horizontal bar: background and a filled image named "Fill" (Image.Type.Filled, horizontal).</summary>
        public static Image Bar(string name, Transform parent, Color fill, Vector2 size)
        {
            Image back = Panel(name, parent, new Color(0f, 0f, 0f, 0.55f));
            back.rectTransform.sizeDelta = size;
            Image bar = Panel("Fill", back.transform, fill);
            Stretch(bar.rectTransform, 2, 2, 2, 2);
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
            Recolor(go.transform.Find("Background"), UiStyle.ButtonPressed);
            Recolor(go.transform.Find("Fill Area/Fill"), UiStyle.Accent);
            Recolor(go.transform.Find("Handle Slide Area/Handle"), UiStyle.Text);
            slider.colors = Colors(false);
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
            ((RectTransform)background).sizeDelta = new Vector2(28, 28);
            Recolor(background, UiStyle.ButtonPressed);
            Recolor(go.transform.Find("Background/Checkmark"), UiStyle.Accent);
            Object.DestroyImmediate(go.transform.Find("Label").gameObject);
            TextMeshProUGUI text = Text("Label", go.transform, label);
            Stretch(text.rectTransform, 40, 0, 0, 0);
            text.alignment = TextAlignmentOptions.Left;
            toggle.colors = Colors(false);
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
            dropdown.colors = Colors(false);
            go.GetComponent<Image>().color = Color.white;
            foreach (TMP_Text text in go.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = Regular;
                text.fontSize = UiStyle.FONT_BODY;
                text.color = UiStyle.Text;
            }
            Recolor(go.transform.Find("Template"), UiStyle.PanelLight);
            Recolor(go.transform.Find("Template/Viewport/Content/Item/Item Background"), UiStyle.Button);
            // Without sprites the arrow is a white square: a small muted diamond instead.
            if (go.transform.Find("Arrow") is RectTransform arrow)
            {
                arrow.sizeDelta = new Vector2(12, 12);
                arrow.anchoredPosition = new Vector2(-20, 0);
                arrow.localRotation = Quaternion.Euler(0, 0, 45);
                Recolor(arrow, UiStyle.TextMuted);
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
            Recolor(scroll.verticalScrollbar.transform, UiStyle.ButtonPressed);
            Recolor(scroll.verticalScrollbar.handleRect, UiStyle.ButtonHover);
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

        private static void SetLayerRecursive(GameObject go)
        {
            int layer = LayerMask.NameToLayer("UI");
            foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }

        /// <summary>A dynamic TMP font asset from a Roboto ttf: glyphs (Cyrillic too) are added on first use.</summary>
        private static TMP_FontAsset EnsureFont(string ttf, string assetName)
        {
            string path = $"{FONT_FOLDER}/{assetName}.asset";
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null)
                return existing;

            Font font = AssetDatabase.LoadAssetAtPath<Font>($"{FONT_FOLDER}/{ttf}.ttf");
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            asset.name = assetName;
            AssetDatabase.CreateAsset(asset, path);
            asset.atlasTextures[0].name = assetName + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
