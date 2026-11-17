using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the BattleUi prefab: a canvas with the HUD, pause, results, the Yes/No dialog and the settings
    /// screen, SandboxBattleStats, the EventSystem, and BattleFlow on the root wired to all of them. The
    /// HUD's often-changing parts (bars, crosshair, counters) and the debug block sit under nested canvases
    /// of their own, so their changes do not rebuild the rest.
    /// </summary>
    public static class BattleUiBuilder
    {
        private const string PREFAB_NAME = "BattleUi";
        private const int SORTING_ORDER = 10;
        private const float BAR_WIDTH = 240f;
        private const float BAR_HEIGHT = 18f;
        private const float ROW_HEIGHT = 28f;
        private const float LABEL_WIDTH = 140f;
        private const float VALUE_WIDTH = 110f;

        [MenuItem("Tools/UI/Build Battle UI")]
        public static void Build()
        {
            Canvas canvas = UiFactory.Canvas(PREFAB_NAME, SORTING_ORDER);
            Transform root = canvas.transform;

            HudScreen hud = CreateHud(root);
            PauseScreen pause = CreatePause(root);
            BattleResultsScreen results = CreateResults(root);
            SettingsScreen settings = SettingsScreenBuilder.Create(root);
            ConfirmDialog dialog = ConfirmDialogBuilder.Create(root);

            LevelCatalog catalog = UiDataBuilder.LoadLevelCatalog();
            LevelDesignConfig design = UiDataBuilder.LoadLevelDesign();
            if (catalog == null || design == null)
                Debug.LogWarning($"{nameof(BattleUiBuilder)}: no {UiDataBuilder.LEVEL_CATALOG_PATH} or " +
                                 $"{UiDataBuilder.LEVEL_DESIGN_PATH} yet (Build UI data first); " +
                                 "BattleUi is saved without them and plays the scene as a level-less sandbox.");

            SandboxBattleStats stats = canvas.gameObject.AddComponent<SandboxBattleStats>();
            UiFactory.SetField(stats, "_levelCatalog", catalog);
            UiFactory.SetField(stats, "_levelDesign", design);

            CreateEventSystem(root);

            BattleFlow flow = canvas.gameObject.AddComponent<BattleFlow>();
            UiFactory.SetField(flow, "_hud", hud);
            UiFactory.SetField(flow, "_pause", pause);
            UiFactory.SetField(flow, "_results", results);
            UiFactory.SetField(flow, "_confirmDialog", dialog);
            UiFactory.SetField(flow, "_settings", settings);
            UiFactory.SetField(flow, "_levelCatalog", catalog);
            UiFactory.SetField(flow, "_levelDesign", design);
            UiFactory.SetField(flow, "_battleStats", stats);

            GameObject prefab = UiFactory.SavePrefab(canvas.gameObject, PREFAB_NAME);
            Selection.activeObject = prefab;
            Debug.Log($"{nameof(BattleUiBuilder)}: saved {AssetDatabase.GetAssetPath(prefab)}");
        }

        // ---------- HUD ----------

        private static HudScreen CreateHud(Transform parent)
        {
            HudScreen hud = UiFactory.Screen<HudScreen>("Hud", parent, overlay: false, canGoBack: false);
            RectTransform screen = (RectTransform)hud.transform;

            // Static part: the hints panel, on the screen's own canvas.
            CanvasGroup hints = CreateHints(screen, out TMP_Text hintsText);

            // Often-changing part: bars, crosshair, counters.
            RectTransform dynamic = NestedCanvas("Dynamic", screen);
            TankHudView tank = dynamic.gameObject.AddComponent<TankHudView>();
            CreateHullBars(dynamic, tank);
            CreateWeaponBars(dynamic, tank);
            CreateCrosshair(dynamic, tank);
            CreateCounters(dynamic, out TMP_Text kills, out TMP_Text time);

            // Debug block (F3), hidden by default, on a canvas of its own: it changes 4 times a second.
            RectTransform debug = NestedCanvas("Debug", screen);
            TMP_Text[] lines = CreateDebug(debug);
            debug.gameObject.SetActive(false);

            UiFactory.SetField(hud, "_tank", tank);
            UiFactory.SetField(hud, "_killsText", kills);
            UiFactory.SetField(hud, "_timeText", time);
            UiFactory.SetField(hud, "_hints", hints);
            UiFactory.SetField(hud, "_hintsText", hintsText);
            UiFactory.SetField(hud, "_debug", debug.gameObject);
            UiFactory.SetField(hud, "_fpsText", lines[0]);
            UiFactory.SetField(hud, "_voxelsText", lines[1]);
            UiFactory.SetField(hud, "_debrisText", lines[2]);
            UiFactory.SetField(hud, "_remeshText", lines[3]);
            UiFactory.SetField(hud, "_smokeText", lines[4]);
            return hud;
        }

        private static RectTransform NestedCanvas(string name, Transform parent)
        {
            RectTransform rect = UiFactory.Stretch(UiFactory.Rect(name, parent));
            rect.gameObject.AddComponent<Canvas>();
            return rect;
        }

        private static CanvasGroup CreateHints(RectTransform parent, out TMP_Text text)
        {
            Image panel = UiFactory.Panel("Hints", parent, UiStyle.Panel);
            panel.raycastTarget = false;
            UiFactory.Place(panel.rectTransform, Vector2.zero, new Vector2(UiStyle.PADDING, UiStyle.PADDING + 110f),
                new Vector2(360, 170));
            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            text = UiFactory.Text("Text", panel.transform, "-", UiStyle.FONT_SMALL, color: UiStyle.TextMuted); // HudScreen writes it
            text.textWrappingMode = TextWrappingModes.NoWrap;
            UiFactory.Stretch(text.rectTransform, 14, 14, 10, 10);
            return group;
        }

        private static void CreateHullBars(RectTransform parent, TankHudView tank)
        {
            RectTransform group = UiFactory.Rect("Hull", parent);
            UiFactory.Place(group, Vector2.zero, new Vector2(UiStyle.PADDING, UiStyle.PADDING),
                new Vector2(LABEL_WIDTH + BAR_WIDTH + VALUE_WIDTH + 2 * UiStyle.SPACING, 2 * ROW_HEIGHT + 6));
            UiFactory.Vertical(group, 6);

            Row(group, "Health", "hud.health", UiStyle.Health, out Image healthFill, out TMP_Text healthText);
            Row(group, "Armor", "hud.armor", UiStyle.Armor, out Image armorFill, out TMP_Text armorText);

            UiFactory.SetField(tank, "_healthFill", healthFill);
            UiFactory.SetField(tank, "_healthText", healthText);
            UiFactory.SetField(tank, "_armorFill", armorFill);
            UiFactory.SetField(tank, "_armorText", armorText);
        }

        private static void CreateWeaponBars(RectTransform parent, TankHudView tank)
        {
            RectTransform group = UiFactory.Rect("Weapons", parent);
            UiFactory.Place(group, new Vector2(1, 0), new Vector2(-UiStyle.PADDING, UiStyle.PADDING),
                new Vector2(LABEL_WIDTH + BAR_WIDTH + VALUE_WIDTH + 2 * UiStyle.SPACING, 3 * ROW_HEIGHT + 12));
            UiFactory.Vertical(group, 6, 0, TextAnchor.LowerRight);

            RectTransform gun = Row(group, "Gun", "hud.gun", UiStyle.Accent, out Image gunFill, out TMP_Text gunText);
            RectTransform mg = Row(group, "MachineGun", "hud.machine_gun", UiStyle.Text, out Image mgFill, out TMP_Text mgText);

            RectTransform boost = UiFactory.Rect("Boost", group);
            UiFactory.Horizontal(boost, UiStyle.SPACING, 0, TextAnchor.MiddleLeft, expandHeight: false);
            UiFactory.Size(boost, ROW_HEIGHT);
            TextMeshProUGUI boostLabel = UiFactory.LocalizedText("Label", boost, "hud.boost", UiStyle.FONT_SMALL, bold: true,
                color: UiStyle.TextMuted);
            UiFactory.Size(boostLabel, ROW_HEIGHT, LABEL_WIDTH);
            Image lamp = UiFactory.Panel("Lamp", boost, UiStyle.Disabled);
            lamp.raycastTarget = false;
            UiFactory.Size(lamp, BAR_HEIGHT, 48);

            UiFactory.SetField(tank, "_gunGroup", gun.gameObject);
            UiFactory.SetField(tank, "_gunFill", gunFill);
            UiFactory.SetField(tank, "_gunText", gunText);
            UiFactory.SetField(tank, "_mgGroup", mg.gameObject);
            UiFactory.SetField(tank, "_mgFill", mgFill);
            UiFactory.SetField(tank, "_mgText", mgText);
            UiFactory.SetField(tank, "_boostGroup", boost.gameObject);
            UiFactory.SetField(tank, "_boostLamp", lamp);
        }

        /// <summary>A HUD row: label (by key), bar, value. Returns the row.</summary>
        private static RectTransform Row(Transform parent, string name, string labelKey, Color color, out Image fill,
            out TMP_Text value)
        {
            RectTransform row = UiFactory.Rect(name, parent);
            UiFactory.Horizontal(row, UiStyle.SPACING, 0, TextAnchor.MiddleLeft, expandHeight: false);
            UiFactory.Size(row, ROW_HEIGHT);

            TextMeshProUGUI title = UiFactory.LocalizedText("Label", row, labelKey, UiStyle.FONT_SMALL, bold: true,
                color: UiStyle.TextMuted);
            UiFactory.Size(title, ROW_HEIGHT, LABEL_WIDTH);

            fill = UiFactory.Bar("Bar", row, color, new Vector2(BAR_WIDTH, BAR_HEIGHT));
            UiFactory.Size(fill.transform.parent.GetComponent<Image>(), BAR_HEIGHT, BAR_WIDTH);

            TextMeshProUGUI text = UiFactory.Text("Value", row, "0/0", UiStyle.FONT_SMALL);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            UiFactory.Size(text, ROW_HEIGHT, VALUE_WIDTH);
            value = text;
            return row;
        }

        private static void CreateCrosshair(RectTransform parent, TankHudView tank)
        {
            RectTransform cross = UiFactory.Rect("Crosshair", parent);
            UiFactory.Place(cross, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72, 72));

            Image arc = UiFactory.Panel("ReloadArc", cross, new Color(UiStyle.Accent.r, UiStyle.Accent.g, UiStyle.Accent.b, 0.35f));
            UiFactory.Stretch(arc.rectTransform);
            arc.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            arc.type = Image.Type.Filled;
            arc.fillMethod = Image.FillMethod.Radial360;
            arc.fillOrigin = (int)Image.Origin360.Top;
            arc.fillClockwise = true;
            arc.fillAmount = 0f;
            arc.raycastTarget = false;
            arc.enabled = false;

            const float length = 14f;
            const float thickness = 2f;
            const float gap = 6f;
            Graphic[] lines =
            {
                Line(cross, "Up", new Vector2(0, gap + length / 2), new Vector2(thickness, length)),
                Line(cross, "Down", new Vector2(0, -gap - length / 2), new Vector2(thickness, length)),
                Line(cross, "Left", new Vector2(-gap - length / 2, 0), new Vector2(length, thickness)),
                Line(cross, "Right", new Vector2(gap + length / 2, 0), new Vector2(length, thickness))
            };

            UiFactory.SetField(tank, "_reloadArc", arc);
            UiFactory.SetArray(tank, "_crosshair", lines);
        }

        private static Graphic Line(Transform parent, string name, Vector2 position, Vector2 size)
        {
            Image line = UiFactory.Panel(name, parent, UiStyle.Text);
            line.sprite = UiFactory.WhiteSprite;
            line.raycastTarget = false;
            UiFactory.Place(line.rectTransform, new Vector2(0.5f, 0.5f), position, size);
            return line;
        }

        private static void CreateCounters(RectTransform parent, out TMP_Text kills, out TMP_Text time)
        {
            RectTransform row = UiFactory.Rect("Counters", parent);
            UiFactory.Place(row, new Vector2(0, 1), new Vector2(UiStyle.PADDING, -UiStyle.PADDING), new Vector2(560, 40));
            UiFactory.Horizontal(row, UiStyle.PADDING, 0, TextAnchor.MiddleLeft);

            // HudScreen writes both
            TextMeshProUGUI killsText = UiFactory.Text("Kills", row, "-", UiStyle.FONT_HEADER, bold: true);
            killsText.textWrappingMode = TextWrappingModes.NoWrap;
            UiFactory.Size(killsText, 40, 220);
            TextMeshProUGUI timeText = UiFactory.Text("Time", row, "-", UiStyle.FONT_HEADER);
            timeText.textWrappingMode = TextWrappingModes.NoWrap;
            UiFactory.Size(timeText, 40, 240);
            kills = killsText;
            time = timeText;
        }

        private static TMP_Text[] CreateDebug(RectTransform parent)
        {
            Image panel = UiFactory.Panel("Panel", parent, UiStyle.Panel);
            panel.raycastTarget = false;
            UiFactory.Place(panel.rectTransform, Vector2.one, new Vector2(-UiStyle.PADDING, -UiStyle.PADDING),
                new Vector2(560, 5 * 24 + 24));
            UiFactory.Vertical(panel.rectTransform, 2, 12);

            string[] names = { "Fps", "Voxels", "Debris", "Remesh", "Smoke" };
            TMP_Text[] lines = new TMP_Text[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                TextMeshProUGUI line = UiFactory.Text(names[i], panel.transform, names[i], UiStyle.FONT_SMALL);
                line.textWrappingMode = TextWrappingModes.NoWrap;
                UiFactory.Size(line, 22);
                lines[i] = line;
            }
            return lines;
        }

        // ---------- Pause ----------

        private static PauseScreen CreatePause(Transform parent)
        {
            PauseScreen pause = UiFactory.Screen<PauseScreen>("Pause", parent, overlay: true, canGoBack: true, dim: true);

            Image box = UiFactory.Panel("Box", pause.transform, UiStyle.PanelLight);
            UiFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440, 460));
            UiFactory.Vertical(box.rectTransform, UiStyle.SPACING, (int)UiStyle.PADDING);

            TextMeshProUGUI title = UiFactory.LocalizedText("Title", box.transform, "pause.title", UiStyle.FONT_TITLE,
                TextAlignmentOptions.Center, bold: true);
            UiFactory.Size(title, 64);

            Button resume = UiFactory.LocalizedButton("Resume", box.transform, "pause.resume", accent: true);
            Button settings = UiFactory.LocalizedButton("Settings", box.transform, "pause.settings");
            Button restart = UiFactory.LocalizedButton("Restart", box.transform, "pause.restart");
            Button menu = UiFactory.LocalizedButton("Menu", box.transform, "pause.menu");
            Button quit = UiFactory.LocalizedButton("Quit", box.transform, "pause.quit");
            resume.GetComponent<UiButtonSounds>().Click = UiSound.Back;

            UiFactory.SetField(pause, "_resume", resume);
            UiFactory.SetField(pause, "_settings", settings);
            UiFactory.SetField(pause, "_restart", restart);
            UiFactory.SetField(pause, "_menu", menu);
            UiFactory.SetField(pause, "_quit", quit);
            UiFactory.SetField(pause, "_firstSelected", resume);
            return pause;
        }

        // ---------- Results ----------

        private static BattleResultsScreen CreateResults(Transform parent)
        {
            BattleResultsScreen results = UiFactory.Screen<BattleResultsScreen>("Results", parent, overlay: false,
                canGoBack: false, dim: true);

            Image box = UiFactory.Panel("Box", results.transform, UiStyle.PanelLight);
            UiFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 520));
            UiFactory.Vertical(box.rectTransform, UiStyle.SPACING, (int)UiStyle.PADDING);

            // BattleResultsScreen.Setup writes the texts; the buttons are static.
            TextMeshProUGUI title = UiFactory.Text("Title", box.transform, "-", UiStyle.FONT_TITLE + 12,
                TextAlignmentOptions.Center, bold: true);
            UiFactory.Size(title, 80);
            TextMeshProUGUI goal = UiFactory.Text("Goal", box.transform, "-", UiStyle.FONT_BODY,
                TextAlignmentOptions.Center, color: UiStyle.TextMuted);
            UiFactory.Size(goal, 60);
            TextMeshProUGUI time = UiFactory.Text("Time", box.transform, "-", UiStyle.FONT_BODY,
                TextAlignmentOptions.Center);
            UiFactory.Size(time, 32);
            TextMeshProUGUI kills = UiFactory.Text("Kills", box.transform, "-", UiStyle.FONT_BODY,
                TextAlignmentOptions.Center);
            UiFactory.Size(kills, 32);
            TextMeshProUGUI reward = UiFactory.Text("Reward", box.transform, "-", UiStyle.FONT_HEADER,
                TextAlignmentOptions.Center, bold: true, color: UiStyle.Accent);
            UiFactory.Size(reward, 48, -1, -1, 1);

            RectTransform row = UiFactory.Rect("Buttons", box.transform);
            UiFactory.Horizontal(row, UiStyle.SPACING, 0, TextAnchor.MiddleCenter);
            UiFactory.Size(row, UiStyle.BUTTON_HEIGHT);
            Button again = UiFactory.LocalizedButton("Again", row, "results.again");
            UiFactory.Size(again, UiStyle.BUTTON_HEIGHT, 210);
            Button hangar = UiFactory.LocalizedButton("Hangar", row, "results.to_hangar");
            UiFactory.Size(hangar, UiStyle.BUTTON_HEIGHT, 210);
            Button next = UiFactory.LocalizedButton("Next", row, "results.next", accent: true);
            UiFactory.Size(next, UiStyle.BUTTON_HEIGHT, 260);

            UiFactory.SetField(results, "_title", title);
            UiFactory.SetField(results, "_goal", goal);
            UiFactory.SetField(results, "_time", time);
            UiFactory.SetField(results, "_kills", kills);
            UiFactory.SetField(results, "_reward", reward);
            UiFactory.SetField(results, "_again", again);
            UiFactory.SetField(results, "_hangar", hangar);
            UiFactory.SetField(results, "_next", next);
            UiFactory.SetField(results, "_firstSelected", again);
            return results;
        }

        // ---------- EventSystem ----------

        private static void CreateEventSystem(Transform parent)
        {
            GameObject go = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule), typeof(UiEventSystem));
            go.transform.SetParent(parent, false);
        }
    }
}
