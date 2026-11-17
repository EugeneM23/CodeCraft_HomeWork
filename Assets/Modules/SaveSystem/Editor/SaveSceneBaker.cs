using System;
using System.Collections.Generic;
using System.Linq;
using Saving.Objects;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Saving.Editor
{
    /// <summary>
    /// Keeps the lists of a save system set to Whole Scene: the objects of its scene with their ids, and the prefabs
    /// the scene can spawn. Runs as the scene is saved, as Play starts, before a build and when the component is
    /// added. An object keeps its id for good once listed (renaming or moving it changes nothing); a new object gets
    /// one from its place in the hierarchy, so the same scene gives the same ids everywhere. Left out: the save
    /// system's own object, objects with a Saveable (and everything under them — they save themselves), Static objects
    /// if asked, the Ignored ones with their children, editor-only objects.
    /// </summary>
    [InitializeOnLoad]
    public static class SaveSceneBaker
    {
        private const int ID_LENGTH = 12;

        static SaveSceneBaker()
        {
            SaveLoadSystem.EditorBake = system => Bake(system);
            EditorSceneManager.sceneSaving += (scene, _) => BakeScene(scene);
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.ExitingEditMode)
                    return;

                for (int index = 0; index < SceneManager.sceneCount; index++)
                    BakeScene(SceneManager.GetSceneAt(index));
            };
        }

        public static void BakeScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (SaveLoadSystem system in root.GetComponentsInChildren<SaveLoadSystem>(true))
                    Bake(system);
            }
        }

        /// <summary>Brings the lists of the system up to date; true when they changed.</summary>
        public static bool Bake(SaveLoadSystem system)
        {
            if (system == null || PrefabUtility.IsPartOfPrefabAsset(system)
                               || PrefabStageUtility.GetPrefabStage(system.gameObject) != null)
                return false;

            bool whole = system.Scope == SaveScope.WholeScene;
            List<SceneObjectId> objects = whole ? CollectObjects(system) : new List<SceneObjectId>();
            List<ScenePrefab> prefabs = whole ? CollectPrefabs(system.gameObject.scene) : new List<ScenePrefab>();
            if (Same(system, objects, prefabs))
                return false;

            system.SetSceneTable(objects, prefabs);
            // In play and in a build the scene in hand is a copy: nothing to mark
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !BuildPipeline.isBuildingPlayer)
            {
                EditorUtility.SetDirty(system);
                if (PrefabUtility.IsPartOfPrefabInstance(system))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(system);
            }

            return true;
        }

        /// <summary>The objects of the system's scene the whole-scene scope saves, old ids kept.</summary>
        public static List<SceneObjectId> CollectObjects(SaveLoadSystem system)
        {
            var known = new Dictionary<GameObject, string>();
            foreach (SceneObjectId entry in system.SceneObjects)
            {
                if (entry?.Object != null && !string.IsNullOrEmpty(entry.Id))
                    known[entry.Object] = entry.Id;
            }

            var used = new HashSet<string>(known.Values, StringComparer.Ordinal);
            var ignored = new HashSet<Transform>(system.Ignored.Where(item => item != null).Select(item => item.transform));
            var result = new List<SceneObjectId>();
            foreach (GameObject root in system.gameObject.scene.GetRootGameObjects())
                Walk(root.transform, system, known, used, ignored, result);

            return result;
        }

        private static void Walk(Transform node, SaveLoadSystem system, Dictionary<GameObject, string> known,
            HashSet<string> used, HashSet<Transform> ignored, List<SceneObjectId> result)
        {
            GameObject target = node.gameObject;
            if (ignored.Contains(node) || (target.hideFlags & HideFlags.DontSave) != 0 || target.CompareTag("EditorOnly")
                || target.TryGetComponent(out Saveable _))
                return;

            if (target != system.gameObject && !(system.SkipStatic && target.isStatic))
            {
                if (!known.TryGetValue(target, out string id))
                    id = NewId(node, used);

                result.Add(new SceneObjectId { Object = target, Id = id });
            }

            foreach (Transform child in node)
                Walk(child, system, known, used, ignored, result);
        }

        /// <summary>
        /// The prefabs an object made in play in this scene may be a copy of: those the scene depends on and those in
        /// Resources folders (both are in a build anyway), if they carry state (a script, a body) and have no Saveable.
        /// </summary>
        public static List<ScenePrefab> CollectPrefabs(Scene scene)
        {
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(scene.path))
            {
                foreach (string path in AssetDatabase.GetDependencies(scene.path, true))
                {
                    if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                        paths.Add(path);
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Resources/"))
                    paths.Add(path);
            }

            ComponentSerializerCatalog catalog = ComponentSerializerCatalog.Default;
            var result = new List<ScenePrefab>();
            foreach (string path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.TryGetComponent(out Saveable _)
                    || (!SavePolicy.HasState(prefab, catalog) && !SavePolicy.IsEffect(prefab, catalog)))
                    continue;

                result.Add(new ScenePrefab { Id = AssetDatabase.AssetPathToGUID(path), Prefab = prefab });
            }

            return result;
        }

        private static string NewId(Transform node, HashSet<string> used)
        {
            string path = ComponentKeys.ScenePathOf(node);
            string id = Hash(path);
            for (int attempt = 1; !used.Add(id); attempt++)
                id = Hash($"{path}~{attempt}");

            return id;
        }

        // FNV-1a, 64 bit: the same text gives the same id on every machine
        private static string Hash(string text)
        {
            ulong hash = 14695981039346656037UL;
            foreach (char symbol in text)
            {
                hash ^= symbol;
                hash *= 1099511628211UL;
            }

            return hash.ToString("x16").Substring(0, ID_LENGTH);
        }

        private static bool Same(SaveLoadSystem system, List<SceneObjectId> objects, List<ScenePrefab> prefabs)
        {
            IReadOnlyList<SceneObjectId> oldObjects = system.SceneObjects;
            IReadOnlyList<ScenePrefab> oldPrefabs = system.ScenePrefabs;
            if (oldObjects.Count != objects.Count || oldPrefabs.Count != prefabs.Count)
                return false;

            for (int index = 0; index < objects.Count; index++)
            {
                if (oldObjects[index] == null || oldObjects[index].Object != objects[index].Object
                                              || oldObjects[index].Id != objects[index].Id)
                    return false;
            }

            for (int index = 0; index < prefabs.Count; index++)
            {
                if (oldPrefabs[index] == null || oldPrefabs[index].Prefab != prefabs[index].Prefab
                                              || oldPrefabs[index].Id != prefabs[index].Id)
                    return false;
            }

            return true;
        }
    }

    /// <summary>Scenes that load in play in the editor and scenes of a build: their lists are made complete.</summary>
    internal sealed class SaveSceneBuildStep : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report) => SaveSceneBaker.BakeScene(scene);
    }
}
