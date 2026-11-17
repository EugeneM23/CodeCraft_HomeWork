using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AIKit.Editor
{
    /// <summary>
    /// Step 90 (C18): the scene objects a character used to keep for its graph's variables (its legacy bindings: a house,
    /// a work place, a patrol zone, a turret) become objects of the world that say themselves whose they are — each bound
    /// object gets an AI Object of the variable's name as its kind, with the character among its owners, and the binding
    /// is cleared. A graph then finds it with Find (What: that kind, mine). Bindings to variables nobody reads any more
    /// (Point A, Point B) are only cleared. Saves nothing: the caller looks and saves.
    /// </summary>
    public static class SceneObjectsMigration
    {
        /// <summary>The variables whose objects nobody reads: their bindings are dropped, no AI Object is made.</summary>
        public static readonly string[] Unread = { "Point A", "Point B" };

        /// <summary>The open scenes: every character's bindings become AI Objects (the scenes are not saved).</summary>
        [MenuItem("Tools/AI Kit/Migrate Scene Objects To AI Objects")]
        public static void MigrateOpenScenes()
        {
            int moved = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                moved += Migrate(SceneManager.GetSceneAt(i));
            Debug.Log($"AI Kit — Migrate Scene Objects To AI Objects: {moved} scene objects are AI Objects with owners " +
                      "now (look, then save the scenes)");
        }

        /// <summary>Every character of the scene: its bindings become AI Objects. The number of objects given owners.</summary>
        public static int Migrate(Scene scene)
        {
            int moved = 0;
            if (!scene.IsValid())
                return moved;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (AICharacter character in root.GetComponentsInChildren<AICharacter>(true))
                    moved += Migrate(character);
            }

            if (moved > 0)
                EditorSceneManager.MarkSceneDirty(scene);
            return moved;
        }

        /// <summary>The character's bindings become AI Objects owned by it; the bindings are cleared. How many were moved.</summary>
        public static int Migrate(AICharacter character)
        {
            if (character == null)
                return 0;

            var fields = new SerializedObject(character);
            SerializedProperty bindings = fields.FindProperty("_bindings");
            if (bindings == null || !bindings.isArray || bindings.arraySize == 0)
                return 0;

            int moved = 0;
            for (int i = 0; i < bindings.arraySize; i++)
            {
                SerializedProperty binding = bindings.GetArrayElementAtIndex(i);
                string variable = binding.FindPropertyRelative(nameof(ObjectBinding.Variable)).stringValue;
                var target = binding.FindPropertyRelative(nameof(ObjectBinding.Object)).objectReferenceValue as Transform;
                if (target == null || string.IsNullOrEmpty(variable) || System.Array.IndexOf(Unread, variable) >= 0)
                    continue;

                // An object of the world already (a turret, a cover, a perch): it becomes the character's own, no marker
                // beside it (a marker on a turret would be found instead of the turret, and offers none of its uses)
                WorldObject owned = target.TryGetComponent(out WorldObject existing) && existing is not AIObject
                    ? existing
                    : MarkerOn(target, variable);
                if (!owned.IsOwnedBy(character))
                {
                    Undo.RecordObject(owned, "Migrate Scene Objects");
                    owned.Owners.Add(character);
                    EditorUtility.SetDirty(owned);
                }

                moved++;
            }

            Undo.RecordObject(character, "Migrate Scene Objects");
            bindings.ClearArray();
            fields.ApplyModifiedProperties();
            EditorUtility.SetDirty(character);
            return moved;
        }

        // The object's AI Object of the kind (made when it has none). An object that is an AI Object of another kind
        // already keeps it: one object, one kind
        private static AIObject MarkerOn(Transform target, string kind)
        {
            if (target.TryGetComponent(out AIObject marker))
            {
                if (marker.Kind != kind)
                    Debug.LogWarning($"Migrate Scene Objects: '{target.name}' is an AI Object of the kind '{marker.Kind}' " +
                                     $"already — bound as '{kind}' too; it keeps '{marker.Kind}'", target);
                return marker;
            }

            marker = Undo.AddComponent<AIObject>(target.gameObject);
            marker.Kind = kind;
            EditorUtility.SetDirty(marker);
            return marker;
        }
    }

    /// <summary>
    /// The kinds of AI Object the project uses — what a What node, a Zone shape and an AI Object offer to pick from (step
    /// 90, C5: a key is picked from a list, the list comes from the project): the kinds of the AI Objects of the open
    /// scenes and of the project's prefabs. A scan, not a list kept: a node draws its picker from NodeOptions, which keeps
    /// it.
    /// </summary>
    public static class ObjectKinds
    {
        /// <summary>The kinds, sorted, each once.</summary>
        public static List<string> Known() => Merged(InScenes(), InPrefabs());

        /// <summary>The kinds of the two lists together, sorted, each once, none empty.</summary>
        public static List<string> Merged(IEnumerable<string> first, IEnumerable<string> second)
        {
            var kinds = new SortedSet<string>(System.StringComparer.Ordinal);
            foreach (string kind in first)
            {
                if (!string.IsNullOrEmpty(kind))
                    kinds.Add(kind);
            }

            foreach (string kind in second)
            {
                if (!string.IsNullOrEmpty(kind))
                    kinds.Add(kind);
            }

            return new List<string>(kinds);
        }

        /// <summary>The kinds of the AI Objects of the open scenes.</summary>
        public static List<string> InScenes()
        {
            var kinds = new List<string>();
            foreach (AIObject marker in Object.FindObjectsByType<AIObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                kinds.Add(marker.Kind);
            return kinds;
        }

        /// <summary>
        /// The kinds of the AI Objects of the project's prefabs. Only the prefabs that use the component are loaded (asked
        /// by their dependencies: a project with packs of thousands of prefabs is not loaded whole).
        /// </summary>
        public static List<string> InPrefabs()
        {
            var kinds = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!System.Array.Exists(AssetDatabase.GetDependencies(path, false),
                        dependency => dependency.EndsWith("/" + nameof(AIObject) + ".cs", System.StringComparison.Ordinal)))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                foreach (AIObject marker in prefab.GetComponentsInChildren<AIObject>(true))
                    kinds.Add(marker.Kind);
            }

            return kinds;
        }
    }

    /// <summary>
    /// The kind of an AI Object (ObjectKind): typed on the object, or picked from the kinds the project's AI Objects use.
    /// </summary>
    [CustomPropertyDrawer(typeof(ObjectKindAttribute))]
    public sealed class ObjectKindDrawer : PropertyDrawer
    {
        private const float PICK_WIDTH = 22f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var text = new Rect(position.x, position.y, position.width - PICK_WIDTH, position.height);
            var pick = new Rect(position.xMax - PICK_WIDTH + 2f, position.y, PICK_WIDTH - 2f, position.height);
            EditorGUI.PropertyField(text, property, label);
            if (!GUI.Button(pick, "▾", EditorStyles.miniButton))
                return;

            var menu = new GenericMenu();
            string current = property.stringValue;
            foreach (string kind in ObjectKinds.Known())
            {
                string chosen = kind;
                menu.AddItem(new GUIContent(kind), kind == current, () =>
                {
                    property.serializedObject.Update();
                    property.stringValue = chosen;
                    property.serializedObject.ApplyModifiedProperties();
                });
            }

            if (menu.GetItemCount() == 0)
                menu.AddDisabledItem(new GUIContent("(no AI Object kinds in the project yet)"));
            menu.DropDown(pick);
        }
    }
}
