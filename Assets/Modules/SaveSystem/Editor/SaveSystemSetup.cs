using System;
using System.IO;
using System.Linq;
using Saving.Objects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Saving.Editor
{
    /// <summary>
    /// The "drop it in and it works" part of the editor. Adding a Saveable makes sure the project has its
    /// SaveSystemSettings — the one that exists, or a new one in Assets/Settings/Resources, where a scene without a
    /// SaveSystem finds it in play (an asset elsewhere is used as it is; the settings Inspector says when play will not
    /// find it). A SaveLoadSystem that saves scene objects gets the asset in its field when added to a scene; prefab
    /// assets and prefab mode keep no reference: their instances in scenes get it.
    /// </summary>
    [InitializeOnLoad]
    public static class SaveSystemSetup
    {
        public const string NEW_SETTINGS_PATH = "Assets/Settings/Resources/" + SaveSystemSettings.RESOURCES_NAME + ".asset";

        static SaveSystemSetup()
        {
            SaveLoadSystem.EditorAdded = system => AssignSettings(system);
            SaveLoadSystem.EditorValidate = system =>
            {
                if (system == null || system.HasSettings || !system.SavesSceneObjects)
                    return;

                // OnValidate must not change serialized data: do it right after
                EditorApplication.delayCall += () => AssignSettings(system);
            };
            // Shared with the id assigner: both add to it
            Saveable.EditorAdded += EnsureSettingsFor;
        }

        /// <summary>Gives the system the project's settings if it saves scene objects and has none; true when it did.</summary>
        public static bool AssignSettings(SaveLoadSystem system)
        {
            if (system == null || EditorApplication.isPlayingOrWillChangePlaymode || !system.SavesSceneObjects
                || system.HasSettings || PrefabUtility.IsPartOfPrefabAsset(system)
                || PrefabStageUtility.GetPrefabStage(system.gameObject) != null)
                return false;

            var serialized = new SerializedObject(system);
            serialized.FindProperty("_settings").objectReferenceValue = FindOrCreateSettings();
            serialized.ApplyModifiedProperties();
            return true;
        }

        /// <summary>A Saveable just added: the project gets its settings now (see the class summary).</summary>
        public static void EnsureSettingsFor(Saveable saveable)
        {
            // A hidden helper object (SaveCatalogBuilder learns the Saveable script path through one) is not an added
            // Saveable: it would re-enter FindOrCreateSettings, and a rebuild would create the asset in a project that
            // has none
            if (saveable == null || EditorApplication.isPlayingOrWillChangePlaymode
                || (saveable.gameObject.hideFlags & HideFlags.HideAndDontSave) == HideFlags.HideAndDontSave)
                return;

            FindOrCreateSettings();
        }

        /// <summary>Whether a scene without a SaveSystem finds settings at this path in play: right in a Resources folder, by name.</summary>
        public static bool IsRuntimeSettingsPath(string assetPath) =>
            !string.IsNullOrEmpty(assetPath)
            && assetPath.EndsWith("/Resources/" + SaveSystemSettings.RESOURCES_NAME + ".asset", StringComparison.Ordinal);

        /// <summary>The project's SaveSystemSettings; created with fresh catalogs when there is none.</summary>
        public static SaveSystemSettings FindOrCreateSettings()
        {
            SaveSystemSettings existing = SaveCatalogBuilder.AllSettings().FirstOrDefault();
            if (existing != null)
                return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(NEW_SETTINGS_PATH) ?? "Assets");
            AssetDatabase.Refresh();
            var settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
            AssetDatabase.CreateAsset(settings, NEW_SETTINGS_PATH);
            SaveCatalogBuilder.Rebuild(settings);
            Debug.Log($"Save System: created {NEW_SETTINGS_PATH} for saving scene objects.", settings);
            return settings;
        }
    }
}
