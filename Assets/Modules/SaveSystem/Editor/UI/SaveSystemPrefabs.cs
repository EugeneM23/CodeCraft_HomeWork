using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Saving.UI.Editor
{
    /// <summary>
    /// The module's prefabs and the GameObject → Save System menu: Save System (the drop-in SaveLoadSystem; buttons
    /// call it straight) and Save Slots Menu (with an EventSystem if the scene has none). Tools → Save System → Rebuild
    /// Prefabs writes Prefabs/SaveSystem.prefab and Prefabs/SaveSlotsMenu.prefab next to the module's code.
    /// </summary>
    public static class SaveSystemPrefabs
    {
        public const string SYSTEM_PREFAB = "SaveSystem.prefab";
        public const string MENU_PREFAB = "SaveSlotsMenu.prefab";

        /// <summary>The module's Prefabs folder, found from the SaveLoadSystem script wherever the module was copied.</summary>
        public static string PrefabsFolder
        {
            get
            {
                string script = AssetDatabase.FindAssets("t:MonoScript SaveLoadSystem")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(path => path.EndsWith("/Runtime/Core/SaveLoadSystem.cs"));
                string root = script == null ? "Assets/SaveSystem" : script.Substring(0, script.Length - "/Runtime/Core/SaveLoadSystem.cs".Length);
                return root + "/Prefabs";
            }
        }

        public static GameObject SystemPrefab => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/{SYSTEM_PREFAB}");
        public static GameObject MenuPrefab => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/{MENU_PREFAB}");

        /// <summary>A new SaveSystem object: a SaveLoadSystem that loads its slot on start.</summary>
        public static SaveLoadSystem BuildSystem()
        {
            var go = new GameObject("SaveSystem");
            var system = go.AddComponent<SaveLoadSystem>();
            var serialized = new SerializedObject(system);
            serialized.FindProperty("_loadOnStart").boolValue = true;
            // The prefab is the precise way in: only objects with a Saveable, no keys. The component added by hand to a
            // scene is the lazy one (Whole Scene)
            serialized.FindProperty("_scope").enumValueIndex = (int)SaveScope.Saveables;
            serialized.FindProperty("_hotkeys").boolValue = false;
            serialized.FindProperty("_sceneObjects").ClearArray();
            serialized.FindProperty("_scenePrefabs").ClearArray();
            serialized.FindProperty("_settings").objectReferenceValue = null; // each project's own, set in its scenes
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return system;
        }

        [MenuItem("Tools/Save System/Rebuild Prefabs")]
        public static void RebuildPrefabs()
        {
            string folder = PrefabsFolder;
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            Save(BuildSystem().gameObject, $"{folder}/{SYSTEM_PREFAB}");
            Save(SaveSlotsMenuBuilder.Build().gameObject, $"{folder}/{MENU_PREFAB}");
            AssetDatabase.SaveAssets();
            Debug.Log($"Save System prefabs written to {folder}.");
        }

        [MenuItem("GameObject/Save System/Save System", false, 10)]
        private static void CreateSystem(MenuCommand command) =>
            Place(SystemPrefab, () => BuildSystem().gameObject, command, "Create Save System");

        [MenuItem("GameObject/Save System/Save Slots Menu", false, 11)]
        private static void CreateMenu(MenuCommand command)
        {
            Place(MenuPrefab, () => SaveSlotsMenuBuilder.Build().gameObject, command, "Create Save Slots Menu");
            SaveSlotsMenuBuilder.EnsureEventSystem();
        }

        private static void Place(GameObject prefab, System.Func<GameObject> build, MenuCommand command, string undo)
        {
            GameObject go = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab) : build();
            GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, undo);
            Selection.activeObject = go;
        }

        private static void Save(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
    }
}
