using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Saving.UI.Editor
{
    /// <summary>
    /// Builds the slot menu out of uGUI and TextMesh Pro parts, wired and ready: a full-screen Canvas, a dimmed
    /// panel, a window with a title, a close button, a scrolling list with an inactive slot row (name, details, name
    /// field, Load/Save, Delete) and a yes/no dialog. The SaveSlotsMenu prefab of the module is made by it, and so is
    /// the menu that GameObject → Save System → Save Slots Menu puts into a scene.
    /// </summary>
    public static class SaveSlotsMenuBuilder
    {
        private static readonly Color Dim = new(0f, 0f, 0f, 0.6f);
        private static readonly Color Window = new(0.13f, 0.14f, 0.17f, 0.97f);
        private static readonly Color Row = new(0.2f, 0.22f, 0.26f, 1f);
        private static readonly Color Accent = new(0.27f, 0.55f, 0.95f, 1f);
        private static readonly Color Danger = new(0.8f, 0.3f, 0.3f, 1f);
        private static readonly Color Muted = new(0.75f, 0.77f, 0.8f, 1f);

        /// <summary>The whole menu as a new root object (a Canvas); not saved anywhere.</summary>
        public static SaveSlotsMenu Build()
        {
            TMP_DefaultControls.Resources resources = Resources();

            var root = new GameObject("SaveSlotsMenu", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            var menu = root.AddComponent<SaveSlotsMenu>();

            GameObject panel = Child("Panel", root);
            Stretch(panel);
            panel.AddComponent<Image>().color = Dim;

            GameObject window = Child("Window", panel);
            var windowRect = (RectTransform)window.transform;
            windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.sizeDelta = new Vector2(1280f, 800f);
            Image windowImage = window.AddComponent<Image>();
            windowImage.sprite = resources.background;
            windowImage.type = Image.Type.Sliced;
            windowImage.color = Window;
            var windowLayout = window.AddComponent<VerticalLayoutGroup>();
            windowLayout.padding = new RectOffset(32, 32, 28, 32);
            windowLayout.spacing = 18f;
            windowLayout.childControlWidth = windowLayout.childControlHeight = true;
            windowLayout.childForceExpandWidth = true;
            windowLayout.childForceExpandHeight = false;

            GameObject header = Child("Header", window);
            Layout(header, height: 64f, flexibleHeight: 0f);
            var headerLayout = header.AddComponent<HorizontalLayoutGroup>();
            headerLayout.childControlWidth = headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = true;
            TMP_Text title = Text("Title", header, "Load Game", 44f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Layout(title.gameObject, flexibleWidth: 1f);
            Button close = Button("Close", header, "X", resources, Danger);
            Layout(close.gameObject, width: 64f, height: 64f);

            GameObject scroll = DefaultControls.CreateScrollView(UguiResources(resources));
            scroll.name = "Slots";
            scroll.transform.SetParent(window.transform, false);
            Layout(scroll, flexibleHeight: 1f);
            var scrollRect = scroll.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;
            Object.DestroyImmediate(scrollRect.horizontalScrollbar.gameObject);
            scrollRect.horizontalScrollbar = null;
            scrollRect.scrollSensitivity = 30f;
            scroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);
            var list = scrollRect.content;
            var listLayout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.padding = new RectOffset(8, 8, 8, 8);
            listLayout.spacing = 10f;
            listLayout.childControlWidth = listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            SaveSlotView template = BuildRow(list.gameObject, resources);
            SaveConfirmDialog confirm = BuildConfirm(panel, resources);

            var serialized = new SerializedObject(menu);
            serialized.FindProperty("_panel").objectReferenceValue = panel;
            serialized.FindProperty("_title").objectReferenceValue = title;
            serialized.FindProperty("_list").objectReferenceValue = list;
            serialized.FindProperty("_slotTemplate").objectReferenceValue = template;
            serialized.FindProperty("_closeButton").objectReferenceValue = close;
            serialized.FindProperty("_confirm").objectReferenceValue = confirm;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            template.gameObject.SetActive(false);
            confirm.gameObject.SetActive(false);
            panel.SetActive(false);
            return menu;
        }

        /// <summary>An EventSystem for the active scene if it has none: with the Input System module when it is in use.</summary>
        public static EventSystem EnsureEventSystem()
        {
            EventSystem existing = Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (existing != null)
                return existing;

            var go = new GameObject("EventSystem", typeof(EventSystem));
            Type inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModule != null)
                go.AddComponent(inputSystemModule);
            else
                go.AddComponent<StandaloneInputModule>();

            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
            return go.GetComponent<EventSystem>();
        }

        private static SaveSlotView BuildRow(GameObject list, TMP_DefaultControls.Resources resources)
        {
            GameObject row = Child("Slot Template", list);
            Layout(row, height: 124f, flexibleHeight: 0f);
            Image background = row.AddComponent<Image>();
            background.sprite = resources.standard;
            background.type = Image.Type.Sliced;
            background.color = Row;
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 16, 12, 12);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            GameObject texts = Child("Texts", row);
            Layout(texts, flexibleWidth: 1f);
            var textsLayout = texts.AddComponent<VerticalLayoutGroup>();
            textsLayout.childControlWidth = textsLayout.childControlHeight = true;
            textsLayout.childForceExpandWidth = true;
            textsLayout.childForceExpandHeight = false;
            textsLayout.spacing = 4f;
            TMP_Text title = Text("Title", texts, "Slot 1", 32f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            TMP_Text details = Text("Details", texts, "Empty", 22f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            details.color = Muted;
            details.textWrappingMode = TextWrappingModes.Normal; // a long scene name takes a second line
            details.maxVisibleLines = 2;

            GameObject nameObject = TMP_DefaultControls.CreateInputField(resources);
            nameObject.name = "Name";
            nameObject.transform.SetParent(row.transform, false);
            Layout(nameObject, width: 240f, height: 56f);
            var nameField = nameObject.GetComponent<TMP_InputField>();
            nameField.characterLimit = 32;
            nameField.textComponent.fontSize = 24f;
            nameField.lineType = TMP_InputField.LineType.SingleLine;
            if (nameField.placeholder is TMP_Text placeholder)
            {
                placeholder.text = "Name…";
                placeholder.fontSize = 24f;
            }

            Button pick = Button("Pick", row, "Load", resources, Accent);
            Layout(pick.gameObject, width: 190f, height: 64f);
            Button delete = Button("Delete", row, "Delete", resources, Danger);
            Layout(delete.gameObject, width: 140f, height: 64f);

            var view = row.AddComponent<SaveSlotView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_title").objectReferenceValue = title;
            serialized.FindProperty("_details").objectReferenceValue = details;
            serialized.FindProperty("_pick").objectReferenceValue = pick;
            serialized.FindProperty("_pickLabel").objectReferenceValue = pick.GetComponentInChildren<TMP_Text>(true);
            serialized.FindProperty("_delete").objectReferenceValue = delete;
            serialized.FindProperty("_name").objectReferenceValue = nameField;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static SaveConfirmDialog BuildConfirm(GameObject panel, TMP_DefaultControls.Resources resources)
        {
            GameObject overlay = Child("Confirm", panel);
            Stretch(overlay);
            overlay.AddComponent<Image>().color = Dim;

            GameObject box = Child("Box", overlay);
            var boxRect = (RectTransform)box.transform;
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(720f, 300f);
            Image image = box.AddComponent<Image>();
            image.sprite = resources.background;
            image.type = Image.Type.Sliced;
            image.color = Window;
            var layout = box.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 32, 32);
            layout.spacing = 24f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TMP_Text message = Text("Message", box, "Are you sure?", 32f, FontStyles.Normal, TextAlignmentOptions.Center);
            Layout(message.gameObject, flexibleHeight: 1f);
            GameObject buttons = Child("Buttons", box);
            Layout(buttons, height: 72f, flexibleHeight: 0f);
            var buttonsLayout = buttons.AddComponent<HorizontalLayoutGroup>();
            buttonsLayout.spacing = 24f;
            buttonsLayout.childControlWidth = buttonsLayout.childControlHeight = true;
            buttonsLayout.childForceExpandWidth = true;
            buttonsLayout.childForceExpandHeight = true;
            Button yes = Button("Yes", buttons, "Yes", resources, Accent);
            Button no = Button("No", buttons, "No", resources, Row);

            var dialog = overlay.AddComponent<SaveConfirmDialog>();
            var serialized = new SerializedObject(dialog);
            serialized.FindProperty("_message").objectReferenceValue = message;
            serialized.FindProperty("_yes").objectReferenceValue = yes;
            serialized.FindProperty("_no").objectReferenceValue = no;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return dialog;
        }

        // ---------- Parts ----------

        private static GameObject Child(string name, GameObject parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        private static void Stretch(GameObject go)
        {
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Layout(GameObject go, float width = -1f, float height = -1f, float flexibleWidth = -1f,
            float flexibleHeight = -1f)
        {
            if (!go.TryGetComponent(out LayoutElement element))
                element = go.AddComponent<LayoutElement>();

            if (width >= 0f)
                element.preferredWidth = element.minWidth = width;

            if (height >= 0f)
                element.preferredHeight = element.minHeight = height;

            element.flexibleWidth = flexibleWidth;
            element.flexibleHeight = flexibleHeight;
        }

        private static TMP_Text Text(string name, GameObject parent, string value, float size, FontStyles style,
            TextAlignmentOptions alignment)
        {
            GameObject go = Child(name, parent);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static Button Button(string name, GameObject parent, string label, TMP_DefaultControls.Resources resources,
            Color color)
        {
            GameObject go = TMP_DefaultControls.CreateButton(resources);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.GetComponent<Image>().color = color;
            var text = go.GetComponentInChildren<TMP_Text>(true);
            text.text = label;
            text.fontSize = 28f;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            return go.GetComponent<Button>();
        }

        private static TMP_DefaultControls.Resources Resources() => new()
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
        };

        private static DefaultControls.Resources UguiResources(TMP_DefaultControls.Resources resources) => new()
        {
            standard = resources.standard,
            background = resources.background,
            inputField = resources.inputField,
            knob = resources.knob,
            checkmark = resources.checkmark,
            dropdown = resources.dropdown,
            mask = resources.mask
        };
    }
}
