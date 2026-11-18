using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the MenuUi prefab (canvas, main menu, hangar, slot assembly, upgrade, shop, chapter select, settings,
    /// the Yes/No dialog, the EventSystem and MenuFlow wired to all of them) and the MainMenu scene with it over the
    /// 3D hangar (HangarStageBuilder). The canvas has no background: the stage's camera draws the hangar behind,
    /// and empty areas take no raycasts, so the stage can turn the tank with the mouse. Panels hang on the edges by
    /// anchors, so the layout holds from 16:9 to 21:9.
    /// </summary>
    public static class MenuUiBuilder
    {
        public const string PREFAB_NAME = "MenuUi";
        public const string SCENE_PATH = "Assets/Game/Scenes/MainMenu.unity";

        private const float BAR_HEIGHT = 72f;
        private const float BACK_WIDTH = 220f;
        private const float MARKER = 6f;
        private const float ICON = 56f;
        private const float ITEM_HEIGHT = 96f;
        private const float ACTION_WIDTH = 230f;
        private const float PRICE_WIDTH = 260f;
        private const float SIDE_WIDTH = 0.26f;
        private const float STATS_LEFT = 0.74f;

        // Stat labels of the stats panel, by HangarTankStats index
        private static readonly string[] StatLabelKeys =
            { "hangar.stat_label.health", "hangar.stat_label.speed", "hangar.stat_label.damage", "hangar.stat_label.reload" };

        // Placeholder tank cards of the shop's Tanks tab: name key, state key
        private static readonly string[,] TankCards =
        {
            { "tank.tiger.name", "hangar.shop.tank_owned" },
            { "tank.hound.name", "hangar.shop.soon" },
            { "tank.fortress.name", "hangar.shop.soon" }
        };

        [MenuItem("Tools/UI/Build Menu UI")]
        public static GameObject Build()
        {
            LevelCatalog levels = UiDataBuilder.LoadLevelCatalog();
            if (levels == null || UiDataBuilder.LoadHangarStub() == null)
            {
                UiDataBuilder.Build();
                levels = UiDataBuilder.LoadLevelCatalog();
            }

            Canvas canvas = UiFactory.Canvas(PREFAB_NAME);
            Transform root = canvas.transform;

            MainMenuScreen main = CreateMainMenu(root);
            HangarScreen hangar = CreateHangar(root);
            SlotAssemblyScreen slotAssembly = CreateSlotAssembly(root);
            UpgradeScreen upgrade = CreateUpgrade(root);
            ShopScreen shop = CreateShop(root);
            ChapterSelectScreen chapters = CreateChapterSelect(root);
            SettingsScreen settings = SettingsScreenBuilder.Create(root);
            ConfirmDialog dialog = ConfirmDialogBuilder.Create(root);
            CreateEventSystem(root);

            MenuFlow flow = canvas.gameObject.AddComponent<MenuFlow>();
            UiFactory.SetField(flow, "_mainMenu", main);
            UiFactory.SetField(flow, "_hangar", hangar);
            UiFactory.SetField(flow, "_slotAssembly", slotAssembly);
            UiFactory.SetField(flow, "_upgrade", upgrade);
            UiFactory.SetField(flow, "_shop", shop);
            UiFactory.SetField(flow, "_chapterSelect", chapters);
            UiFactory.SetField(flow, "_settings", settings);
            UiFactory.SetField(flow, "_dialog", dialog);
            UiFactory.SetField(flow, "_levelCatalog", levels);

            GameObject prefab = UiFactory.SavePrefab(canvas.gameObject, PREFAB_NAME);
            Debug.Log($"Menu UI built: {AssetDatabase.GetAssetPath(prefab)}");
            return prefab;
        }

        [MenuItem("Tools/UI/Build Main Menu Scene")]
        public static void BuildScene()
        {
            // No question in batch mode or when the only open scene is the menu scene itself: it is rebuilt anyway.
            bool onlyMenuOpen = SceneManager.sceneCount == 1 && SceneManager.GetActiveScene().path == SCENE_PATH;
            if (!Application.isBatchMode && !onlyMenuOpen && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{UiFactory.PREFAB_FOLDER}/{PREFAB_NAME}.prefab");
            if (prefab == null)
                prefab = Build();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The 3D hangar brings the MainCamera (with the AudioListener) and the lights
            HangarStage stage = HangarStageBuilder.Create();

            GameObject ui = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            UiFactory.SetField(ui.GetComponent<MenuFlow>(), "_stage", stage);

            UiFactory.EnsureFolder(System.IO.Path.GetDirectoryName(SCENE_PATH)!.Replace('\\', '/'));
            // Saved over the core's MainMenu scene: same file, same GUID (the level catalog refers to it).
            // Build Settings belong to the core team (ProjectSettings): not touched here.
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            Debug.Log($"Main menu scene built: {SCENE_PATH}");
        }

        // ---------- Main menu ----------

        private static MainMenuScreen CreateMainMenu(Transform parent)
        {
            MainMenuScreen screen = UiFactory.Screen<MainMenuScreen>("MainMenuScreen", parent, canGoBack: false);

            // Top left: the tank sits low right in the menu framing
            TextMeshProUGUI title = UiFactory.LocalizedText("GameTitle", screen.transform, "game.title",
                UiStyle.FONT_TITLE * 1.6f, TextAlignmentOptions.TopLeft, bold: true);
            UiFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(UiStyle.PADDING * 2, -UiStyle.PADDING * 2),
                new Vector2(1000f, 90f));

            Image column = UiFactory.Panel("Buttons", screen.transform, UiStyle.Panel);
            float height = 4 * UiStyle.BUTTON_HEIGHT + 3 * UiStyle.SPACING + 2 * UiStyle.PADDING;
            UiFactory.Place(column.rectTransform, new Vector2(0f, 1f), new Vector2(UiStyle.PADDING * 2, -180f),
                new Vector2(420f, height));
            UiFactory.Vertical(column.rectTransform, UiStyle.SPACING, (int)UiStyle.PADDING);
            UiFactory.FitHeight(column.rectTransform); // fits the visible buttons: Continue is hidden without a save

            Button resume = UiFactory.LocalizedButton("Continue", column.transform, "hangar.main.continue", accent: true);
            Button newGame = UiFactory.LocalizedButton("NewGame", column.transform, "hangar.main.new_game");
            Button settings = UiFactory.LocalizedButton("Settings", column.transform, "main.settings");
            Button quit = UiFactory.LocalizedButton("Quit", column.transform, "main.quit");

            // MainMenuScreen writes the version
            TextMeshProUGUI version = UiFactory.Text("Version", screen.transform, "-", UiStyle.FONT_SMALL,
                TextAlignmentOptions.BottomRight, color: UiStyle.TextMuted);
            UiFactory.Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(-UiStyle.PADDING, UiStyle.PADDING),
                new Vector2(400f, 30f));

            UiFactory.SetField(screen, "_continue", resume);
            UiFactory.SetField(screen, "_newGame", newGame);
            UiFactory.SetField(screen, "_settings", settings);
            UiFactory.SetField(screen, "_quit", quit);
            UiFactory.SetField(screen, "_version", version);
            UiFactory.SetField(screen, "_firstSelected", resume);
            return screen;
        }

        // ---------- Hangar ----------

        private static HangarScreen CreateHangar(Transform parent)
        {
            HangarScreen screen = UiFactory.Screen<HangarScreen>("HangarScreen", parent);
            Bar(screen.transform, "hangar.title");
            RectTransform content = Content(screen.transform);
            Button back = BackButton(screen.transform, "common.back", UiSound.Back);

            // Left: the six slots. The centre stays empty for the 3D tank.
            Image slots = UiFactory.Panel("Slots", content, UiStyle.Panel);
            UiFactory.Span(slots.rectTransform, Vector2.zero, new Vector2(SIDE_WIDTH, 1f));
            UiFactory.Vertical(slots.rectTransform, UiStyle.SPACING / 2, (int)UiStyle.SPACING);
            Header(slots.transform, "hangar.slots");
            HangarItemView slot = ListItem(slots.transform, "SlotTemplate", false);

            // Right: the tank's numbers
            Image statsPanel = UiFactory.Panel("Stats", content, UiStyle.Panel);
            UiFactory.Span(statsPanel.rectTransform, new Vector2(STATS_LEFT, 0.4f), Vector2.one);
            UiFactory.Vertical(statsPanel.rectTransform, UiStyle.SPACING / 2, (int)UiStyle.PADDING);
            TextMeshProUGUI tankName = UiFactory.Text("TankName", statsPanel.transform, "-", UiStyle.FONT_HEADER, bold: true);
            UiFactory.Size(tankName, 44f);
            HangarStatsView stats = StatsRows(statsPanel.transform);

            // Bottom right: Shop, Upgrade, To battle
            RectTransform buttons = UiFactory.Rect("Buttons", screen.transform);
            float width = 3 * 240f + 2 * UiStyle.SPACING;
            UiFactory.Place(buttons, new Vector2(1f, 0f), new Vector2(-UiStyle.PADDING, UiStyle.PADDING),
                new Vector2(width, UiStyle.BUTTON_HEIGHT));
            UiFactory.Horizontal(buttons, UiStyle.SPACING, 0, TextAnchor.MiddleRight);
            Button shop = UiFactory.LocalizedButton("Shop", buttons, "hangar.to_shop");
            UiFactory.Size(shop, UiStyle.BUTTON_HEIGHT, 240f);
            Button upgrade = UiFactory.LocalizedButton("Upgrade", buttons, "hangar.to_upgrade");
            UiFactory.Size(upgrade, UiStyle.BUTTON_HEIGHT, 240f);
            Button battle = UiFactory.LocalizedButton("Battle", buttons, "hangar.to_battle", accent: true);
            UiFactory.Size(battle, UiStyle.BUTTON_HEIGHT, 240f);

            UiFactory.SetField(screen, "_slotTemplate", slot);
            UiFactory.SetField(screen, "_stats", stats);
            UiFactory.SetField(screen, "_tankName", tankName);
            UiFactory.SetField(screen, "_shop", shop);
            UiFactory.SetField(screen, "_upgrade", upgrade);
            UiFactory.SetField(screen, "_battle", battle);
            UiFactory.SetField(screen, "_back", back);
            UiFactory.SetField(screen, "_firstSelected", battle);
            return screen;
        }

        // ---------- Slot assembly (overlay over the hangar) ----------

        private static SlotAssemblyScreen CreateSlotAssembly(Transform parent)
        {
            SlotAssemblyScreen screen = UiFactory.Screen<SlotAssemblyScreen>("SlotAssemblyScreen", parent, overlay: true);
            RectTransform content = Content(screen.transform);
            Button close = BackButton(screen.transform, "hangar.close", UiSound.Close);

            // Left, over the hangar's slots: the owned modules of the slot
            Image list = UiFactory.Panel("Modules", content, UiStyle.Background);
            UiFactory.Span(list.rectTransform, Vector2.zero, new Vector2(SIDE_WIDTH, 1f));
            UiFactory.Vertical(list.rectTransform, UiStyle.SPACING / 2, (int)UiStyle.SPACING);
            TextMeshProUGUI title = UiFactory.Text("Title", list.transform, "-", UiStyle.FONT_HEADER, bold: true,
                color: UiStyle.Accent); // the slot's name
            UiFactory.Size(title, 44f);
            HangarItemView item = ListItem(list.transform, "ModuleTemplate", false);

            // Right, over the hangar's numbers: «было → стало», the module and Install
            Image side = UiFactory.Panel("Details", content, UiStyle.Background);
            UiFactory.Span(side.rectTransform, new Vector2(STATS_LEFT, 0f), Vector2.one);
            UiFactory.Vertical(side.rectTransform, UiStyle.SPACING / 2, (int)UiStyle.PADDING);
            Header(side.transform, "hangar.compare");
            HangarStatsView stats = StatsRows(side.transform);
            RectTransform gap = UiFactory.Rect("Gap", side.transform);
            UiFactory.Size(gap, UiStyle.SPACING);
            TextMeshProUGUI name = UiFactory.Text("Name", side.transform, "-", UiStyle.FONT_BODY, bold: true);
            UiFactory.Size(name, 32f);
            TextMeshProUGUI description = UiFactory.Text("Description", side.transform, "-", UiStyle.FONT_SMALL,
                TextAlignmentOptions.TopLeft, color: UiStyle.TextMuted);
            UiFactory.Size(description, 60f, -1, -1, 1);
            Button install = UiFactory.Button("Install", side.transform, UiStrings.Get("hangar.install"), accent: true);
            // Plain Click: the screen plays Install or Error by the result

            UiFactory.SetField(screen, "_title", title);
            UiFactory.SetField(screen, "_itemTemplate", item);
            UiFactory.SetField(screen, "_stats", stats);
            UiFactory.SetField(screen, "_name", name);
            UiFactory.SetField(screen, "_description", description);
            UiFactory.SetField(screen, "_install", install);
            UiFactory.SetField(screen, "_installLabel", install.GetComponentInChildren<TMP_Text>());
            UiFactory.SetField(screen, "_close", close);
            UiFactory.SetField(screen, "_firstSelected", install);
            return screen;
        }

        // ---------- Upgrade (overlay over the hangar) ----------

        private static UpgradeScreen CreateUpgrade(Transform parent)
        {
            UpgradeScreen screen = UiFactory.Screen<UpgradeScreen>("UpgradeScreen", parent, overlay: true);
            RectTransform content = Content(screen.transform);
            Button close = BackButton(screen.transform, "hangar.close", UiSound.Close);

            // Left half, over the slots; the hangar's numbers stay visible on the right
            Image panel = UiFactory.Panel("Upgrades", content, UiStyle.Background);
            UiFactory.Span(panel.rectTransform, Vector2.zero, new Vector2(0.55f, 1f));
            TextMeshProUGUI title = UiFactory.LocalizedText("Title", panel.transform, "hangar.upgrade_title",
                UiStyle.FONT_HEADER, bold: true, color: UiStyle.Accent);
            UiFactory.Span(title.rectTransform, new Vector2(0f, 1f), Vector2.one, UiStyle.PADDING, UiStyle.PADDING,
                UiStyle.SPACING, -UiStyle.SPACING - 44f);
            RectTransform list = UiFactory.ScrollList("List", panel.transform, out ScrollRect scroll, UiStyle.SPACING / 2);
            UiFactory.Stretch((RectTransform)scroll.transform, UiStyle.SPACING, UiStyle.SPACING, 44f + 2 * UiStyle.SPACING,
                UiStyle.SPACING);
            HangarItemView item = ListItem(list, "ModuleTemplate", true);

            UiFactory.SetField(screen, "_itemTemplate", item);
            UiFactory.SetField(screen, "_close", close);
            UiFactory.SetField(screen, "_firstSelected", close);
            return screen;
        }

        // ---------- Shop ----------

        private static ShopScreen CreateShop(Transform parent)
        {
            ShopScreen screen = UiFactory.Screen<ShopScreen>("ShopScreen", parent);
            Bar(screen.transform, "hangar.shop.title");
            RectTransform content = Content(screen.transform);
            Button back = BackButton(screen.transform, "common.back", UiSound.Back);

            RectTransform tabRow = UiFactory.Rect("Tabs", content);
            TopStrip(tabRow, UiStyle.BUTTON_HEIGHT);
            UiFactory.Horizontal(tabRow, UiStyle.SPACING / 2);
            string[] tabKeys = ShopScreen.TabKeys;
            Button[] tabs = new Button[tabKeys.Length];
            GameObject[] markers = new GameObject[tabKeys.Length];
            for (int i = 0; i < tabKeys.Length; i++)
            {
                tabs[i] = UiFactory.LocalizedButton("Tab_" + i, tabRow, tabKeys[i]);
                UiFactory.Size(tabs[i], UiStyle.BUTTON_HEIGHT, -1, 1);
                Image marker = UiFactory.Panel("Marker", tabs[i].transform, UiStyle.Accent);
                RectTransform rect = marker.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(0f, 4f);
                marker.raycastTarget = false;
                markers[i] = marker.gameObject;
            }

            float below = UiStyle.BUTTON_HEIGHT + UiStyle.SPACING;
            Image modules = UiFactory.Panel("Modules", content, UiStyle.Panel);
            UiFactory.Span(modules.rectTransform, Vector2.zero, Vector2.one, 0, 0, below);
            RectTransform grid = UiFactory.Rect("Grid", modules.transform);
            UiFactory.Stretch(grid, UiStyle.PADDING, UiStyle.PADDING, UiStyle.PADDING, UiStyle.PADDING);
            UiFactory.Grid(grid, new Vector2(440f, 330f), UiStyle.SPACING);
            HangarItemView card = ShopCard(grid);

            // The Tanks tab: placeholder cards (milestone 1 has only the Tiger)
            Image tanks = UiFactory.Panel("Tanks", content, UiStyle.Panel);
            UiFactory.Span(tanks.rectTransform, Vector2.zero, Vector2.one, 0, 0, below);
            RectTransform tankGrid = UiFactory.Rect("Grid", tanks.transform);
            UiFactory.Stretch(tankGrid, UiStyle.PADDING, UiStyle.PADDING, UiStyle.PADDING, UiStyle.PADDING);
            UiFactory.Grid(tankGrid, new Vector2(440f, 200f), UiStyle.SPACING);
            for (int i = 0; i < TankCards.GetLength(0); i++)
            {
                Image tank = UiFactory.Panel("Tank_" + i, tankGrid, UiStyle.PanelLight);
                TextMeshProUGUI tankName = UiFactory.LocalizedText("Name", tank.transform, TankCards[i, 0],
                    UiStyle.FONT_HEADER, TextAlignmentOptions.TopLeft, bold: true);
                UiFactory.Stretch(tankName.rectTransform, UiStyle.PADDING, UiStyle.PADDING, UiStyle.PADDING, 80f);
                TextMeshProUGUI state = UiFactory.LocalizedText("State", tank.transform, TankCards[i, 1],
                    UiStyle.FONT_BODY, TextAlignmentOptions.BottomLeft, color: i == 0 ? UiStyle.Good : UiStyle.TextMuted);
                UiFactory.Stretch(state.rectTransform, UiStyle.PADDING, UiStyle.PADDING, 80f, UiStyle.PADDING);
            }
            tanks.gameObject.SetActive(false);

            UiFactory.SetArray(screen, "_tabs", tabs);
            UiFactory.SetArray(screen, "_tabMarkers", markers);
            UiFactory.SetField(screen, "_cardTemplate", card);
            UiFactory.SetField(screen, "_modulesPanel", modules.gameObject);
            UiFactory.SetField(screen, "_tanksPanel", tanks.gameObject);
            UiFactory.SetField(screen, "_back", back);
            UiFactory.SetField(screen, "_firstSelected", tabs[0]);
            return screen;
        }

        // ---------- Chapter select ----------

        private static ChapterSelectScreen CreateChapterSelect(Transform parent)
        {
            ChapterSelectScreen screen = UiFactory.Screen<ChapterSelectScreen>("ChapterSelectScreen", parent);
            Bar(screen.transform, "hangar.chapter.title");
            RectTransform content = Content(screen.transform);
            Button back = BackButton(screen.transform, "common.back", UiSound.Back);

            // Left two thirds: the tank stands low right in the menu framing
            Image listPanel = UiFactory.Panel("Chapters", content, UiStyle.Panel);
            UiFactory.Span(listPanel.rectTransform, Vector2.zero, new Vector2(0.3f, 1f), 0, UiStyle.SPACING / 2);
            UiFactory.Vertical(listPanel.rectTransform, UiStyle.SPACING / 2, (int)UiStyle.SPACING);
            HangarItemView item = ListItem(listPanel.transform, "ChapterTemplate", false, icon: false);

            Image details = UiFactory.Panel("Details", content, UiStyle.Panel);
            UiFactory.Span(details.rectTransform, new Vector2(0.3f, 0f), new Vector2(0.62f, 1f), UiStyle.SPACING / 2);
            UiFactory.Vertical(details.rectTransform, UiStyle.SPACING, (int)UiStyle.PADDING);
            TextMeshProUGUI name = UiFactory.Text("Name", details.transform, "-", UiStyle.FONT_HEADER, bold: true);
            UiFactory.Size(name, 44f);
            TextMeshProUGUI state = UiFactory.Text("State", details.transform, "-", UiStyle.FONT_BODY, bold: true);
            UiFactory.Size(state, 32f);
            TextMeshProUGUI record = UiFactory.Text("Record", details.transform, "-", UiStyle.FONT_BODY);
            UiFactory.Size(record, 64f);
            TextMeshProUGUI reward = UiFactory.Text("Reward", details.transform, "-", UiStyle.FONT_BODY);
            UiFactory.Size(reward, 64f);
            TextMeshProUGUI tank = UiFactory.Text("Tank", details.transform, "-", UiStyle.FONT_BODY, color: UiStyle.TextMuted);
            UiFactory.Size(tank, 32f);
            RectTransform spacer = UiFactory.Rect("Spacer", details.transform);
            UiFactory.Size(spacer, -1, -1, -1, 1);
            Button start = UiFactory.LocalizedButton("Start", details.transform, "hangar.to_battle", accent: true);

            UiFactory.SetField(screen, "_itemTemplate", item);
            UiFactory.SetField(screen, "_name", name);
            UiFactory.SetField(screen, "_state", state);
            UiFactory.SetField(screen, "_record", record);
            UiFactory.SetField(screen, "_reward", reward);
            UiFactory.SetField(screen, "_tank", tank);
            UiFactory.SetField(screen, "_start", start);
            UiFactory.SetField(screen, "_back", back);
            UiFactory.SetField(screen, "_firstSelected", start);
            return screen;
        }

        // ---------- EventSystem ----------

        private static void CreateEventSystem(Transform parent)
        {
            GameObject go = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule), typeof(UiInputModuleBinding));
            go.transform.SetParent(parent, false);
        }

        // ---------- Common parts ----------

        /// <summary>The top strip: marks and samples on the left (CurrencyBar writes them), the screen title by key in the middle.</summary>
        private static CurrencyBar Bar(Transform screen, string titleKey)
        {
            Image panel = UiFactory.Panel("CurrencyBar", screen, UiStyle.Panel);
            TopStrip(panel.rectTransform, BAR_HEIGHT);

            TextMeshProUGUI marks = UiFactory.Text("Marks", panel.transform, "-", UiStyle.FONT_BODY,
                TextAlignmentOptions.Left, bold: true, color: UiStyle.Accent);
            UiFactory.Span(marks.rectTransform, Vector2.zero, new Vector2(0.15f, 1f), UiStyle.PADDING);
            TextMeshProUGUI samples = UiFactory.Text("Samples", panel.transform, "-", UiStyle.FONT_BODY,
                TextAlignmentOptions.Left, bold: true, color: UiStyle.Armor);
            UiFactory.Span(samples.rectTransform, new Vector2(0.15f, 0f), new Vector2(0.3f, 1f), UiStyle.SPACING);
            TextMeshProUGUI titleText = UiFactory.LocalizedText("Title", panel.transform, titleKey, UiStyle.FONT_HEADER,
                TextAlignmentOptions.Center, bold: true);
            UiFactory.Span(titleText.rectTransform, new Vector2(0.3f, 0f), new Vector2(0.7f, 1f));

            CurrencyBar bar = panel.gameObject.AddComponent<CurrencyBar>();
            UiFactory.SetField(bar, "_title", titleText);
            UiFactory.SetField(bar, "_marks", marks);
            UiFactory.SetField(bar, "_samples", samples);
            return bar;
        }

        /// <summary>The area between the bar and the bottom buttons, inset from the edges; takes no raycasts.</summary>
        private static RectTransform Content(Transform screen)
        {
            RectTransform content = UiFactory.Rect("Content", screen);
            return UiFactory.Stretch(content, UiStyle.PADDING, UiStyle.PADDING, BAR_HEIGHT + UiStyle.PADDING,
                UiStyle.PADDING * 2 + UiStyle.BUTTON_HEIGHT);
        }

        private static Button BackButton(Transform screen, string key, UiSound sound)
        {
            Button back = UiFactory.LocalizedButton("Back", screen, key);
            UiFactory.Place((RectTransform)back.transform, Vector2.zero, new Vector2(UiStyle.PADDING, UiStyle.PADDING),
                new Vector2(BACK_WIDTH, UiStyle.BUTTON_HEIGHT));
            back.GetComponent<UiButtonSounds>().Click = sound;
            return back;
        }

        private static void Header(Transform parent, string key)
        {
            TextMeshProUGUI header = UiFactory.LocalizedText("Header", parent, key, UiStyle.FONT_BODY, bold: true,
                color: UiStyle.TextMuted);
            UiFactory.Size(header, 36f);
        }

        /// <summary>Label, value and «→ preview» per stat, in a vertical panel.</summary>
        private static HangarStatsView StatsRows(Transform parent)
        {
            RectTransform rows = UiFactory.Rect("StatRows", parent);
            UiFactory.Vertical(rows, UiStyle.SPACING / 2);
            UiFactory.Size(rows, HangarTankStats.COUNT * 40f + (HangarTankStats.COUNT - 1) * UiStyle.SPACING / 2);
            TMP_Text[] values = new TMP_Text[HangarTankStats.COUNT];
            TMP_Text[] previews = new TMP_Text[HangarTankStats.COUNT];
            for (int i = 0; i < HangarTankStats.COUNT; i++)
            {
                RectTransform row = UiFactory.Rect("Stat_" + i, rows);
                UiFactory.Horizontal(row, UiStyle.SPACING / 2);
                UiFactory.Size(row, 40f);
                TextMeshProUGUI label = UiFactory.LocalizedText("Label", row, StatLabelKeys[i], UiStyle.FONT_BODY,
                    color: UiStyle.TextMuted);
                UiFactory.Size(label, 40f, -1, 1);
                values[i] = UiFactory.Text("Value", row, "0", UiStyle.FONT_BODY, TextAlignmentOptions.Right, bold: true);
                UiFactory.Size(values[i], 40f, 80f);
                previews[i] = UiFactory.Text("Preview", row, string.Empty, UiStyle.FONT_BODY, TextAlignmentOptions.Right,
                    bold: true, color: UiStyle.Good);
                UiFactory.Size(previews[i], 40f, 100f);
            }

            HangarStatsView view = rows.gameObject.AddComponent<HangarStatsView>();
            UiFactory.SetArray(view, "_values", values);
            UiFactory.SetArray(view, "_previews", previews);
            return view;
        }

        /// <summary>
        /// A switched-off list row: marker, slot icon, a small caption on top (Extra), the name (Title), a small
        /// line below (Info); with action, the price and an action button on the right.
        /// </summary>
        private static HangarItemView ListItem(Transform parent, string name, bool action, bool icon = true)
        {
            Button button = UiFactory.Button(name, parent, string.Empty);
            Object.DestroyImmediate(button.transform.Find("Label").gameObject);
            UiFactory.Size(button, ITEM_HEIGHT);
            HangarItemView view = button.gameObject.AddComponent<HangarItemView>();
            UiFactory.SetField(view, "_button", button);
            UiFactory.SetField(view, "_marker", Marker(button.transform));
            if (icon)
                Icon(view, button.transform, new Vector2(0f, 0.5f), new Vector2(16f + MARKER, 0f));

            float left = 16f + MARKER + (icon ? ICON + 16f : 0f);
            float right = action ? ACTION_WIDTH + PRICE_WIDTH + 36f : 16f;
            UiFactory.SetField(view, "_extra", Line(button.transform, "Extra", UiStyle.FONT_SMALL, false, UiStyle.TextMuted,
                0.64f, 1f, left, right, TextAlignmentOptions.BottomLeft));
            UiFactory.SetField(view, "_title", Line(button.transform, "Title", UiStyle.FONT_BODY, true, UiStyle.Text,
                0.3f, 0.7f, left, right, TextAlignmentOptions.Left));
            UiFactory.SetField(view, "_info", Line(button.transform, "Info", UiStyle.FONT_SMALL, false, UiStyle.Accent,
                0f, 0.36f, left, right, TextAlignmentOptions.TopLeft));

            if (action)
            {
                TextMeshProUGUI price = UiFactory.Text("Price", button.transform, "-", UiStyle.FONT_SMALL,
                    TextAlignmentOptions.Right, bold: true);
                UiFactory.Place(price.rectTransform, new Vector2(1f, 0.5f), new Vector2(-(ACTION_WIDTH + 24f), 0f),
                    new Vector2(PRICE_WIDTH, ITEM_HEIGHT - 16f));
                // Plain Click: the screen plays Upgrade or Error by the result
                Button act = UiFactory.Button("Action", button.transform, "-", accent: true, fontSize: UiStyle.FONT_SMALL);
                UiFactory.Place((RectTransform)act.transform, new Vector2(1f, 0.5f), new Vector2(-12f, 0f),
                    new Vector2(ACTION_WIDTH, UiStyle.BUTTON_HEIGHT));
                UiFactory.SetField(view, "_price", price);
                UiFactory.SetField(view, "_action", act);
                UiFactory.SetField(view, "_actionLabel", act.GetComponentInChildren<TMP_Text>());
            }

            button.gameObject.SetActive(false);
            return view;
        }

        /// <summary>
        /// A switched-off shop card (a grid cell): slot icon and name on top, description (Info), what it changes
        /// (Extra), price or lock condition and Buy at the bottom.
        /// </summary>
        private static HangarItemView ShopCard(Transform grid)
        {
            Image card = UiFactory.Panel("CardTemplate", grid, UiStyle.PanelLight);
            HangarItemView view = card.gameObject.AddComponent<HangarItemView>();
            Icon(view, card.transform, new Vector2(0f, 1f), new Vector2(16f, -16f));

            TextMeshProUGUI title = UiFactory.Text("Title", card.transform, "-", UiStyle.FONT_BODY, TextAlignmentOptions.Left,
                bold: true);
            UiFactory.Span(title.rectTransform, new Vector2(0f, 1f), Vector2.one, 16f + ICON + 12f, 16f, 16f, -16f - ICON);
            TextMeshProUGUI info = UiFactory.Text("Info", card.transform, "-", UiStyle.FONT_SMALL,
                TextAlignmentOptions.TopLeft, color: UiStyle.TextMuted);
            UiFactory.Span(info.rectTransform, new Vector2(0f, 1f), Vector2.one, 16f, 16f, 28f + ICON, -28f - ICON - 90f);
            TextMeshProUGUI extra = UiFactory.Text("Extra", card.transform, "-", UiStyle.FONT_SMALL,
                TextAlignmentOptions.TopLeft);
            UiFactory.Stretch(extra.rectTransform, 16f, 16f, 36f + ICON + 90f, 76f);
            TextMeshProUGUI price = UiFactory.Text("Price", card.transform, "-", UiStyle.FONT_SMALL,
                TextAlignmentOptions.Left, bold: true);
            UiFactory.Span(price.rectTransform, Vector2.zero, new Vector2(1f, 0f), 16f, ACTION_WIDTH * 0.8f + 28f, -64f, 12f);
            // Plain Click: Buy only opens the question; MenuFlow plays Purchase or Error after it
            Button buy = UiFactory.Button("Action", card.transform, "-", accent: true, fontSize: UiStyle.FONT_SMALL);
            UiFactory.Place((RectTransform)buy.transform, new Vector2(1f, 0f), new Vector2(-16f, 16f),
                new Vector2(ACTION_WIDTH * 0.8f, UiStyle.BUTTON_HEIGHT));

            UiFactory.SetField(view, "_title", title);
            UiFactory.SetField(view, "_info", info);
            UiFactory.SetField(view, "_extra", extra);
            UiFactory.SetField(view, "_price", price);
            UiFactory.SetField(view, "_action", buy);
            UiFactory.SetField(view, "_actionLabel", buy.GetComponentInChildren<TMP_Text>());
            card.gameObject.SetActive(false);
            return view;
        }

        /// <summary>The slot icon: a coloured square (HangarItemView colours it) with a short glyph.</summary>
        private static void Icon(HangarItemView view, Transform item, Vector2 anchor, Vector2 position)
        {
            Image icon = UiFactory.Panel("Icon", item, Color.white);
            icon.raycastTarget = false;
            UiFactory.Place(icon.rectTransform, anchor, position, new Vector2(ICON, ICON));
            TextMeshProUGUI glyph = UiFactory.Text("Glyph", icon.transform, "-", UiStyle.FONT_BODY,
                TextAlignmentOptions.Center, bold: true, color: UiStyle.AccentText);
            UiFactory.Stretch(glyph.rectTransform);
            UiFactory.SetField(view, "_icon", icon);
            UiFactory.SetField(view, "_glyph", glyph);
        }

        private static TMP_Text Line(Transform item, string name, float size, bool bold, Color color, float yMin, float yMax,
            float left, float right, TextAlignmentOptions align)
        {
            TextMeshProUGUI label = UiFactory.Text(name, item, string.Empty, size, align, bold, color);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UiFactory.Span(label.rectTransform, new Vector2(0f, yMin), new Vector2(1f, yMax), left, right, 2f, 2f);
            return label;
        }

        /// <summary>The accent strip on the left edge of a list item: shown on the selected one.</summary>
        private static GameObject Marker(Transform item)
        {
            Image marker = UiFactory.Panel("Marker", item, UiStyle.Accent);
            RectTransform rect = marker.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(MARKER, 0f);
            marker.raycastTarget = false;
            marker.gameObject.SetActive(false);
            return marker.gameObject;
        }

        private static void TopStrip(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
        }
    }
}
