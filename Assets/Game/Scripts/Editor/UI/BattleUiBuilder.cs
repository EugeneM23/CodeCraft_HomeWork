using ScreenNavigation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the BattleUi prefab: a canvas with the HUD (tank bars, crosshair, the run's counters, skills,
    /// special, elite bar, gate pointer, crush streak), the pause with the run's skills, the skill choice, the
    /// run results, the Yes/No dialog and the settings screen, the EventSystem, and BattleFlow on the root
    /// wired to all of them. The run itself comes from AppServices.RunHud at run time. The HUD's
    /// often-changing parts sit under nested canvases of their own, so their changes do not rebuild the rest.
    /// Layout is anchored to the screen's corners and centre, so it holds from 16:9 to 21:9.
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
        private const int SKILL_PLACES = SkillOfferUseCase.MAX_WEAPONS + SkillOfferUseCase.MAX_UPGRADES;
        private const float SKILL_ICON = 48f;
        private const float SPECIAL_SIZE = 72f;
        private const string KNOB_SPRITE = "UI/Skin/Knob.psd";

        // The icon set given to every skill icon view of this build
        private static UiIconSetConfig s_icons;

        [MenuItem("Tools/UI/Build Battle UI")]
        public static void Build()
        {
            s_icons = UiIconSetBuilder.Ensure();
            Canvas canvas = UiFactory.Canvas(PREFAB_NAME, SORTING_ORDER);
            Transform root = canvas.transform;

            HudScreen hud = CreateHud(root);
            PauseScreen pause = CreatePause(root);
            SkillChoiceScreen choice = CreateSkillChoice(root);
            RunResultsScreen results = CreateResults(root);
            SettingsScreen settings = SettingsScreenBuilder.Create(root);
            ConfirmDialog dialog = ConfirmDialogBuilder.Create(root);

            LevelCatalog catalog = UiDataBuilder.LoadLevelCatalog();
            if (catalog == null)
                Debug.LogWarning($"{nameof(BattleUiBuilder)}: no {UiDataBuilder.LEVEL_CATALOG_PATH} yet (Build UI data first); " +
                                 "BattleUi is saved without it, «Ещё раз» then reloads the open scene only through the core flow.");

            CreateEventSystem(root);

            BattleFlow flow = canvas.gameObject.AddComponent<BattleFlow>();
            UiFactory.SetField(flow, "_hud", hud);
            UiFactory.SetField(flow, "_pause", pause);
            UiFactory.SetField(flow, "_skillChoice", choice);
            UiFactory.SetField(flow, "_results", results);
            UiFactory.SetField(flow, "_confirmDialog", dialog);
            UiFactory.SetField(flow, "_settings", settings);
            UiFactory.SetField(flow, "_levelCatalog", catalog);

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
            CanvasGroup arenaHint = CreateArenaHint(screen, out TMP_Text arenaHintText);

            // Often-changing part: bars, crosshair, counters, pointer.
            RectTransform dynamic = NestedCanvas("Dynamic", screen);
            TankHudView tank = dynamic.gameObject.AddComponent<TankHudView>();
            CreateHullBars(dynamic, tank);
            CreateWeaponBars(dynamic, tank);
            CreateCrosshair(dynamic, tank);

            RunHudView run = dynamic.gameObject.AddComponent<RunHudView>();
            CreateRunCounters(dynamic, run);
            CreateSkillIcons(dynamic, run);
            CreateSpecial(dynamic, run);
            CreateEliteBar(dynamic, run);
            CreateCrush(dynamic, run);
            CreateGatePointer(dynamic, run);
            UiFactory.SetField(run, "_arenaHintGroup", arenaHint);
            UiFactory.SetField(run, "_arenaHintText", arenaHintText);

            // Debug block (F3), hidden by default, on a canvas of its own: it changes 4 times a second.
            RectTransform debug = NestedCanvas("Debug", screen);
            TMP_Text[] lines = CreateDebug(debug);
            debug.gameObject.SetActive(false);

            UiFactory.SetField(hud, "_tank", tank);
            UiFactory.SetField(hud, "_run", run);
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
            Image panel = UiFactory.Frame("Hints", parent, UiPackSprites.Panel);
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
                new Vector2(LABEL_WIDTH + BAR_WIDTH + VALUE_WIDTH + 2 * UiStyle.SPACING, ROW_HEIGHT));
            UiFactory.Vertical(group, 6);

            // GDD HUD: health only, no armour stat
            Row(group, "Health", "run.hud_health", UiStyle.Health, out Image healthFill, out TMP_Text healthText);

            UiFactory.SetField(tank, "_healthFill", healthFill);
            UiFactory.SetField(tank, "_healthText", healthText);
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
            Image lamp = UiFactory.Frame("Lamp", boost, UiPackSprites.BarFill, UiStyle.Disabled); // TankHudView tints it
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
            arc.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(KNOB_SPRITE);
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

        private static TMP_Text[] CreateDebug(RectTransform parent)
        {
            Image panel = UiFactory.Frame("Panel", parent, UiPackSprites.Panel);
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

        // ---------- HUD: the run ----------

        /// <summary>The arena's hints other than the keys (crush, destroy cover), top centre under the elite bar.</summary>
        private static CanvasGroup CreateArenaHint(RectTransform parent, out TMP_Text text)
        {
            Image panel = UiFactory.Frame("ArenaHint", parent, UiPackSprites.Panel);
            panel.raycastTarget = false;
            UiFactory.Place(panel.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(760, 84));
            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            text = UiFactory.Text("Text", panel.transform, "-", UiStyle.FONT_BODY, TextAlignmentOptions.Center,
                bold: true, color: UiStyle.Accent); // RunHudView writes it
            UiFactory.Stretch(text.rectTransform, 16, 16, 8, 8);
            panel.gameObject.SetActive(false);
            return group;
        }

        private static void CreateRunCounters(RectTransform parent, RunHudView run)
        {
            Image panel = UiFactory.Frame("Run", parent, UiPackSprites.Panel);
            panel.raycastTarget = false;
            UiFactory.Place(panel.rectTransform, new Vector2(0, 1), new Vector2(UiStyle.PADDING, -UiStyle.PADDING),
                new Vector2(480, 140));
            UiFactory.Vertical(panel.rectTransform, 6, 12);

            // RunHudView writes every value
            RectTransform top = HudRow("Arena", panel.transform, 36);
            TMP_Text arena = HudText("Arena", top, UiStyle.FONT_HEADER, 240, true, UiStyle.Text);
            TMP_Text time = HudText("Time", top, UiStyle.FONT_HEADER, 140, false, UiStyle.TextMuted);

            RectTransform xp = HudRow("Experience", panel.transform, 26);
            TMP_Text level = HudText("Level", xp, UiStyle.FONT_BODY, 90, true, UiStyle.Accent);
            Image xpFill = UiFactory.Bar("XpBar", xp, Color.white, new Vector2(250, 14), UiPackSprites.ProgressBack,
                UiPackSprites.ProgressFill, Color.white); // the pack's cyan progress bar
            UiFactory.Size(xpFill.transform.parent.GetComponent<Image>(), 14, 250);
            TMP_Text xpText = HudText("XpValue", xp, UiStyle.FONT_SMALL, 90, false, UiStyle.TextMuted);

            RectTransform currency = HudRow("Currency", panel.transform, 28);
            TMP_Text marks = HudText("Marks", currency, UiStyle.FONT_BODY, 200, true, UiStyle.Accent);
            TMP_Text samples = HudText("Samples", currency, UiStyle.FONT_BODY, 200, true, UiStyle.Armor);

            UiFactory.SetField(run, "_arenaText", arena);
            UiFactory.SetField(run, "_timeText", time);
            UiFactory.SetField(run, "_levelText", level);
            UiFactory.SetField(run, "_xpFill", xpFill);
            UiFactory.SetField(run, "_xpText", xpText);
            UiFactory.SetField(run, "_marksText", marks);
            UiFactory.SetField(run, "_samplesText", samples);
        }

        private static RectTransform HudRow(string name, Transform parent, float height)
        {
            RectTransform row = UiFactory.Rect(name, parent);
            UiFactory.Horizontal(row, UiStyle.SPACING, 0, TextAnchor.MiddleLeft, expandHeight: false);
            UiFactory.Size(row, height);
            return row;
        }

        private static TMP_Text HudText(string name, Transform parent, float size, float width, bool bold, Color color)
        {
            TextMeshProUGUI text = UiFactory.Text(name, parent, "-", size, bold: bold, color: color);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            UiFactory.Size(text, size + 8, width);
            return text;
        }

        private static void CreateSkillIcons(RectTransform parent, RunHudView run)
        {
            const float spacing = 6f;
            RectTransform row = UiFactory.Rect("Skills", parent);
            UiFactory.Place(row, new Vector2(0.5f, 0), new Vector2(0, UiStyle.PADDING),
                new Vector2(SKILL_PLACES * (SKILL_ICON + spacing), SKILL_ICON));
            UiFactory.Horizontal(row, spacing, 0, TextAnchor.MiddleCenter, expandHeight: false);

            var icons = new Object[SKILL_PLACES];
            for (int i = 0; i < SKILL_PLACES; i++)
            {
                Image frame = SkillIcon($"Skill{i + 1}", row, SKILL_ICON, out Image symbol, out TMP_Text glyph,
                    out TMP_Text level);
                UiFactory.Size(frame, SKILL_ICON, SKILL_ICON);
                SkillIconView icon = frame.gameObject.AddComponent<SkillIconView>();
                UiFactory.SetField(icon, "_symbol", symbol);
                UiFactory.SetField(icon, "_glyph", glyph);
                UiFactory.SetField(icon, "_level", level);
                UiFactory.SetField(icon, "_icons", s_icons);
                icons[i] = icon;
            }
            UiFactory.SetArray(run, "_skillIcons", icons);
        }

        /// <summary>
        /// A skill icon: the pack's icon frame with the icon set's sprite in it (the glyph when it has none), the
        /// level on the pack's small plate in the corner.
        /// </summary>
        private static Image SkillIcon(string name, Transform parent, float size, out Image symbol, out TMP_Text glyphText,
            out TMP_Text levelText)
        {
            Image frame = UiFactory.IconFrame(name, parent, size, false, out symbol, out TextMeshProUGUI glyph);

            Image levelBack = UiFactory.Frame("Level", frame.transform, UiPackSprites.LevelPlate);
            levelBack.raycastTarget = false;
            float plate = Mathf.Round(size * 0.42f);
            UiFactory.Place(levelBack.rectTransform, new Vector2(1, 0), new Vector2(4f, -4f), new Vector2(plate, plate));
            TextMeshProUGUI level = UiFactory.Text("Value", levelBack.transform, "1", Mathf.Round(size * 0.28f),
                TextAlignmentOptions.Center, bold: true);
            level.textWrappingMode = TextWrappingModes.NoWrap;
            UiFactory.Stretch(level.rectTransform);

            glyphText = glyph;
            levelText = level;
            return frame;
        }

        private static void CreateSpecial(RectTransform parent, RunHudView run)
        {
            // Above the weapon bars, bottom-right: the pack's round skill frame (RunHudView tints it by readiness)
            // and a round charge fill in it.
            Image frame = UiFactory.Frame("Special", parent, UiPackSprites.SpecialFrame, UiStyle.Panel);
            frame.preserveAspect = true;
            frame.raycastTarget = false;
            UiFactory.Place(frame.rectTransform, new Vector2(1, 0),
                new Vector2(-UiStyle.PADDING, UiStyle.PADDING + 3 * ROW_HEIGHT + 12 + UiStyle.SPACING),
                new Vector2(SPECIAL_SIZE, SPECIAL_SIZE));

            Image fill = UiFactory.Panel("Fill", frame.transform, UiStyle.Accent);
            UiFactory.Stretch(fill.rectTransform, 10, 10, 10, 10);
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(KNOB_SPRITE);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.raycastTarget = false;

            TextMeshProUGUI key = UiFactory.LocalizedText("Key", frame.transform, "run.hud_special_key", UiStyle.FONT_HEADER,
                TextAlignmentOptions.Center, bold: true, color: UiStyle.Text);
            UiFactory.Stretch(key.rectTransform);

            TextMeshProUGUI label = UiFactory.LocalizedText("Label", frame.transform, "run.hud_special", UiStyle.FONT_SMALL,
                TextAlignmentOptions.Right, bold: true, color: UiStyle.TextMuted);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UiFactory.Place(label.rectTransform, new Vector2(0, 0.5f), new Vector2(-UiStyle.SPACING - 100f, 0), new Vector2(100, 24));

            UiFactory.SetField(run, "_specialGroup", frame.gameObject);
            UiFactory.SetField(run, "_specialFill", fill);
            UiFactory.SetField(run, "_specialFrame", frame);
        }

        private static void CreateEliteBar(RectTransform parent, RunHudView run)
        {
            const float width = 640f;
            RectTransform group = UiFactory.Rect("Elite", parent);
            UiFactory.Place(group, new Vector2(0.5f, 1), new Vector2(0, -UiStyle.PADDING), new Vector2(width, 56));
            UiFactory.Vertical(group, 4, 0, TextAnchor.UpperCenter);

            TextMeshProUGUI label = UiFactory.LocalizedText("Label", group, "run.hud_elite", UiStyle.FONT_SMALL,
                TextAlignmentOptions.Center, bold: true, color: UiStyle.Bad);
            UiFactory.Size(label, 24);
            Image fill = UiFactory.Bar("Bar", group, UiStyle.Bad, new Vector2(width, BAR_HEIGHT));
            UiFactory.Size(fill.transform.parent.GetComponent<Image>(), BAR_HEIGHT, width);

            UiFactory.SetField(run, "_eliteGroup", group.gameObject);
            UiFactory.SetField(run, "_eliteFill", fill);
        }

        private static void CreateCrush(RectTransform parent, RunHudView run)
        {
            RectTransform group = UiFactory.Rect("Crush", parent);
            UiFactory.Place(group, new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(480, 60));
            CanvasGroup canvasGroup = group.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            TextMeshProUGUI text = UiFactory.Text("Text", group, "-", UiStyle.FONT_TITLE, TextAlignmentOptions.Center,
                bold: true, color: UiStyle.Accent); // RunHudView writes it
            text.textWrappingMode = TextWrappingModes.NoWrap;
            UiFactory.Stretch(text.rectTransform);

            UiFactory.SetField(run, "_crushGroup", canvasGroup);
            UiFactory.SetField(run, "_crushText", text);
        }

        private static void CreateGatePointer(RectTransform parent, RunHudView run)
        {
            RectTransform pointer = UiFactory.Rect("GatePointer", parent);
            UiFactory.Place(pointer, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96, 96));

            // The pack's cyan chevron, pointing down like the built-in arrow the pointer's rotation expects
            Image arrow = UiFactory.Frame("Arrow", pointer, UiPackSprites.GateArrow);
            arrow.type = Image.Type.Simple;
            arrow.preserveAspect = true;
            arrow.raycastTarget = false;
            UiFactory.Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56));

            TextMeshProUGUI label = UiFactory.LocalizedText("Label", pointer, "run.hud_gate", UiStyle.FONT_SMALL,
                TextAlignmentOptions.Center, bold: true, color: UiStyle.Accent);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UiFactory.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -44), new Vector2(140, 24));
            pointer.gameObject.SetActive(false);

            UiFactory.SetField(run, "_pointerArea", parent);
            UiFactory.SetField(run, "_gatePointer", pointer);
            UiFactory.SetField(run, "_gateArrow", arrow.rectTransform);
        }

        // ---------- Pause ----------

        private static PauseScreen CreatePause(Transform parent)
        {
            PauseScreen pause = UiFactory.Screen<PauseScreen>("Pause", parent, overlay: true, canGoBack: true, dim: true);

            // Two pack windows side by side, the titles on their plates
            Image box = UiFactory.Window("Box", pause.transform);
            UiFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-250, 0), new Vector2(440, 320));
            UiFactory.WindowLayout(box.rectTransform, UiStyle.SPACING, true);
            UiFactory.LocalizedWindowTitle(box, "run.pause_title", 360f);

            Button resume = UiFactory.LocalizedButton("Resume", box.transform, "run.pause_resume", accent: true);
            Button settings = UiFactory.LocalizedButton("Settings", box.transform, "run.pause_settings");
            Button exit = UiFactory.LocalizedButton("Exit", box.transform, "run.pause_exit");
            resume.GetComponent<UiButtonSounds>().Click = UiSound.Back;

            // The run's skills, right of the buttons
            Image list = UiFactory.Window("Skills", pause.transform);
            UiFactory.Place(list.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(250, 0), new Vector2(440, 640));
            UiFactory.WindowLayout(list.rectTransform, 6, true);
            UiFactory.LocalizedWindowTitle(list, "run.pause_skills", 360f);

            var rows = new Object[SKILL_PLACES];
            for (int i = 0; i < SKILL_PLACES; i++)
                rows[i] = SkillRow($"Skill{i + 1}", list.transform);

            TextMeshProUGUI none = UiFactory.LocalizedText("None", list.transform, "run.pause_no_skills", UiStyle.FONT_BODY,
                TextAlignmentOptions.Center, color: UiStyle.TextMuted);
            UiFactory.Size(none, 40);

            UiFactory.SetField(pause, "_resume", resume);
            UiFactory.SetField(pause, "_settings", settings);
            UiFactory.SetField(pause, "_exit", exit);
            UiFactory.SetArray(pause, "_skillRows", rows);
            UiFactory.SetField(pause, "_noSkills", none.gameObject);
            UiFactory.SetField(pause, "_firstSelected", resume);
            return pause;
        }

        /// <summary>A pause list row: the skill's icon with its level, and its name.</summary>
        private static SkillIconView SkillRow(string name, Transform parent)
        {
            const float height = 40f;
            RectTransform row = UiFactory.Rect(name, parent);
            UiFactory.Horizontal(row, UiStyle.SPACING, 0, TextAnchor.MiddleLeft, expandHeight: false);
            UiFactory.Size(row, height);

            Image frame = SkillIcon("Icon", row, height, out Image symbol, out TMP_Text glyph, out TMP_Text level);
            UiFactory.Size(frame, height, height);

            TextMeshProUGUI skillName = UiFactory.Text("Name", row, "-", UiStyle.FONT_BODY); // SkillIconView writes it
            skillName.textWrappingMode = TextWrappingModes.NoWrap;
            UiFactory.Size(skillName, height, -1, 1);

            // The row is the pooled place, so hiding hides the whole row.
            SkillIconView view = row.gameObject.AddComponent<SkillIconView>();
            UiFactory.SetField(view, "_symbol", symbol);
            UiFactory.SetField(view, "_glyph", glyph);
            UiFactory.SetField(view, "_level", level);
            UiFactory.SetField(view, "_name", skillName);
            UiFactory.SetField(view, "_icons", s_icons);
            row.gameObject.SetActive(false);
            return view;
        }

        // ---------- Skill choice ----------

        private static SkillChoiceScreen CreateSkillChoice(Transform parent)
        {
            const float cardWidth = 360f;
            const float cardHeight = 480f;
            const float gap = 24f;

            SkillChoiceScreen choice = UiFactory.Screen<SkillChoiceScreen>("SkillChoice", parent, overlay: true,
                canGoBack: false, dim: true);

            TextMeshProUGUI title = UiFactory.LocalizedText("Title", choice.transform, "run.choice_title", UiStyle.FONT_TITLE,
                TextAlignmentOptions.Center, bold: true, color: UiStyle.Accent);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, cardHeight / 2 + 110), new Vector2(1000, 64));
            UiFactory.TitleLines(title.rectTransform, 240f); // the pack's lines either side of the title
            TextMeshProUGUI hint = UiFactory.LocalizedText("Hint", choice.transform, "run.choice_hint", UiStyle.FONT_BODY,
                TextAlignmentOptions.Center, color: UiStyle.TextMuted);
            UiFactory.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, cardHeight / 2 + 56), new Vector2(1000, 36));

            RectTransform row = UiFactory.Rect("Cards", choice.transform);
            UiFactory.Place(row, new Vector2(0.5f, 0.5f), new Vector2(0, -20),
                new Vector2(SkillOfferUseCase.OFFER_COUNT * cardWidth + (SkillOfferUseCase.OFFER_COUNT - 1) * gap, cardHeight));
            UiFactory.Horizontal(row, gap, 0, TextAnchor.MiddleCenter, expandHeight: false);

            var cards = new Object[SkillOfferUseCase.OFFER_COUNT];
            for (int i = 0; i < cards.Length; i++)
            {
                SkillCardView card = SkillCard($"Card{i + 1}", row);
                UiFactory.Size(card, cardHeight, cardWidth);
                cards[i] = card;
            }

            UiFactory.SetArray(choice, "_cards", cards);
            return choice;
        }

        private static SkillCardView SkillCard(string name, Transform parent)
        {
            const float iconSize = 104f;
            // The pack's item button (its pressed sprite on hover and selection)
            Image image = UiFactory.Panel(name, parent, Color.white);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            UiFactory.ApplyButtonSkin(button, UiPackSprites.SkillCard);
            image.gameObject.AddComponent<SelectOnPointerEnter>();
            VerticalLayoutGroup layout = UiFactory.Vertical(image.rectTransform, 8, (int)UiStyle.PADDING, TextAnchor.UpperCenter);
            layout.padding = new RectOffset(32, 32, 30, 30);
            SkillCardView card = image.gameObject.AddComponent<SkillCardView>();

            // SkillCardView.Setup writes every text
            TextMeshProUGUI key = UiFactory.Text("Key", image.transform, "-", UiStyle.FONT_SMALL, TextAlignmentOptions.Right,
                bold: true, color: UiStyle.TextMuted);
            UiFactory.Size(key, 22);

            RectTransform iconRow = UiFactory.Rect("IconRow", image.transform);
            UiFactory.Horizontal(iconRow, 0, 0, TextAnchor.MiddleCenter, expandHeight: false);
            UiFactory.Size(iconRow, iconSize);
            Image icon = UiFactory.IconFrame("Icon", iconRow, iconSize, true, out Image symbol, out TextMeshProUGUI glyph);
            UiFactory.Size(icon, iconSize, iconSize);

            TextMeshProUGUI skillName = UiFactory.Text("Name", image.transform, "-", UiStyle.FONT_HEADER,
                TextAlignmentOptions.Center, bold: true);
            UiFactory.Size(skillName, 72);
            TextMeshProUGUI kind = UiFactory.Text("Kind", image.transform, "-", UiStyle.FONT_SMALL,
                TextAlignmentOptions.Center, color: UiStyle.TextMuted);
            UiFactory.Size(kind, 22);
            TextMeshProUGUI level = UiFactory.Text("Level", image.transform, "-", UiStyle.FONT_HEADER,
                TextAlignmentOptions.Center, bold: true, color: UiStyle.Accent);
            UiFactory.Size(level, 36);
            TextMeshProUGUI description = UiFactory.Text("Description", image.transform, "-", UiStyle.FONT_BODY,
                TextAlignmentOptions.Top);
            UiFactory.Size(description, 100, -1, -1, 1);

            UiFactory.SetField(card, "_button", button);
            UiFactory.SetField(card, "_symbol", symbol);
            UiFactory.SetField(card, "_glyph", glyph);
            UiFactory.SetField(card, "_icons", s_icons);
            UiFactory.SetField(card, "_name", skillName);
            UiFactory.SetField(card, "_kind", kind);
            UiFactory.SetField(card, "_level", level);
            UiFactory.SetField(card, "_description", description);
            UiFactory.SetField(card, "_key", key);
            return card;
        }

        // ---------- Results ----------

        private static RunResultsScreen CreateResults(Transform parent)
        {
            RunResultsScreen results = UiFactory.Screen<RunResultsScreen>("Results", parent, overlay: false,
                canGoBack: false, dim: true);

            // The pack's window; the big victory/defeat title stays inside it (the screen colours it)
            Image box = UiFactory.Window("Box", results.transform);
            UiFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 760));
            UiFactory.WindowLayout(box.rectTransform, 8, false);

            // RunResultsScreen.Setup writes the values; labels and buttons are static.
            TextMeshProUGUI title = UiFactory.Text("Title", box.transform, "-", UiStyle.FONT_TITLE + 12,
                TextAlignmentOptions.Center, bold: true);
            UiFactory.Size(title, 72);
            TextMeshProUGUI arena = UiFactory.Text("Arena", box.transform, "-", UiStyle.FONT_HEADER,
                TextAlignmentOptions.Center);
            UiFactory.Size(arena, 40);
            TextMeshProUGUI record = UiFactory.LocalizedText("Record", box.transform, "run.results_record", UiStyle.FONT_HEADER,
                TextAlignmentOptions.Center, bold: true, color: UiStyle.Accent);
            UiFactory.Size(record, 40);

            TMP_Text time = StatRow(box.transform, "Time", "run.results_time", UiStyle.Text);
            TMP_Text kills = StatRow(box.transform, "Kills", "run.results_kills", UiStyle.Text);
            TMP_Text crushes = StatRow(box.transform, "Crushes", "run.results_crushes", UiStyle.Text);
            TMP_Text elites = StatRow(box.transform, "Elites", "run.results_elites", UiStyle.Text);
            TMP_Text marks = StatRow(box.transform, "Marks", "run.results_marks", UiStyle.Accent);
            TMP_Text samples = StatRow(box.transform, "Samples", "run.results_samples", UiStyle.Armor);

            RectTransform spacer = UiFactory.Rect("Spacer", box.transform);
            UiFactory.Size(spacer, 8, -1, -1, 1);

            RectTransform row = UiFactory.Rect("Buttons", box.transform);
            UiFactory.Horizontal(row, UiStyle.SPACING, 0, TextAnchor.MiddleCenter);
            UiFactory.Size(row, UiStyle.BUTTON_HEIGHT);
            Button again = UiFactory.LocalizedButton("Again", row, "run.results_again");
            UiFactory.Size(again, UiStyle.BUTTON_HEIGHT, 240);
            Button hangar = UiFactory.LocalizedButton("Hangar", row, "run.results_to_hangar", accent: true);
            UiFactory.Size(hangar, UiStyle.BUTTON_HEIGHT, 240);

            UiFactory.SetField(results, "_title", title);
            UiFactory.SetField(results, "_arena", arena);
            UiFactory.SetField(results, "_time", time);
            UiFactory.SetField(results, "_kills", kills);
            UiFactory.SetField(results, "_crushes", crushes);
            UiFactory.SetField(results, "_elites", elites);
            UiFactory.SetField(results, "_marks", marks);
            UiFactory.SetField(results, "_samples", samples);
            UiFactory.SetField(results, "_record", record.gameObject);
            UiFactory.SetField(results, "_hangar", hangar);
            UiFactory.SetField(results, "_again", again);
            UiFactory.SetField(results, "_firstSelected", hangar);
            return results;
        }

        /// <summary>A results row: label by key on the left, the value (written by the screen) on the right.</summary>
        private static TMP_Text StatRow(Transform parent, string name, string labelKey, Color valueColor)
        {
            RectTransform row = UiFactory.Rect(name, parent);
            UiFactory.Horizontal(row, UiStyle.SPACING, 0, TextAnchor.MiddleLeft, expandHeight: false);
            UiFactory.Size(row, 34);

            TextMeshProUGUI label = UiFactory.LocalizedText("Label", row, labelKey, UiStyle.FONT_BODY, color: UiStyle.TextMuted);
            UiFactory.Size(label, 34, -1, 1);
            TextMeshProUGUI value = UiFactory.Text("Value", row, "-", UiStyle.FONT_HEADER, TextAlignmentOptions.Right,
                bold: true, color: valueColor);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            UiFactory.Size(value, 34, 220);
            return value;
        }

        // ---------- EventSystem ----------

        private static void CreateEventSystem(Transform parent)
        {
            GameObject go = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule), typeof(UiInputModuleBinding));
            go.transform.SetParent(parent, false);
        }
    }
}
