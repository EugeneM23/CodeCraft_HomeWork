using System.Collections.Generic;
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
    /// Builds the MenuUi prefab (canvas, main menu, level select, hangar, shop, settings, the Yes/No dialog,
    /// the EventSystem and MenuFlow wired to all of them) and the MainMenu scene with it. Panels hang on the
    /// edges by anchors, so the layout holds from 16:9 to 21:9.
    /// </summary>
    public static class MenuUiBuilder
    {
        public const string PREFAB_NAME = "MenuUi";
        public const string SCENE_PATH = "Assets/Game/Scenes/MainMenu.unity";
        public const string SANDBOX_SCENE_PATH = "Assets/Game/Voxels/Scenes/VoxelSandbox.unity";

        private const float BAR_HEIGHT = 72f;
        private const float BACK_WIDTH = 220f;
        private const float HANGAR_BOTTOM_HEIGHT = 240f;
        private const float MARKER = 6f;

        // Shop tab labels by (int)TankSlot
        private static readonly string[] TabKeys =
        {
            "shop.tab_gun", "shop.tab_machine_gun", "shop.tab_hull", "shop.tab_engine", "shop.tab_tracks", "shop.tab_armor"
        };

        // Hangar stat labels by TankStats index
        private static readonly string[] StatLabelKeys =
        {
            "stat.health", "stat.armor", "stat.speed", "stat.damage", "stat.reload_seconds", "stat.fire_rate_per_second"
        };

        [MenuItem("Tools/UI/Build Menu UI")]
        public static GameObject Build()
        {
            LevelCatalog levels = UiDataBuilder.LoadLevelCatalog();
            LevelDesignConfig design = UiDataBuilder.LoadLevelDesign();
            ShopCatalogConfig shop = UiDataBuilder.LoadShopCatalog();
            if (levels == null || design == null || shop == null)
            {
                UiDataBuilder.Build();
                levels = UiDataBuilder.LoadLevelCatalog();
                design = UiDataBuilder.LoadLevelDesign();
                shop = UiDataBuilder.LoadShopCatalog();
            }

            Canvas canvas = UiFactory.Canvas(PREFAB_NAME);
            Transform root = canvas.transform;

            Image background = UiFactory.Panel("Background", root, UiStyle.Background);
            UiFactory.Stretch(background.rectTransform);
            background.raycastTarget = false;

            MainMenuScreen main = CreateMainMenu(root);
            LevelSelectScreen levelSelect = CreateLevelSelect(root);
            HangarScreen hangar = CreateHangar(root);
            ShopScreen shopScreen = CreateShop(root);
            SettingsScreen settings = SettingsScreenBuilder.Create(root);
            ConfirmDialog dialog = ConfirmDialogBuilder.Create(root);
            CreateEventSystem(root);

            MenuFlow flow = canvas.gameObject.AddComponent<MenuFlow>();
            UiFactory.SetField(flow, "_mainMenu", main);
            UiFactory.SetField(flow, "_levelSelect", levelSelect);
            UiFactory.SetField(flow, "_hangar", hangar);
            UiFactory.SetField(flow, "_shop", shopScreen);
            UiFactory.SetField(flow, "_settings", settings);
            UiFactory.SetField(flow, "_dialog", dialog);
            UiFactory.SetField(flow, "_levelCatalog", levels);
            UiFactory.SetField(flow, "_levelDesign", design);
            UiFactory.SetField(flow, "_shopCatalog", shop);

            GameObject prefab = UiFactory.SavePrefab(canvas.gameObject, PREFAB_NAME);
            Debug.Log($"Menu UI built: {AssetDatabase.GetAssetPath(prefab)}");
            return prefab;
        }

        [MenuItem("Tools/UI/Build Main Menu Scene")]
        public static void BuildScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{UiFactory.PREFAB_FOLDER}/{PREFAB_NAME}.prefab");
            if (prefab == null)
                prefab = Build();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UiStyle.Background;

            PrefabUtility.InstantiatePrefab(prefab, scene);

            UiFactory.EnsureFolder(System.IO.Path.GetDirectoryName(SCENE_PATH)!.Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            AddToBuildSettings();
            Debug.Log($"Main menu scene built: {SCENE_PATH}");
        }

        // MainMenu first, the others kept in their order, VoxelSandbox present
        private static void AddToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new() { new EditorBuildSettingsScene(SCENE_PATH, true) };
            bool hasSandbox = false;
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            {
                if (entry.path == SCENE_PATH)
                    continue;
                hasSandbox |= entry.path == SANDBOX_SCENE_PATH;
                scenes.Add(entry);
            }

            if (!hasSandbox)
                scenes.Add(new EditorBuildSettingsScene(SANDBOX_SCENE_PATH, true));

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------- Main menu ----------

        private static MainMenuScreen CreateMainMenu(Transform parent)
        {
            MainMenuScreen screen = UiFactory.Screen<MainMenuScreen>("MainMenuScreen", parent, canGoBack: false);
            Bar(screen.transform, "main.title");

            Image column = UiFactory.Panel("Buttons", screen.transform, UiStyle.Panel);
            float height = 5 * UiStyle.BUTTON_HEIGHT + 4 * UiStyle.SPACING + 2 * UiStyle.PADDING;
            UiFactory.Place(column.rectTransform, new Vector2(0f, 0.5f), new Vector2(UiStyle.PADDING * 2, 0f),
                new Vector2(420f, height));
            UiFactory.Vertical(column.rectTransform, UiStyle.SPACING, (int)UiStyle.PADDING);

            Button play = UiFactory.LocalizedButton("Play", column.transform, "main.play", accent: true);
            Button hangar = UiFactory.LocalizedButton("Hangar", column.transform, "main.hangar");
            Button shop = UiFactory.LocalizedButton("Shop", column.transform, "main.shop");
            Button settings = UiFactory.LocalizedButton("Settings", column.transform, "main.settings");
            Button quit = UiFactory.LocalizedButton("Quit", column.transform, "main.quit");

            TextMeshProUGUI title = UiFactory.LocalizedText("GameTitle", screen.transform, "game.title",
                UiStyle.FONT_TITLE * 2f, TextAlignmentOptions.Center, bold: true);
            UiFactory.Span(title.rectTransform, new Vector2(0.3f, 0.3f), new Vector2(1f, 0.8f), UiStyle.PADDING, UiStyle.PADDING);

            // MainMenuScreen writes the version
            TextMeshProUGUI version = UiFactory.Text("Version", screen.transform, "-", UiStyle.FONT_SMALL,
                TextAlignmentOptions.BottomRight, color: UiStyle.TextMuted);
            UiFactory.Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(-UiStyle.PADDING, UiStyle.PADDING),
                new Vector2(400f, 30f));

            UiFactory.SetField(screen, "_play", play);
            UiFactory.SetField(screen, "_hangar", hangar);
            UiFactory.SetField(screen, "_shop", shop);
            UiFactory.SetField(screen, "_settings", settings);
            UiFactory.SetField(screen, "_quit", quit);
            UiFactory.SetField(screen, "_version", version);
            UiFactory.SetField(screen, "_firstSelected", play);
            return screen;
        }

        // ---------- Level select ----------

        private static LevelSelectScreen CreateLevelSelect(Transform parent)
        {
            LevelSelectScreen screen = UiFactory.Screen<LevelSelectScreen>("LevelSelectScreen", parent);
            Bar(screen.transform, "level.title");
            RectTransform content = Content(screen.transform);
            Button back = BackButton(screen.transform);

            Image listPanel = UiFactory.Panel("Levels", content, UiStyle.Panel);
            UiFactory.Span(listPanel.rectTransform, Vector2.zero, new Vector2(0.38f, 1f), 0, UiStyle.SPACING / 2);
            RectTransform list = UiFactory.ScrollList("List", listPanel.transform, out ScrollRect scroll);
            UiFactory.Stretch((RectTransform)scroll.transform, UiStyle.SPACING, UiStyle.SPACING, UiStyle.SPACING, UiStyle.SPACING);

            // Name, difficulty and reward are written per card; the lock label is static.
            Button card = UiFactory.Button("CardTemplate", list, string.Empty);
            Object.DestroyImmediate(card.transform.Find("Label").gameObject);
            UiFactory.Size(card, 88f);
            Marker(card.transform, LevelSelectScreen.CARD_MARKER);
            ItemText(card.transform, LevelSelectScreen.CARD_NAME, "-", UiStyle.FONT_BODY, true, UiStyle.Text,
                new Vector2(0f, 0.5f), new Vector2(0.65f, 1f), TextAlignmentOptions.BottomLeft);
            ItemText(card.transform, LevelSelectScreen.CARD_DIFFICULTY, "-", UiStyle.FONT_BODY, false, UiStyle.Accent,
                new Vector2(0.65f, 0.5f), Vector2.one, TextAlignmentOptions.BottomRight);
            ItemText(card.transform, LevelSelectScreen.CARD_REWARD, "-", UiStyle.FONT_SMALL, false, UiStyle.TextMuted,
                Vector2.zero, new Vector2(0.65f, 0.5f), TextAlignmentOptions.TopLeft);
            TMP_Text cardLock = ItemText(card.transform, LevelSelectScreen.CARD_LOCK, UiStrings.Get("level.card_locked"),
                UiStyle.FONT_SMALL, true, UiStyle.Bad, new Vector2(0.65f, 0f), new Vector2(1f, 0.5f), TextAlignmentOptions.TopRight);
            UiFactory.Localize(cardLock, "level.card_locked");
            card.gameObject.SetActive(false);

            // The screen writes all detail texts. The description sits right under the title, top-aligned; the
            // spacer, not the description, takes the spare height, so the buttons stay at the bottom.
            Image details = UiFactory.Panel("Details", content, UiStyle.Panel);
            UiFactory.Span(details.rectTransform, new Vector2(0.38f, 0f), Vector2.one, UiStyle.SPACING / 2);
            UiFactory.Vertical(details.rectTransform, UiStyle.SPACING, (int)UiStyle.PADDING);
            TextMeshProUGUI name = UiFactory.Text("Name", details.transform, "-", UiStyle.FONT_HEADER, bold: true);
            UiFactory.Size(name, 44f);
            TextMeshProUGUI description = UiFactory.Text("Description", details.transform, "-", UiStyle.FONT_BODY,
                TextAlignmentOptions.TopLeft, color: UiStyle.TextMuted);
            TextMeshProUGUI goal = UiFactory.Text("Goal", details.transform, "-", UiStyle.FONT_BODY);
            UiFactory.Size(goal, 32f);
            TextMeshProUGUI reward = UiFactory.Text("Reward", details.transform, "-", UiStyle.FONT_BODY);
            UiFactory.Size(reward, 32f);
            TextMeshProUGUI locked = UiFactory.Text("Lock", details.transform, "-", UiStyle.FONT_BODY, color: UiStyle.Bad);
            UiFactory.Size(locked, 32f);
            RectTransform detailsSpacer = UiFactory.Rect("Spacer", details.transform);
            UiFactory.Size(detailsSpacer, -1, -1, -1, 1);

            RectTransform row = UiFactory.Rect("Buttons", details.transform);
            UiFactory.Horizontal(row, UiStyle.SPACING, 0, TextAnchor.MiddleRight);
            UiFactory.Size(row, UiStyle.BUTTON_HEIGHT);
            Button toHangar = UiFactory.LocalizedButton("ToHangar", row, "level.to_hangar");
            UiFactory.Size(toHangar, UiStyle.BUTTON_HEIGHT, 240f);
            Button start = UiFactory.LocalizedButton("Start", row, "level.start", accent: true);
            UiFactory.Size(start, UiStyle.BUTTON_HEIGHT, 280f);

            UiFactory.SetField(screen, "_cardTemplate", card);
            UiFactory.SetField(screen, "_detailName", name);
            UiFactory.SetField(screen, "_detailDescription", description);
            UiFactory.SetField(screen, "_detailGoal", goal);
            UiFactory.SetField(screen, "_detailReward", reward);
            UiFactory.SetField(screen, "_detailLock", locked);
            UiFactory.SetField(screen, "_start", start);
            UiFactory.SetField(screen, "_toHangar", toHangar);
            UiFactory.SetField(screen, "_back", back);
            UiFactory.SetField(screen, "_firstSelected", start);
            return screen;
        }

        // ---------- Hangar ----------

        private static HangarScreen CreateHangar(Transform parent)
        {
            HangarScreen screen = UiFactory.Screen<HangarScreen>("HangarScreen", parent);
            CurrencyBar bar = Bar(screen.transform, "hangar.title");
            RectTransform content = Content(screen.transform);
            Button back = BackButton(screen.transform);

            Button shop = UiFactory.LocalizedButton("Shop", bar.transform, "hangar.shop");
            UiFactory.Place((RectTransform)shop.transform, new Vector2(1f, 0.5f), new Vector2(-UiStyle.PADDING, 0f),
                new Vector2(220f, UiStyle.BUTTON_HEIGHT));

            RectTransform top = UiFactory.Stretch(UiFactory.Rect("Top", content), 0, 0, 0, HANGAR_BOTTOM_HEIGHT + UiStyle.SPACING);

            // Slots
            Image slots = UiFactory.Panel("Slots", top, UiStyle.Panel);
            UiFactory.Span(slots.rectTransform, Vector2.zero, new Vector2(0.24f, 1f), 0, UiStyle.SPACING / 2);
            UiFactory.Vertical(slots.rectTransform, UiStyle.SPACING / 2, (int)UiStyle.SPACING);
            Header(slots.transform, "hangar.slots");
            // Slot rows and module items: HangarScreen writes their texts
            Button slot = UiFactory.Button("SlotTemplate", slots.transform, string.Empty);
            Object.DestroyImmediate(slot.transform.Find("Label").gameObject);
            UiFactory.Size(slot, 64f);
            Marker(slot.transform, HangarScreen.ITEM_MARKER);
            ItemText(slot.transform, HangarScreen.ITEM_NAME, "-", UiStyle.FONT_BODY, true, UiStyle.Text,
                new Vector2(0f, 0.5f), Vector2.one, TextAlignmentOptions.BottomLeft);
            ItemText(slot.transform, HangarScreen.ITEM_VALUE, "-", UiStyle.FONT_SMALL, false, UiStyle.TextMuted,
                Vector2.zero, new Vector2(1f, 0.5f), TextAlignmentOptions.TopLeft);
            slot.gameObject.SetActive(false);

            // Tank placeholder
            Image view = UiFactory.Panel("TankView", top, UiStyle.Panel);
            UiFactory.Span(view.rectTransform, new Vector2(0.24f, 0f), new Vector2(0.74f, 1f), UiStyle.SPACING / 2,
                UiStyle.SPACING / 2);
            TextMeshProUGUI placeholder = UiFactory.LocalizedText("Placeholder", view.transform, "hangar.tank_placeholder",
                UiStyle.FONT_HEADER, TextAlignmentOptions.Center, color: UiStyle.TextMuted);
            UiFactory.Stretch(placeholder.rectTransform, UiStyle.PADDING, UiStyle.PADDING, UiStyle.PADDING, UiStyle.PADDING);

            // Stats
            Image stats = UiFactory.Panel("Stats", top, UiStyle.Panel);
            UiFactory.Span(stats.rectTransform, new Vector2(0.74f, 0f), Vector2.one, UiStyle.SPACING / 2);
            UiFactory.Vertical(stats.rectTransform, UiStyle.SPACING / 2, (int)UiStyle.PADDING);
            Header(stats.transform, "hangar.stats");
            TMP_Text[] values = new TMP_Text[TankStats.COUNT];
            TMP_Text[] deltas = new TMP_Text[TankStats.COUNT];
            for (int i = 0; i < TankStats.COUNT; i++)
            {
                RectTransform row = UiFactory.Rect("Stat_" + i, stats.transform);
                UiFactory.Horizontal(row, UiStyle.SPACING / 2);
                UiFactory.Size(row, 40f);
                TextMeshProUGUI label = UiFactory.LocalizedText("Label", row, StatLabelKeys[i], UiStyle.FONT_BODY,
                    color: UiStyle.TextMuted);
                UiFactory.Size(label, 40f, -1, 1);
                values[i] = UiFactory.Text("Value", row, "0", UiStyle.FONT_BODY, TextAlignmentOptions.Right, bold: true);
                UiFactory.Size(values[i], 40f, 80f);
                deltas[i] = UiFactory.Text("Delta", row, string.Empty, UiStyle.FONT_SMALL, TextAlignmentOptions.Right,
                    color: UiStyle.Good);
                UiFactory.Size(deltas[i], 40f, 70f);
            }

            // Modules of the slot
            Image bottom = UiFactory.Panel("Modules", content, UiStyle.Panel);
            BottomStrip(bottom.rectTransform, HANGAR_BOTTOM_HEIGHT);

            TextMeshProUGUI modulesTitle = UiFactory.Text("Title", bottom.transform, "-", UiStyle.FONT_BODY,
                bold: true, color: UiStyle.TextMuted); // HangarScreen writes it with the slot's name
            UiFactory.Span(modulesTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0.6f, 1f), UiStyle.PADDING, 0,
                UiStyle.SPACING, -UiStyle.SPACING - 36f);

            RectTransform moduleRow = UiFactory.Rect("List", bottom.transform);
            UiFactory.Span(moduleRow, Vector2.zero, new Vector2(0.6f, 1f), UiStyle.PADDING, UiStyle.SPACING,
                UiStyle.SPACING + 44f, UiStyle.PADDING);
            UiFactory.Horizontal(moduleRow, UiStyle.SPACING, 0, TextAnchor.UpperLeft, expandHeight: false);
            Button module = UiFactory.Button("ModuleTemplate", moduleRow, string.Empty);
            Object.DestroyImmediate(module.transform.Find("Label").gameObject);
            UiFactory.Size(module, 96f, 260f);
            Marker(module.transform, HangarScreen.ITEM_MARKER);
            ItemText(module.transform, HangarScreen.ITEM_NAME, "-", UiStyle.FONT_BODY, true, UiStyle.Text,
                new Vector2(0f, 0.5f), Vector2.one, TextAlignmentOptions.BottomLeft);
            ItemText(module.transform, HangarScreen.ITEM_VALUE, "-", UiStyle.FONT_SMALL, false, UiStyle.TextMuted,
                Vector2.zero, new Vector2(1f, 0.5f), TextAlignmentOptions.TopLeft);
            module.gameObject.SetActive(false);

            Image details = UiFactory.Panel("Details", bottom.transform, UiStyle.PanelLight);
            UiFactory.Span(details.rectTransform, new Vector2(0.6f, 0f), Vector2.one, 0, UiStyle.SPACING, UiStyle.SPACING,
                UiStyle.SPACING);
            UiFactory.Vertical(details.rectTransform, UiStyle.SPACING / 2, (int)UiStyle.SPACING);
            // HangarScreen writes the module's texts and the Install / Upgrade labels; To shop is static.
            TextMeshProUGUI moduleName = UiFactory.Text("Name", details.transform, "-", UiStyle.FONT_BODY, bold: true);
            UiFactory.Size(moduleName, 30f);
            TextMeshProUGUI moduleLevel = UiFactory.Text("Level", details.transform, "-", UiStyle.FONT_SMALL,
                color: UiStyle.Accent);
            UiFactory.Size(moduleLevel, 24f);
            TextMeshProUGUI moduleDescription = UiFactory.Text("Description", details.transform, "-",
                UiStyle.FONT_SMALL, color: UiStyle.TextMuted);
            UiFactory.Size(moduleDescription, 40f, -1, -1, 1);
            RectTransform buttons = UiFactory.Rect("Buttons", details.transform);
            UiFactory.Horizontal(buttons, UiStyle.SPACING, 0, TextAnchor.MiddleRight);
            UiFactory.Size(buttons, UiStyle.BUTTON_HEIGHT);
            Button buyInShop = UiFactory.LocalizedButton("BuyInShop", buttons, "hangar.to_shop");
            UiFactory.Size(buyInShop, UiStyle.BUTTON_HEIGHT, 200f);
            Button install = UiFactory.Button("Install", buttons, UiStrings.Get("hangar.install"), accent: true);
            UiFactory.Size(install, UiStyle.BUTTON_HEIGHT, 200f);
            Button upgrade = UiFactory.Button("Upgrade", buttons, "-");
            UiFactory.Size(upgrade, UiStyle.BUTTON_HEIGHT, 240f);
            // Plain Click: HangarScreen plays Purchase or Error by the result of the upgrade

            UiFactory.SetField(screen, "_slotTemplate", slot);
            UiFactory.SetArray(screen, "_statValues", values);
            UiFactory.SetArray(screen, "_statDeltas", deltas);
            UiFactory.SetField(screen, "_modulesTitle", modulesTitle);
            UiFactory.SetField(screen, "_moduleTemplate", module);
            UiFactory.SetField(screen, "_moduleName", moduleName);
            UiFactory.SetField(screen, "_moduleLevel", moduleLevel);
            UiFactory.SetField(screen, "_moduleDescription", moduleDescription);
            UiFactory.SetField(screen, "_install", install);
            UiFactory.SetField(screen, "_installLabel", install.GetComponentInChildren<TMP_Text>());
            UiFactory.SetField(screen, "_upgrade", upgrade);
            UiFactory.SetField(screen, "_upgradeLabel", upgrade.GetComponentInChildren<TMP_Text>());
            UiFactory.SetField(screen, "_buyInShop", buyInShop);
            UiFactory.SetField(screen, "_shop", shop);
            UiFactory.SetField(screen, "_back", back);
            UiFactory.SetField(screen, "_firstSelected", shop);
            return screen;
        }

        // ---------- Shop ----------

        private static ShopScreen CreateShop(Transform parent)
        {
            ShopScreen screen = UiFactory.Screen<ShopScreen>("ShopScreen", parent);
            Bar(screen.transform, "shop.title");
            RectTransform content = Content(screen.transform);
            Button back = BackButton(screen.transform);

            RectTransform tabRow = UiFactory.Rect("Tabs", content);
            TopStrip(tabRow, UiStyle.BUTTON_HEIGHT);
            UiFactory.Horizontal(tabRow, UiStyle.SPACING / 2);
            Button[] tabs = new Button[TabKeys.Length];
            GameObject[] markers = new GameObject[TabKeys.Length];
            for (int i = 0; i < TabKeys.Length; i++)
            {
                tabs[i] = UiFactory.LocalizedButton("Tab_" + (TankSlot)i, tabRow, TabKeys[i]);
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
            Image gridPanel = UiFactory.Panel("Items", content, UiStyle.Panel);
            UiFactory.Span(gridPanel.rectTransform, Vector2.zero, new Vector2(0.66f, 1f), 0, UiStyle.SPACING / 2, below);
            RectTransform grid = UiFactory.Rect("Grid", gridPanel.transform);
            UiFactory.Stretch(grid, UiStyle.PADDING, UiStyle.PADDING, UiStyle.PADDING, UiStyle.PADDING);
            UiFactory.Grid(grid, new Vector2(320f, 230f));

            // ShopScreen writes every card text and both buy labels. The buy buttons click plainly: they only open
            // the question, and MenuFlow plays Purchase or Error once the purchase is done.
            Button card = UiFactory.Button("CardTemplate", grid, string.Empty);
            Object.DestroyImmediate(card.transform.Find("Label").gameObject);
            Marker(card.transform, ShopScreen.CARD_MARKER);
            TextMeshProUGUI cardName = UiFactory.Text(ShopScreen.CARD_NAME, card.transform, "-", UiStyle.FONT_BODY,
                TextAlignmentOptions.TopLeft, bold: true);
            UiFactory.Span(cardName.rectTransform, new Vector2(0f, 1f), Vector2.one, 20f, 12f, 12f, -48f);
            TextMeshProUGUI cardStats = UiFactory.Text(ShopScreen.CARD_STATS, card.transform, "-",
                UiStyle.FONT_SMALL, TextAlignmentOptions.TopLeft, color: UiStyle.TextMuted);
            UiFactory.Stretch(cardStats.rectTransform, 20f, 12f, 52f, 64f);
            TextMeshProUGUI cardPrice = UiFactory.Text(ShopScreen.CARD_PRICE, card.transform, "-", UiStyle.FONT_BODY,
                TextAlignmentOptions.Left, bold: true);
            UiFactory.Place(cardPrice.rectTransform, Vector2.zero, new Vector2(20f, 12f), new Vector2(140f, 44f));
            Button cardBuy = UiFactory.Button(ShopScreen.CARD_BUY, card.transform, UiStrings.Get("shop.card_buy"),
                fontSize: UiStyle.FONT_SMALL);
            UiFactory.Place((RectTransform)cardBuy.transform, new Vector2(1f, 0f), new Vector2(-12f, 12f),
                new Vector2(140f, 44f));
            card.gameObject.SetActive(false);

            Image details = UiFactory.Panel("Details", content, UiStyle.Panel);
            UiFactory.Span(details.rectTransform, new Vector2(0.66f, 0f), Vector2.one, UiStyle.SPACING / 2, 0, below);
            UiFactory.Vertical(details.rectTransform, UiStyle.SPACING, (int)UiStyle.PADDING);
            TextMeshProUGUI name = UiFactory.Text("Name", details.transform, "-", UiStyle.FONT_HEADER, bold: true);
            UiFactory.Size(name, 44f);
            TextMeshProUGUI description = UiFactory.Text("Description", details.transform, "-", UiStyle.FONT_BODY,
                color: UiStyle.TextMuted);
            UiFactory.Size(description, 80f);
            TextMeshProUGUI stats = UiFactory.Text("Stats", details.transform, "-", UiStyle.FONT_BODY);
            UiFactory.Size(stats, 120f, -1, -1, 1);
            TextMeshProUGUI price = UiFactory.Text("Price", details.transform, "-", UiStyle.FONT_BODY, bold: true);
            UiFactory.Size(price, 32f);
            Button buy = UiFactory.Button("Buy", details.transform, UiStrings.Get("shop.buy"), accent: true);

            UiFactory.SetArray(screen, "_tabs", tabs);
            UiFactory.SetArray(screen, "_tabMarkers", markers);
            UiFactory.SetField(screen, "_cardTemplate", card);
            UiFactory.SetField(screen, "_detailName", name);
            UiFactory.SetField(screen, "_detailDescription", description);
            UiFactory.SetField(screen, "_detailStats", stats);
            UiFactory.SetField(screen, "_detailPrice", price);
            UiFactory.SetField(screen, "_detailBuy", buy);
            UiFactory.SetField(screen, "_detailBuyLabel", buy.GetComponentInChildren<TMP_Text>());
            UiFactory.SetField(screen, "_back", back);
            UiFactory.SetField(screen, "_firstSelected", tabs[0]);
            return screen;
        }

        // ---------- EventSystem ----------

        private static void CreateEventSystem(Transform parent)
        {
            GameObject go = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule), typeof(UiEventSystem));
            go.transform.SetParent(parent, false);
        }

        // ---------- Common parts ----------

        /// <summary>The top strip: credits on the left (CurrencyBar writes them), the screen title by key in the middle.</summary>
        private static CurrencyBar Bar(Transform screen, string titleKey)
        {
            Image panel = UiFactory.Panel("CurrencyBar", screen, UiStyle.Panel);
            TopStrip(panel.rectTransform, BAR_HEIGHT);

            TextMeshProUGUI credits = UiFactory.Text("Credits", panel.transform, "-", UiStyle.FONT_BODY,
                TextAlignmentOptions.Left, bold: true, color: UiStyle.Accent);
            UiFactory.Span(credits.rectTransform, Vector2.zero, new Vector2(0.3f, 1f), UiStyle.PADDING);
            TextMeshProUGUI titleText = UiFactory.LocalizedText("Title", panel.transform, titleKey, UiStyle.FONT_HEADER,
                TextAlignmentOptions.Center, bold: true);
            UiFactory.Span(titleText.rectTransform, new Vector2(0.3f, 0f), new Vector2(0.7f, 1f));

            CurrencyBar bar = panel.gameObject.AddComponent<CurrencyBar>();
            UiFactory.SetField(bar, "_title", titleText);
            UiFactory.SetField(bar, "_credits", credits);
            return bar;
        }

        /// <summary>The area between the bar and the Back button, inset from the edges.</summary>
        private static RectTransform Content(Transform screen)
        {
            RectTransform content = UiFactory.Rect("Content", screen);
            return UiFactory.Stretch(content, UiStyle.PADDING, UiStyle.PADDING, BAR_HEIGHT + UiStyle.PADDING,
                UiStyle.PADDING * 2 + UiStyle.BUTTON_HEIGHT);
        }

        private static Button BackButton(Transform screen)
        {
            Button back = UiFactory.LocalizedButton("Back", screen, "common.back");
            UiFactory.Place((RectTransform)back.transform, Vector2.zero, new Vector2(UiStyle.PADDING, UiStyle.PADDING),
                new Vector2(BACK_WIDTH, UiStyle.BUTTON_HEIGHT));
            back.GetComponent<UiButtonSounds>().Click = UiSound.Back;
            return back;
        }

        private static void Header(Transform parent, string key)
        {
            TextMeshProUGUI header = UiFactory.LocalizedText("Header", parent, key, UiStyle.FONT_BODY, bold: true,
                color: UiStyle.TextMuted);
            UiFactory.Size(header, 36f);
        }

        /// <summary>The accent strip on the left edge of a list item: shown on the selected one.</summary>
        private static void Marker(Transform item, string name)
        {
            Image marker = UiFactory.Panel(name, item, UiStyle.Accent);
            RectTransform rect = marker.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(MARKER, 0f);
            marker.raycastTarget = false;
            marker.gameObject.SetActive(false);
        }

        private static TMP_Text ItemText(Transform item, string name, string text, float size, bool bold, Color color,
            Vector2 anchorMin, Vector2 anchorMax, TextAlignmentOptions align)
        {
            TextMeshProUGUI label = UiFactory.Text(name, item, text, size, align, bold, color);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UiFactory.Span(label.rectTransform, anchorMin, anchorMax, 20f, 16f, 4f, 4f);
            return label;
        }

        private static void TopStrip(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
        }

        private static void BottomStrip(RectTransform rect, float height)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
        }
    }
}
