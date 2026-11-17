using System.IO;
using System.Linq;
using Saving.Objects;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Saving.Editor
{
    /// <summary>Tools → Save System: settings, catalogs, scene check, the saves folder, deleting every save.</summary>
    public static class SaveSystemMenu
    {
        public static string SavesFolder => Path.Combine(Application.persistentDataPath, SaveLoadSystem.DEFAULT_FOLDER);

        [MenuItem("Tools/Save System/Open Saves Folder")]
        public static void OpenSavesFolder()
        {
            Directory.CreateDirectory(SavesFolder);
            EditorUtility.RevealInFinder(SavesFolder);
        }

        [MenuItem("Tools/Save System/Delete All Saves")]
        public static void DeleteAllSaves()
        {
            if (!Directory.Exists(SavesFolder))
            {
                Debug.Log("There are no saves to delete.");
                return;
            }

            string warning = EditorApplication.isPlaying
                ? "\n\nPlay Mode is running: the save system keeps its snapshot in memory and writes the slot again on the next save."
                : string.Empty;

            if (!EditorUtility.DisplayDialog("Delete All Saves", $"Delete every save in\n{SavesFolder}?{warning}",
                    "Delete", "Cancel"))
                return;

            Directory.Delete(SavesFolder, recursive: true);
            Debug.Log($"Deleted all saves in {SavesFolder}.");
        }

        [MenuItem("Tools/Save System/Create Settings")]
        public static void CreateSettings()
        {
            SaveSystemSettings existing = SaveCatalogBuilder.AllSettings().FirstOrDefault();
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            string path = EditorUtility.SaveFilePanelInProject("Save System Settings", "SaveSystemSettings", "asset",
                "Where to keep the save settings; set the asset on SaveLoadSystem.");
            if (string.IsNullOrEmpty(path))
                return;

            var settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
            AssetDatabase.CreateAsset(settings, path);
            SaveCatalogBuilder.Rebuild(settings);
            Selection.activeObject = settings;
        }

        [MenuItem("Tools/Save System/Rebuild Catalogs")]
        public static void RebuildCatalogs()
        {
            int baked = SaveCatalogBuilder.BakePrefabIds();
            foreach (SaveSystemSettings settings in SaveCatalogBuilder.AllSettings())
                SaveCatalogBuilder.Rebuild(settings);

            Debug.Log($"Save catalogs rebuilt; prefab ids fixed in {baked} prefab(s).");
        }

        [MenuItem("Tools/Save System/Validate Scenes")]
        public static void ValidateScenes()
        {
            var paths = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToList();
            for (int index = 0; index < SceneManager.sceneCount; index++)
                paths.Add(SceneManager.GetSceneAt(index).path);

            var problems = SaveValidator.ValidateScenes(paths.Where(path => !string.IsNullOrEmpty(path)));
            foreach (SaveSystemSettings settings in SaveCatalogBuilder.AllSettings())
                problems.AddRange(SaveValidator.ValidateCatalogs(settings));

            foreach (string problem in problems)
                Debug.LogError(problem);

            if (problems.Count == 0)
                Debug.Log("Save System: no problems found in the scenes and catalogs.");
        }
    }
}
