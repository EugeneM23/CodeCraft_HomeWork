using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the settings screen inside a canvas and wires its fields; MenuUiBuilder and BattleUiBuilder embed
    /// it in their prefabs (no prefab of its own). A centred panel: title, tabs, the tab's rows, bottom buttons.
    /// </summary>
    public static class SettingsScreenBuilder
    {
        private static readonly Vector2 PanelSize = new(1100, 760);
        private const float TAB_WIDTH = 240f;
        private const float LABEL_SHARE = 4f;
        private const float CONTROL_SHARE = 6f;
        private const float VALUE_WIDTH = 90f;
        private const float SLIDER_HEIGHT = 28f;
        private const float TOGGLE_HEIGHT = 32f;

        public static SettingsScreen Create(Transform parent)
        {
            SettingsScreen screen = UiFactory.Screen<SettingsScreen>("SettingsScreen", parent, dim: true);

            Image panel = UiFactory.Panel("Panel", screen.transform, UiStyle.Panel);
            UiFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, PanelSize);
            UiFactory.Vertical(panel.rectTransform, UiStyle.SPACING, (int)UiStyle.PADDING);

            TextMeshProUGUI title = UiFactory.Text("Title", panel.transform, "НАСТРОЙКИ", UiStyle.FONT_TITLE,
                TextAlignmentOptions.Center, bold: true);
            UiFactory.Size(title, 56);

            // Tabs
            RectTransform tabs = UiFactory.Rect("Tabs", panel.transform);
            UiFactory.Horizontal(tabs, UiStyle.SPACING, 0, TextAnchor.MiddleCenter);
            UiFactory.Size(tabs, UiStyle.BUTTON_HEIGHT);
            Button graphicsTab = TabButton(tabs, "GraphicsTab", "Графика");
            Button audioTab = TabButton(tabs, "AudioTab", "Звук");
            Button controlsTab = TabButton(tabs, "ControlsTab", "Управление");

            RectTransform content = UiFactory.Rect("Content", panel.transform);
            UiFactory.Size(content, -1, -1, 1, 1);

            GraphicsSettingsTab graphics = BuildGraphics(content);
            AudioSettingsTab audio = BuildAudio(content);
            ControlsSettingsTab controls = BuildControls(content, screen.transform);

            // Bottom buttons
            RectTransform bottom = UiFactory.Rect("Buttons", panel.transform);
            UiFactory.Horizontal(bottom, UiStyle.SPACING);
            UiFactory.Size(bottom, UiStyle.BUTTON_HEIGHT);
            Button defaults = UiFactory.Button("Defaults", bottom, "По умолчанию");
            UiFactory.Size(defaults, UiStyle.BUTTON_HEIGHT, 260);
            RectTransform spacer = UiFactory.Rect("Spacer", bottom);
            UiFactory.Size(spacer, -1, 0, 1);
            Button back = UiFactory.Button("Back", bottom, "Назад");
            UiFactory.Size(back, UiStyle.BUTTON_HEIGHT, 200);
            back.GetComponent<UiButtonSounds>().Click = UiSound.Back;
            Button apply = UiFactory.Button("Apply", bottom, "Применить", accent: true);
            UiFactory.Size(apply, UiStyle.BUTTON_HEIGHT, 240);

            UiFactory.SetArray(screen, "_tabButtons", graphicsTab, audioTab, controlsTab);
            UiFactory.SetArray(screen, "_tabLabels", Label(graphicsTab), Label(audioTab), Label(controlsTab));
            UiFactory.SetArray(screen, "_tabPanels", graphics.gameObject, audio.gameObject, controls.gameObject);
            UiFactory.SetField(screen, "_graphics", graphics);
            UiFactory.SetField(screen, "_audio", audio);
            UiFactory.SetField(screen, "_controls", controls);
            UiFactory.SetField(screen, "_apply", apply);
            UiFactory.SetField(screen, "_defaults", defaults);
            UiFactory.SetField(screen, "_back", back);
            UiFactory.SetField(screen, "_firstSelected", graphicsTab);

            audio.gameObject.SetActive(false);
            controls.gameObject.SetActive(false);
            return screen;
        }

        private static GraphicsSettingsTab BuildGraphics(RectTransform content)
        {
            RectTransform root = TabPanel(content, "Graphics");
            GraphicsSettingsTab tab = root.gameObject.AddComponent<GraphicsSettingsTab>();

            TMP_Dropdown resolution = DropdownRow(root, "Разрешение");
            TMP_Dropdown windowMode = DropdownRow(root, "Режим окна");
            TMP_Dropdown quality = DropdownRow(root, "Качество");

            RectTransform vSyncRow = Row(root, "Вертикальная синхронизация");
            Toggle vSync = UiFactory.Toggle("VSync", vSyncRow, "Вкл.");
            UiFactory.Size(vSync, TOGGLE_HEIGHT, 0, CONTROL_SHARE);

            TMP_Dropdown frameLimit = DropdownRow(root, "Ограничение FPS");

            UiFactory.SetField(tab, "_resolution", resolution);
            UiFactory.SetField(tab, "_windowMode", windowMode);
            UiFactory.SetField(tab, "_quality", quality);
            UiFactory.SetField(tab, "_vSync", vSync);
            UiFactory.SetField(tab, "_frameLimit", frameLimit);
            return tab;
        }

        private static AudioSettingsTab BuildAudio(RectTransform content)
        {
            RectTransform root = TabPanel(content, "Audio");
            AudioSettingsTab tab = root.gameObject.AddComponent<AudioSettingsTab>();

            // AudioChannel order: Master, Music, Effects, Ui, Ambient
            string[] names = { "Общая", "Музыка", "Эффекты", "Интерфейс", "Окружение" };
            Object[] sliders = new Object[names.Length];
            Object[] values = new Object[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                Slider slider = SliderRow(root, names[i], out TextMeshProUGUI value);
                slider.minValue = 0f;
                slider.maxValue = 100f;
                slider.wholeNumbers = true;
                slider.value = 100f;
                value.text = "100 %";
                sliders[i] = slider;
                values[i] = value;
            }

            UiFactory.SetArray(tab, "_sliders", sliders);
            UiFactory.SetArray(tab, "_values", values);
            return tab;
        }

        private static ControlsSettingsTab BuildControls(RectTransform content, Transform screen)
        {
            RectTransform root = TabPanel(content, "Controls");
            ControlsSettingsTab tab = root.gameObject.AddComponent<ControlsSettingsTab>();

            Slider sensitivity = SliderRow(root, "Чувствительность мыши", out TextMeshProUGUI sensitivityValue);
            sensitivity.minValue = 0.2f;
            sensitivity.maxValue = 3f;
            sensitivity.wholeNumbers = false;
            sensitivity.value = 1f;
            sensitivityValue.text = "1.0";

            RectTransform list = UiFactory.ScrollList("Keys", root, out ScrollRect scroll, 6f);
            UiFactory.Size(scroll, -1, -1, 1, 1);
            RebindRow template = BuildRowTemplate(list);

            RectTransform resetRow = UiFactory.Rect("ResetRow", root);
            UiFactory.Horizontal(resetRow, UiStyle.SPACING, 0, TextAnchor.MiddleRight);
            UiFactory.Size(resetRow, UiStyle.BUTTON_HEIGHT);
            Button resetAll = UiFactory.Button("ResetAll", resetRow, "Сбросить всё");
            UiFactory.Size(resetAll, UiStyle.BUTTON_HEIGHT, 260);

            // Over the whole screen, last child: covers the panel while a key is awaited.
            Image overlay = UiFactory.Panel("RebindOverlay", screen, UiStyle.Dim);
            UiFactory.Stretch(overlay.rectTransform);
            TextMeshProUGUI overlayText = UiFactory.Text("Text", overlay.transform, "Нажмите клавишу…\nEsc — отмена",
                UiStyle.FONT_HEADER, TextAlignmentOptions.Center, bold: true);
            UiFactory.Stretch(overlayText.rectTransform);
            overlay.gameObject.SetActive(false);

            UiFactory.SetField(tab, "_sensitivity", sensitivity);
            UiFactory.SetField(tab, "_sensitivityValue", sensitivityValue);
            UiFactory.SetField(tab, "_rowTemplate", template);
            UiFactory.SetField(tab, "_resetAll", resetAll);
            UiFactory.SetField(tab, "_overlay", overlay.gameObject);
            UiFactory.SetField(tab, "_overlayText", overlayText);
            return tab;
        }

        private static RebindRow BuildRowTemplate(RectTransform list)
        {
            Image row = UiFactory.Panel("RowTemplate", list, UiStyle.PanelLight);
            UiFactory.Horizontal(row.rectTransform, UiStyle.SPACING, 4, TextAnchor.MiddleLeft, expandHeight: false);
            UiFactory.Size(row, UiStyle.BUTTON_HEIGHT + 8);
            RebindRow rebind = row.gameObject.AddComponent<RebindRow>();

            TextMeshProUGUI label = UiFactory.Text("Label", row.transform, "Действие");
            UiFactory.Size(label, UiStyle.BUTTON_HEIGHT, 0, 1);
            Button key = UiFactory.Button("Key", row.transform, "Клавиша");
            UiFactory.Size(key, UiStyle.BUTTON_HEIGHT, 280);
            Button reset = UiFactory.Button("Reset", row.transform, "Сброс");
            UiFactory.Size(reset, UiStyle.BUTTON_HEIGHT, 110);

            UiFactory.SetField(rebind, "_background", row);
            UiFactory.SetField(rebind, "_label", label);
            UiFactory.SetField(rebind, "_keyButton", key);
            UiFactory.SetField(rebind, "_keyText", Label(key));
            UiFactory.SetField(rebind, "_resetButton", reset);
            row.gameObject.SetActive(false);
            return rebind;
        }

        // ---------- Pieces ----------

        private static Button TabButton(RectTransform row, string name, string label)
        {
            Button button = UiFactory.Button(name, row, label);
            UiFactory.Size(button, UiStyle.BUTTON_HEIGHT, TAB_WIDTH);
            Label(button).color = UiStyle.TextMuted;
            return button;
        }

        private static RectTransform TabPanel(RectTransform content, string name)
        {
            RectTransform root = UiFactory.Stretch(UiFactory.Rect(name, content));
            UiFactory.Vertical(root, UiStyle.SPACING);
            return root;
        }

        /// <summary>A line with the label on the left 40 %; the control goes into the rest.</summary>
        private static RectTransform Row(RectTransform parent, string label)
        {
            RectTransform row = UiFactory.Rect(label, parent);
            UiFactory.Horizontal(row, UiStyle.SPACING, 0, TextAnchor.MiddleLeft, expandHeight: false);
            UiFactory.Size(row, UiStyle.BUTTON_HEIGHT);
            TextMeshProUGUI text = UiFactory.Text("Label", row, label);
            UiFactory.Size(text, UiStyle.BUTTON_HEIGHT, 0, LABEL_SHARE);
            return row;
        }

        private static TMP_Dropdown DropdownRow(RectTransform parent, string label)
        {
            TMP_Dropdown dropdown = UiFactory.Dropdown("Dropdown", Row(parent, label));
            UiFactory.Size(dropdown, UiStyle.BUTTON_HEIGHT, 0, CONTROL_SHARE);
            return dropdown;
        }

        private static Slider SliderRow(RectTransform parent, string label, out TextMeshProUGUI value)
        {
            RectTransform row = Row(parent, label);
            RectTransform holder = UiFactory.Rect("Control", row);
            UiFactory.Horizontal(holder, UiStyle.SPACING, 0, TextAnchor.MiddleLeft, expandHeight: false);
            UiFactory.Size(holder, UiStyle.BUTTON_HEIGHT, 0, CONTROL_SHARE);

            Slider slider = UiFactory.Slider("Slider", holder);
            UiFactory.Size(slider, SLIDER_HEIGHT, 0, 1);
            value = UiFactory.Text("Value", holder, "", UiStyle.FONT_BODY, TextAlignmentOptions.Right);
            UiFactory.Size(value, UiStyle.BUTTON_HEIGHT, VALUE_WIDTH);
            return slider;
        }

        private static TMP_Text Label(Button button) => button.GetComponentInChildren<TMP_Text>(true);
    }
}
