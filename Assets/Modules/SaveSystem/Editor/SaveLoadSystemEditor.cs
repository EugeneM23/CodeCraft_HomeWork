using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Saving.Editor
{
    /// <summary>
    /// SaveLoadSystem in the Inspector, grouped the way a beginner thinks: where saves live, the server, start, when to
    /// save, scene objects, events — and a slot browser: every slot with its name, time and scene, to load (in play),
    /// copy as text, export or delete.
    /// </summary>
    [CustomEditor(typeof(SaveLoadSystem))]
    public sealed class SaveLoadSystemEditor : UnityEditor.Editor
    {
        private static bool _eventsOpen;
        private static bool _slotsOpen = true;
        private static bool _advancedOpen;
        private static bool _problemsOpen;

        private IReadOnlyList<SlotInfo> _slots;
        private SaveLoadService _browser;
        private string _browserError;

        public override void OnInspectorGUI()
        {
            var system = (SaveLoadSystem)target;
            serializedObject.Update();

            DrawScope(system);
            DrawTryOut(system);

            Header("Where saves live");
            SerializedProperty kind = Property("_storageKind");
            EditorGUILayout.PropertyField(kind, new GUIContent("Storage"));
            var storageKind = (SaveStorageKind)kind.enumValueIndex;
            EditorGUILayout.PropertyField(Property("_folder"),
                new GUIContent(storageKind == SaveStorageKind.PlayerPrefs ? "Key prefix" : "Folder"));
            if (storageKind == SaveStorageKind.Custom)
                EditorGUILayout.PropertyField(Property("_customStorage"));

            EditorGUILayout.PropertyField(Property("_compress"));
            SerializedProperty encrypt = Property("_encrypt");
            EditorGUILayout.PropertyField(encrypt);
            if (encrypt.boolValue)
                EditorGUILayout.PropertyField(Property("_encryptionKey"));

            if (storageKind == SaveStorageKind.Files)
                DrawFolder(Path.Combine(Application.persistentDataPath, Property("_folder").stringValue));

            Header("Server");
            EditorGUILayout.PropertyField(Property("_remote"), new GUIContent("Remote storage"));
            if (Property("_remote").objectReferenceValue != null)
                EditorGUILayout.PropertyField(Property("_uploadAfterSave"));

            Header("Start");
            EditorGUILayout.PropertyField(Property("_startupSlot"));
            EditorGUILayout.PropertyField(Property("_startInLatestSlot"));
            EditorGUILayout.PropertyField(Property("_loadOnStart"));
            EditorGUILayout.PropertyField(Property("_keepAcrossScenes"));
            EditorGUILayout.PropertyField(Property("_newGameScene"));
            EditorGUILayout.PropertyField(Property("_liveLoad"));
            EditorGUILayout.PropertyField(Property("_finishWorkBeforeSave"));

            Header("When to save");
            EditorGUILayout.PropertyField(Property("_saveOnQuit"));
            EditorGUILayout.PropertyField(Property("_saveOnPause"));
            EditorGUILayout.PropertyField(Property("_autoSavePeriod"));

            EditorGUILayout.Space();
            _eventsOpen = EditorGUILayout.Foldout(_eventsOpen, "Events", true);
            if (_eventsOpen)
            {
                EditorGUILayout.PropertyField(Property("_onSaved"));
                EditorGUILayout.PropertyField(Property("_onLoaded"));
                EditorGUILayout.PropertyField(Property("_onSlotsChanged"));
                EditorGUILayout.PropertyField(Property("_onFailed"));
            }

            bool changed = serializedObject.ApplyModifiedProperties();
            if (changed)
                ForgetSlots();

            EditorGUILayout.Space();
            DrawSlots(system);
        }

        private void OnDisable() => ForgetSlots();

        // The last save and the slot list change in play without the Inspector being touched
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        // ---------- What to save ----------

        private void DrawScope(SaveLoadSystem system)
        {
            Header("What to save");
            SerializedProperty saveObjects = Property("_saveSceneObjects");
            SerializedProperty scope = Property("_scope");
            using (new EditorGUI.DisabledScope(!saveObjects.boolValue))
                EditorGUILayout.PropertyField(scope, new GUIContent("Scope"));

            if (!saveObjects.boolValue)
            {
                EditorGUILayout.HelpBox("Scene objects are not saved: only values and systems registered from code.",
                    MessageType.None);
            }
            else if ((SaveScope)scope.enumValueIndex == SaveScope.WholeScene)
            {
                EditorGUILayout.PropertyField(Property("_coverage"), new GUIContent("Take From Objects",
                    "Transforms: where an object is, if it moved. Physics: bodies and colliders. Scripts: your " +
                    "components, all their fields. Effects: Animator, AudioSource, ParticleSystem, SpriteRenderer"));
                using (new EditorGUI.DisabledScope((Property("_coverage").intValue & (int)SaveCoverage.Scripts) == 0))
                    EditorGUILayout.PropertyField(Property("_privateFields"), new GUIContent("Private Fields"));
                EditorGUILayout.PropertyField(Property("_skipStatic"), new GUIContent("Skip Static Objects"));
                EditorGUILayout.PropertyField(Property("_ignored"), new GUIContent("Ignored Objects"), true);
                EditorGUILayout.HelpBox(
                    $"Every object of this scene is saved, nothing to add to them ({system.SceneObjects.Count} listed). " +
                    "An object with a Saveable keeps its own setup. Objects made in play come back as copies of the prefab " +
                    $"they are named after ({system.ScenePrefabs.Count} prefabs known). The lists are refreshed when the " +
                    "scene is saved and when Play starts.", MessageType.None);
                if (!Application.isPlaying && GUILayout.Button("Refresh Lists Now"))
                {
                    serializedObject.ApplyModifiedProperties();
                    SaveSceneBaker.Bake(system);
                    serializedObject.Update();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Only objects with a Saveable component are saved, each with the components " +
                                        "checked on it.", MessageType.None);
            }

            _advancedOpen = EditorGUILayout.Foldout(_advancedOpen, "Advanced", true);
            if (!_advancedOpen)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(saveObjects, new GUIContent("Save Scene Objects"));
                if (!saveObjects.boolValue)
                    return;

                EditorGUILayout.PropertyField(Property("_settings"));
                if (Property("_settings").objectReferenceValue == null)
                    EditorGUILayout.HelpBox("The settings asset is set in a moment (in a scene, not in prefab mode).",
                        MessageType.Info);
            }
        }

        // ---------- Try it out ----------

        private void DrawTryOut(SaveLoadSystem system)
        {
            Header("Try it out");
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Save", "Save the current slot (Play Mode)")))
                    system.SaveGame();

                if (GUILayout.Button(new GUIContent("Load", "Load the current slot (Play Mode)")))
                    system.LoadGame();

                if (GUILayout.Button(new GUIContent("New Game", "Start over in a free slot (Play Mode)")))
                    system.NewGame();
            }

            SerializedProperty hotkeys = Property("_hotkeys");
            EditorGUILayout.PropertyField(hotkeys, new GUIContent("Hotkeys", "Save and load with keys in play"));
            if (hotkeys.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(Property("_saveKey"), new GUIContent("Save"));
                    EditorGUILayout.PropertyField(Property("_loadKey"), new GUIContent("Load"));
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("The buttons work in Play Mode; from code: SaveLoadSystem.Current.Save() and Load().",
                    MessageType.None);
                return;
            }

            SaveReport report = system.LastSave;
            if (report == null)
            {
                EditorGUILayout.HelpBox($"Slot {system.CurrentSlot}. Nothing saved in this session yet.", MessageType.None);
                return;
            }

            EditorGUILayout.HelpBox(Describe(report), report.Outcome == SaveOutcome.Saved ? MessageType.Info : MessageType.Warning);
            if (report.Problems.Count == 0)
                return;

            _problemsOpen = EditorGUILayout.Foldout(_problemsOpen, $"Not saved or saved with a warning ({report.Problems.Count})", true);
            if (!_problemsOpen)
                return;

            foreach (string problem in report.Problems)
                EditorGUILayout.LabelField(problem, EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>One line about a save: when, where, how much.</summary>
        public static string Describe(SaveReport report) =>
            $"Last save {report.SavedAtUtc.ToLocalTime():HH:mm:ss}: {report.Outcome}, slot {report.Slot}, " +
            $"{report.Objects} scene objects, {report.Characters / 1024f:0.#} KB, {report.Milliseconds:0} ms" +
            (report.Problems.Count > 0 ? $", {report.Problems.Count} warnings" : string.Empty);

        // ---------- Slots ----------

        private void DrawSlots(SaveLoadSystem system)
        {
            _slotsOpen = EditorGUILayout.Foldout(_slotsOpen, "Slots", true);
            if (!_slotsOpen)
                return;

            ISaveLoadService saves = Saves(system);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh"))
                    ForgetSlots();

                using (new EditorGUI.DisabledScope(saves == null))
                {
                    if (GUILayout.Button("Delete All") && saves != null
                        && EditorUtility.DisplayDialog("Delete All Slots", "Delete every save slot of this save system?",
                            "Delete", "Cancel"))
                    {
                        foreach (SlotInfo slot in saves.GetSlots())
                            saves.DeleteSlot(slot.Slot);

                        ForgetSlots();
                    }
                }
            }

            if (_browserError != null)
            {
                EditorGUILayout.HelpBox(_browserError, MessageType.Warning);
                return;
            }

            if (saves == null)
                return;

            _slots ??= saves.GetSlots();
            if (_slots.Count == 0)
            {
                EditorGUILayout.HelpBox("No saves yet.", MessageType.None);
                return;
            }

            foreach (SlotInfo slot in _slots)
            {
                if (DrawSlot(saves, slot, Application.isPlaying && saves.CurrentSlot == slot.Slot))
                    break; // the list changed under the loop
            }
        }

        /// <summary>One slot: title and details, Load (in play), Copy and Delete; true when the list changed.</summary>
        private bool DrawSlot(ISaveLoadService saves, SlotInfo slot, bool current)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string title = $"Slot {slot.Slot}" + (string.IsNullOrEmpty(slot.Name) ? string.Empty : $"  ·  {slot.Name}") +
                               (current ? "  (current)" : string.Empty);
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(Details(slot), EditorStyles.miniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    {
                        if (GUILayout.Button(new GUIContent("Load", "Make it the current slot and load it (Play Mode)")))
                        {
                            ((SaveLoadSystem)target).LoadFromSlot(slot.Slot);
                            return true;
                        }
                    }

                    if (GUILayout.Button(new GUIContent("Copy", "Copy the slot as one text (ExportSlot) to the clipboard")))
                        CopyToClipboard(saves, slot.Slot);

                    if (GUILayout.Button("Delete") && EditorUtility.DisplayDialog("Delete Slot",
                            $"Delete save slot {slot.Slot}?", "Delete", "Cancel"))
                    {
                        saves.DeleteSlot(slot.Slot);
                        ForgetSlots();
                        return true;
                    }
                }
            }

            return false;
        }

        private static void CopyToClipboard(ISaveLoadService saves, int slot)
        {
            try
            {
                EditorGUIUtility.systemCopyBuffer = saves.ExportSlot(slot) ?? string.Empty;
            }
            catch (Exception exception)
            {
                // Another encryption key, a storage that cannot be reached
                EditorUtility.DisplayDialog("Copy Slot", $"Slot {slot} could not be read: {exception.Message}", "OK");
            }
        }

        /// <summary>The live system in play; in edit mode a service over the storage the Inspector describes.</summary>
        private ISaveLoadService Saves(SaveLoadSystem system)
        {
            if (Application.isPlaying)
                return system;

            if (_browser != null || _browserError != null)
                return _browser;

            try
            {
                _browser = new SaveLoadService(system.CreateConfiguredStorage(), Application.version);
            }
            catch (Exception exception)
            {
                _browserError = $"The storage could not be opened: {exception.Message}";
            }

            return _browser;
        }

        private void ForgetSlots()
        {
            _slots = null;
            _browser = null;
            _browserError = null;
        }

        private static string Details(SlotInfo slot)
        {
            var parts = new List<string> { slot.SavedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm") };
            parts.Add("played " + TimeSpan.FromSeconds(Math.Round(slot.PlayTimeSeconds)).ToString("g"));
            if (!string.IsNullOrEmpty(slot.Scene))
                parts.Add(slot.Scene);

            if (!string.IsNullOrEmpty(slot.GameVersion))
                parts.Add("v" + slot.GameVersion);

            return string.Join("  ·  ", parts);
        }

        // ---------- Drawing ----------

        private SerializedProperty Property(string name) => serializedObject.FindProperty(name);

        private static void Header(string text)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }

        private static void DrawFolder(string path)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
                if (GUILayout.Button("Open", GUILayout.Width(50f)))
                {
                    Directory.CreateDirectory(path);
                    EditorUtility.RevealInFinder(path);
                }
            }
        }
    }
}
