using System;
using System.Collections.Generic;
using System.Linq;
using Saving.Objects;
using UnityEditor;
using UnityEngine;

namespace Saving.Editor
{
    /// <summary>
    /// Saveable in the Inspector: its id with a "New ID" button, its prefab id, every component of the object and its
    /// children with a checkbox, who saves it and a mode (Auto, All Fields, Selected with a list of members), serializer
    /// options, and warnings about a missing or repeated id. Changes go through Undo and prefab overrides.
    /// </summary>
    [CustomEditor(typeof(Saveable))]
    public sealed class SaveableEditor : UnityEditor.Editor
    {
        public readonly struct Row
        {
            private readonly ICollection<string> _on;

            public Row(string key, Type type, SerializerKind kind, bool saved, SaveMode mode, IReadOnlyList<string> members,
                IReadOnlyList<string> options, ICollection<string> on)
            {
                Key = key;
                Type = type;
                Kind = kind;
                Saved = saved;
                Mode = mode;
                Members = members ?? Array.Empty<string>();
                Options = options ?? Array.Empty<string>();
                _on = on;
            }

            public string Key { get; }
            public Type Type { get; }
            public SerializerKind Kind { get; }
            public bool Saved { get; }
            public SaveMode Mode { get; }
            public IReadOnlyList<string> Members { get; }
            public IReadOnlyList<string> Options { get; }
            public IReadOnlyList<SaveMember> Candidates => SaveMembers.Candidates(Type);

            public bool IsOn(string option) => _on != null && _on.Contains(option);
        }

        private static readonly string[] AllModes = { "Auto", "All Fields", "Selected" };
        private static readonly string[] ModesWithoutAuto = { "All Fields", "Selected" };

        private readonly HashSet<string> _expanded = new();

        /// <summary>Every component the Saveable covers, with its checkbox, mode and members.</summary>
        public static List<Row> BuildRows(Saveable saveable, ComponentSerializerCatalog catalog)
        {
            var rows = new List<Row>();
            foreach (KeyedComponent entry in ComponentKeys.Collect(saveable))
            {
                Type type = entry.Component.GetType();
                SerializerKind kind = catalog.KindOf(type);
                SaveMode mode = saveable.ModeOf(entry.Key);
                // Without a serializer a component is saved only once it has a mode
                bool saved = saveable.IsSaved(entry.Key) && (kind != SerializerKind.None || mode != SaveMode.Auto);
                var options = (catalog.For(type) as ISerializerOptions)?.Options;
                rows.Add(new Row(entry.Key, type, kind, saved, mode, saveable.MembersOf(entry.Key),
                    mode == SaveMode.Auto ? options : null, saveable.OptionsOf(entry.Key)));
            }

            return rows;
        }

        /// <summary>The checkbox: a component with a serializer is excluded or not; one without gets All Fields or no mode.</summary>
        public static void SetChecked(Saveable saveable, string key, SerializerKind kind, bool on)
        {
            if (kind != SerializerKind.None)
            {
                saveable.SetSaved(key, on);
                return;
            }

            if (on)
            {
                saveable.SetMode(key, SaveMode.AllFields);
            }
            else
            {
                saveable.SetMode(key, SaveMode.Auto);
                // Old way: back among "everything unless unchecked" (nothing saves it); explicit list: off the list
                saveable.SetSaved(key, !saveable.IsExplicit);
            }
        }

        /// <summary>Every Saveable of the open scenes and of the project's prefabs that is not on an explicit list yet.</summary>
        [MenuItem("Tools/Save System/Switch Saveables To Explicit Lists")]
        public static void SwitchAllToExplicitLists()
        {
            int switched = 0;
            for (int index = 0; index < UnityEngine.SceneManagement.SceneManager.sceneCount; index++)
            {
                UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(index);
                if (!scene.isLoaded)
                    continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Saveable saveable in root.GetComponentsInChildren<Saveable>(true))
                    {
                        // A prefab's own Saveable is switched in the prefab below
                        if (saveable.IsExplicit || PrefabUtility.GetCorrespondingObjectFromSource(saveable) != null)
                            continue;

                        Change(saveable, "Explicit List", () => saveable.UseExplicitList());
                        switched++;
                    }
                }
            }

            string module = ModuleFolder();
            foreach (string path in SaveCatalogBuilder.PrefabPathsWithSaveable())
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                // The module's own demo and test prefabs stay as they are: its tests cover the old way too
                if (path.StartsWith("Packages/", StringComparison.Ordinal) || asset == null
                    || (module.Length > 0 && path.StartsWith(module, StringComparison.Ordinal))
                    || asset.GetComponentsInChildren<Saveable>(true).All(saveable => saveable.IsExplicit))
                    continue;

                using var scope = new PrefabUtility.EditPrefabContentsScope(path);
                foreach (Saveable saveable in scope.prefabContentsRoot.GetComponentsInChildren<Saveable>(true))
                {
                    if (saveable.IsExplicit)
                        continue;

                    saveable.UseExplicitList();
                    switched++;
                }
            }

            Debug.Log($"Save System: {switched} Saveable(s) switched to an explicit list of components.");
        }

        /// <summary>The folder the module lies in ("…/SaveSystem/"), found by this script; empty when it cannot be told.</summary>
        private static string ModuleFolder()
        {
            const string TAIL = "/Editor/" + nameof(SaveableEditor) + ".cs";
            foreach (string guid in AssetDatabase.FindAssets(nameof(SaveableEditor) + " t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(TAIL, StringComparison.Ordinal))
                    return path.Substring(0, path.Length - TAIL.Length + 1);
            }

            return string.Empty;
        }

        public override void OnInspectorGUI()
        {
            var saveable = (Saveable)target;
            DrawId(saveable);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Saved components", EditorStyles.boldLabel);
            if (saveable.IsExplicit)
            {
                EditorGUILayout.HelpBox("Only what is checked is saved. Auto uses the component's serializer; All Fields " +
                                        "saves everything it can; Selected saves only the members you pick.", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("Old setup: everything that can be saved is saved unless unchecked — children, " +
                                        "particles and sounds too, so the save grows. An explicit list keeps the object's " +
                                        "own Transform, body and scripts and whatever was given a mode here.", MessageType.Info);
                if (GUILayout.Button("Switch To An Explicit List"))
                    Change(saveable, "Explicit List", () => saveable.UseExplicitList());
            }

            foreach (Row row in BuildRows(saveable, ComponentSerializerCatalog.Default))
                DrawRow(saveable, row);

            DrawIdWarnings(saveable);
        }

        private void DrawId(Saveable saveable)
        {
            serializedObject.Update();
            SerializedProperty id = serializedObject.FindProperty("_id");
            SerializedProperty prefabId = serializedObject.FindProperty("_prefabId");
            bool prefabSide = SaveableIdAssigner.IsPrefabSide(saveable);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("ID", prefabSide ? "given to each instance in a scene" : id.stringValue);
                using (new EditorGUI.DisabledScope(prefabSide || Application.isPlaying))
                {
                    if (GUILayout.Button("New ID", GUILayout.Width(70f)))
                        id.stringValue = SaveRegistry.NewId();
                }
            }

            EditorGUILayout.LabelField("Prefab", string.IsNullOrEmpty(prefabId.stringValue) ? "-" : prefabId.stringValue);
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("In play", saveable.IsSpawned ? "Spawned" : "Scene object");
                // Added in Play Mode by hand (or made by code without a prefab): it is not in the scene file, Unity drops
                // it when Play ends, and the next Play has nothing to load its data into
                if (saveable.IsSpawned && string.IsNullOrEmpty(prefabId.stringValue))
                    EditorGUILayout.HelpBox("This Saveable is not part of the scene: added in Play Mode it is gone when "
                        + "Play ends, and nothing of it is loaded next time. Add it outside Play Mode and save the scene.",
                        MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRow(Saveable saveable, Row row)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool saved = EditorGUILayout.ToggleLeft(new GUIContent(row.Key, row.Type.FullName), row.Saved);
                if (saved != row.Saved)
                    Change(saveable, "Save Component", () => SetChecked(saveable, row.Key, row.Kind, saved));

                using (new EditorGUI.DisabledScope(!row.Saved))
                    DrawMode(saveable, row);
            }

            if (!row.Saved)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (string option in row.Options)
                {
                    bool on = EditorGUILayout.ToggleLeft(option, row.IsOn(option));
                    if (on != row.IsOn(option))
                        Change(saveable, "Serializer Option", () => saveable.SetOption(row.Key, option, on));
                }

                if (row.Mode == SaveMode.Selected)
                    DrawMembers(saveable, row);
                else if (row.Mode == SaveMode.AllFields)
                    EditorGUILayout.LabelField(new GUIContent(Describe(SaveMembers.AllWithReferences(row.Type)),
                        "Members marked (ref) hold Unity objects: saved when they point at Saveables, their children, " +
                        "prefabs with a Saveable or assets of the settings' folders; otherwise left as the scene has them."),
                        EditorStyles.miniLabel);
            }
        }

        private static void DrawMode(Saveable saveable, Row row)
        {
            bool canAuto = row.Kind != SerializerKind.None;
            string[] labels = canAuto ? AllModes.Select((label, index) => index == 0 ? $"Auto ({Describe(row.Kind)})" : label).ToArray()
                : ModesWithoutAuto;
            int current = canAuto ? (int)row.Mode : Math.Max(0, (int)row.Mode - 1);
            int picked = EditorGUILayout.Popup(current, labels, GUILayout.Width(160f));
            if (picked == current)
                return;

            var mode = (SaveMode)(canAuto ? picked : picked + 1);
            Change(saveable, "Save Mode", () => saveable.SetMode(row.Key, mode));
        }

        private void DrawMembers(Saveable saveable, Row row)
        {
            IReadOnlyList<SaveMember> candidates = row.Candidates;
            bool expanded = _expanded.Contains(row.Key) || row.Members.Count == 0;
            bool open = EditorGUILayout.Foldout(expanded, $"Members ({row.Members.Count} of {candidates.Count})", true);
            if (open != expanded)
            {
                if (open)
                    _expanded.Add(row.Key);
                else
                    _expanded.Remove(row.Key);
            }

            if (!open)
                return;

            if (candidates.Count == 0)
            {
                EditorGUILayout.HelpBox("This component has no members that can be saved.", MessageType.Info);
                return;
            }

            var picked = new HashSet<string>(row.Members);
            using (new EditorGUI.IndentLevelScope())
            {
                foreach (SaveMember member in candidates)
                {
                    string label = $"{member.Name}   {TypeName(member.Type)}{(member.IsReference ? "  (reference)" : string.Empty)}";
                    bool on = EditorGUILayout.ToggleLeft(label, picked.Contains(member.Name));
                    if (on == picked.Contains(member.Name))
                        continue;

                    if (on)
                        picked.Add(member.Name);
                    else
                        picked.Remove(member.Name);

                    // Keep the order of the candidates: the saved data reads the same every time
                    List<string> members = candidates.Select(candidate => candidate.Name).Where(picked.Contains).ToList();
                    Change(saveable, "Save Members", () => saveable.SetMembers(row.Key, members));
                }
            }
        }

        private static void DrawIdWarnings(Saveable saveable)
        {
            if (SaveableIdAssigner.IsPrefabSide(saveable) || Application.isPlaying)
                return;

            if (string.IsNullOrEmpty(saveable.Id))
                EditorGUILayout.HelpBox("No id yet: it is given in a moment.", MessageType.Warning);
            else if (SaveableIdAssigner.IsUsedElsewhere(saveable, saveable.Id))
                EditorGUILayout.HelpBox("Another Saveable in the open scenes has this id: press New ID.", MessageType.Error);
        }

        private static void Change(Saveable saveable, string name, Action change)
        {
            Undo.RecordObject(saveable, name);
            change();
            EditorUtility.SetDirty(saveable);
            PrefabUtility.RecordPrefabInstancePropertyModifications(saveable);
        }

        private static string Describe(IReadOnlyList<SaveMember> members) =>
            members.Count == 0
                ? "nothing to save"
                : string.Join(", ", members.Select(member => member.IsReference ? member.Name + " (ref)" : member.Name));

        private static string Describe(SerializerKind kind) => kind switch
        {
            SerializerKind.Own => "own serializer",
            SerializerKind.BuiltIn => "built-in",
            SerializerKind.Fields => "[Save] fields",
            _ => "nothing"
        };

        private static string TypeName(Type type)
        {
            if (!type.IsGenericType)
                return type.Name;

            string name = type.Name.Substring(0, type.Name.IndexOf('`'));
            return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(TypeName))}>";
        }
    }
}
