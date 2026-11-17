using System;
using System.Collections.Generic;
using System.Linq;
using Saving.Objects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Saving.Editor
{
    /// <summary>
    /// Play started with unsaved changes in a scene that is saved: a load that reloads the scene would take the file on
    /// disk — an older scene, with other objects and ids — and the saved state would land on the wrong things (a
    /// reference not found, a null in a list of the game). So the scene is written as it stands to a copy next to the
    /// project's assets, loads go to the copy (<see cref="SaveSceneCopies"/>), and the copy is deleted when Play ends.
    /// The scene itself is neither saved nor touched.
    /// </summary>
    [InitializeOnLoad]
    public static class SavePlaySceneCopies
    {
        /// <summary>Where the copies live while Play runs; the catalogs of SaveSystemSettings leave it out.</summary>
        public const string FOLDER = "Assets/__SaveSystemPlayCopies";

        private const string SESSION_KEY = "SaveSystem.PlaySceneCopies";

        static SavePlaySceneCopies()
        {
            // After a domain reload on the way into Play the list made before it is gone: put it back
            Restore();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static bool IsCopy(string path) => path != null && path.StartsWith(FOLDER + "/", StringComparison.Ordinal);

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
                    DeleteAll();
                    for (int index = 0; index < SceneManager.sceneCount; index++)
                        Make(SceneManager.GetSceneAt(index));

                    Remember();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    DeleteAll();
                    break;
            }
        }

        /// <summary>Writes the copy of a scene that needs one; true when it did.</summary>
        public static bool Make(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path) || IsCopy(scene.path))
                return false;

            // The lists of a whole-scene save system first: the copy must hold them as this Play has them
            SaveSceneBaker.BakeScene(scene);
            if (!scene.isDirty || !IsSaved(scene))
                return false;

            // A folder per scene keeps the scene's own name, which a game may look at
            string folder = $"{FOLDER}/{AssetDatabase.AssetPathToGUID(scene.path)}";
            if (!AssetDatabase.IsValidFolder(FOLDER))
                AssetDatabase.CreateFolder("Assets", FOLDER.Substring("Assets/".Length));
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(FOLDER, folder.Substring(FOLDER.Length + 1));

            string copy = $"{folder}/{scene.name}.unity";
            if (!EditorSceneManager.SaveScene(scene, copy, saveAsCopy: true))
            {
                Debug.LogWarning($"Save System: the scene '{scene.name}' has unsaved changes and its play copy could not be " +
                                 "written. A load in this Play session reloads the scene from disk, without those changes: " +
                                 "save the scene before Play.");
                return false;
            }

            SaveSceneCopies.Set(scene.path, copy);
            return true;
        }

        /// <summary>The scene has something a save system keeps: a save system of its own or a Saveable.</summary>
        private static bool IsSaved(Scene scene) =>
            scene.GetRootGameObjects().Any(root => root.GetComponentInChildren<SaveLoadSystem>(true) != null
                                                   || root.GetComponentInChildren<Saveable>(true) != null);

        private static void DeleteAll()
        {
            SaveSceneCopies.Clear();
            SessionState.EraseString(SESSION_KEY);
            if (AssetDatabase.IsValidFolder(FOLDER))
                AssetDatabase.DeleteAsset(FOLDER);
        }

        private static void Remember() =>
            SessionState.SetString(SESSION_KEY,
                string.Join("\n", SaveSceneCopies.Copies.Select(pair => pair.Key + "\t" + pair.Value)));

        private static void Restore()
        {
            string saved = SessionState.GetString(SESSION_KEY, string.Empty);
            if (saved.Length == 0)
                return;

            foreach (string line in saved.Split('\n'))
            {
                string[] parts = line.Split('\t');
                if (parts.Length == 2 && AssetDatabase.LoadAssetAtPath<SceneAsset>(parts[1]) != null)
                    SaveSceneCopies.Set(parts[0], parts[1]);
            }
        }
    }
}
