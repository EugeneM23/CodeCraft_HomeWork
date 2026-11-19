using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the debug panel prefab (Resources/UI/DebugPanel, loaded by DebugPanel at game start): its own canvas
    /// above every screen, a window on the right with the title, the FPS line, the category tabs and a scroll list,
    /// and the row templates the panel clones for the registry's entries (a caption with buttons, a toggle, a slider
    /// with its caption, a line of text). The FPS corner line sits top-left, outside the window.
    /// </summary>
    public static class DebugPanelBuilder
    {
        public const string PREFAB_PATH = UiFactory.PREFAB_FOLDER + "/Resources/" + DebugPanel.RESOURCE_PATH + ".prefab";
        private const string ZOMBIE_PREFAB = "Assets/Game/Prefabs/Characters/Zombie.prefab";
        private const int SORTING_ORDER = 200;
        private const float WINDOW_WIDTH = 820f;
        private const float ROW_HEIGHT = 40f;
        private const float LABEL_WIDTH = 300f;
        private const float TAB_WIDTH = 170f;
        private const float TAB_HEIGHT = 40f;

        [MenuItem("Tools/UI/Build Debug Panel")]
        public static void Build()
        {
            Canvas canvas = UiFactory.Canvas("DebugPanel", SORTING_ORDER);
            Transform root = canvas.transform;
            DebugPanel panel = canvas.gameObject.AddComponent<DebugPanel>();

            // ---------- Window ----------
            Image window = UiFactory.Window("Window", root);
            RectTransform windowRect = window.rectTransform;
            windowRect.anchorMin = new Vector2(1f, 0f);
            windowRect.anchorMax = new Vector2(1f, 1f);
            windowRect.pivot = new Vector2(1f, 0.5f);
            windowRect.sizeDelta = new Vector2(WINDOW_WIDTH, -80f);
            windowRect.anchoredPosition = new Vector2(-24f, 0f);
            UiFactory.WindowLayout(windowRect, 8f, true);
            UiFactory.WindowTitle(window, "Отладка · F2");

            TextMeshProUGUI status = UiFactory.Text("Status", windowRect, "60 FPS", UiStyle.FONT_SMALL,
                TextAlignmentOptions.Left, false, UiStyle.TextMuted);
            status.textWrappingMode = TextWrappingModes.NoWrap;
            UiFactory.Size(status, 24f);

            RectTransform tabs = UiFactory.Rect("Tabs", windowRect);
            GridLayoutGroup grid = UiFactory.Grid(tabs, new Vector2(TAB_WIDTH, TAB_HEIGHT), 6f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            UiFactory.FitHeight(tabs);

            RectTransform content = UiFactory.ScrollList("List", windowRect, out ScrollRect scroll, 6f);
            UiFactory.Size(scroll, -1, -1, 1, 1);
            content.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 12, 4, 4);

            // ---------- Templates (switched off by the panel; cloned per entry) ----------
            RectTransform templates = UiFactory.Rect("Templates", windowRect);
            UiFactory.IgnoreLayout(templates);

            Button tab = UiFactory.Button("Tab", tabs, "Вкладка", false, UiStyle.FONT_SMALL);
            UiFactory.TabSkin(tab);

            RectTransform buttonsRow = UiFactory.Rect("ButtonsRow", templates);
            UiFactory.Horizontal(buttonsRow, 8f);
            UiFactory.Size(buttonsRow, ROW_HEIGHT);
            TextMeshProUGUI buttonsLabel = UiFactory.Text("Label", buttonsRow, "Навык", UiStyle.FONT_SMALL);
            buttonsLabel.textWrappingMode = TextWrappingModes.NoWrap;
            UiFactory.Size(buttonsLabel, ROW_HEIGHT, LABEL_WIDTH);
            RectTransform buttons = UiFactory.Rect("Buttons", buttonsRow);
            HorizontalLayoutGroup buttonsLayout = UiFactory.Horizontal(buttons, 6f);
            buttonsLayout.childForceExpandWidth = true;
            UiFactory.Size(buttons, ROW_HEIGHT, -1, 1);
            Button button = UiFactory.Button("Button", buttons, "+1", false, UiStyle.FONT_SMALL);
            UiFactory.Size(button, ROW_HEIGHT, -1, 1);

            Toggle toggle = UiFactory.Toggle("Toggle", templates, "Переключатель");
            toggle.GetComponentInChildren<TMP_Text>(true).fontSize = UiStyle.FONT_SMALL + 2f;

            RectTransform sliderRow = UiFactory.Rect("SliderRow", templates);
            UiFactory.Horizontal(sliderRow, 8f, 0, TextAnchor.MiddleLeft, false);
            UiFactory.Size(sliderRow, ROW_HEIGHT);
            TextMeshProUGUI sliderLabel = UiFactory.Text("Label", sliderRow, "Значение", UiStyle.FONT_SMALL);
            sliderLabel.textWrappingMode = TextWrappingModes.NoWrap;
            UiFactory.Size(sliderLabel, ROW_HEIGHT, LABEL_WIDTH);
            UiFactory.Slider("Slider", sliderRow);

            TextMeshProUGUI info = UiFactory.Text("Info", templates, "Строка", UiStyle.FONT_SMALL, TextAlignmentOptions.Left,
                false, UiStyle.TextMuted);
            info.textWrappingMode = TextWrappingModes.Normal;
            info.overflowMode = TextOverflowModes.Overflow; // the list's layout takes its preferred height

            // ---------- FPS corner ----------
            TextMeshProUGUI corner = UiFactory.Text("FpsCorner", root, "60 FPS", UiStyle.FONT_SMALL,
                TextAlignmentOptions.TopLeft, true, UiStyle.Accent);
            corner.textWrappingMode = TextWrappingModes.NoWrap;
            UiFactory.Place(corner.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(520f, 28f));

            UiFactory.SetField(panel, "_window", window.gameObject);
            UiFactory.SetField(panel, "_status", status);
            UiFactory.SetField(panel, "_tabs", tabs);
            UiFactory.SetField(panel, "_tabTemplate", tab);
            UiFactory.SetField(panel, "_content", content);
            UiFactory.SetField(panel, "_scroll", scroll);
            UiFactory.SetField(panel, "_buttonsTemplate", buttonsRow);
            UiFactory.SetField(panel, "_toggleTemplate", toggle);
            UiFactory.SetField(panel, "_sliderTemplate", sliderRow);
            UiFactory.SetField(panel, "_infoTemplate", info);
            UiFactory.SetField(panel, "_fpsCorner", corner);
            UiFactory.SetField(panel, "_zombiePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(ZOMBIE_PREFAB));

            string folder = System.IO.Path.GetDirectoryName(PREFAB_PATH)!.Replace('\\', '/');
            UiFactory.EnsureFolder(folder);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, PREFAB_PATH);
            Object.DestroyImmediate(canvas.gameObject);
            Debug.Log($"{nameof(DebugPanelBuilder)}: saved {AssetDatabase.GetAssetPath(prefab)}");
        }
    }
}
