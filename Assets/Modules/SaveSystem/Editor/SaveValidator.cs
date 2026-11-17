using System.Collections.Generic;
using System.Linq;
using Saving.Objects;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Saving.Editor
{
    /// <summary>Finds what breaks saves before the game does: empty and repeated ids in scenes, stale catalogs.</summary>
    public static class SaveValidator
    {
        public static List<string> ValidateScene(Scene scene)
        {
            var problems = new List<string>();
            var seen = new Dictionary<string, Saveable>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Saveable saveable in root.GetComponentsInChildren<Saveable>(true))
                {
                    string path = ComponentKeys.ScenePathOf(saveable.transform);
                    if (string.IsNullOrEmpty(saveable.Id))
                        problems.Add($"{scene.path}: '{path}' has no id");
                    else if (seen.TryGetValue(saveable.Id, out Saveable first))
                        problems.Add($"{scene.path}: '{path}' repeats the id of '{ComponentKeys.ScenePathOf(first.transform)}'");
                    else
                        seen.Add(saveable.Id, saveable);
                }
            }

            return problems;
        }

        /// <summary>Checks scenes by path; scenes that are not open are opened additively and closed again.</summary>
        public static List<string> ValidateScenes(IEnumerable<string> paths)
        {
            var problems = new List<string>();
            foreach (string path in paths.Distinct())
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.isLoaded;
                if (opened)
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

                problems.AddRange(ValidateScene(scene));
                if (opened)
                    EditorSceneManager.CloseScene(scene, removeScene: true);
            }

            return problems;
        }

        public static List<string> ValidateCatalogs(SaveSystemSettings settings)
        {
            var problems = new List<string>();
            List<SaveSystemSettings.PrefabEntry> prefabs = SaveCatalogBuilder.BuildPrefabEntries();
            List<SaveSystemSettings.SceneEntry> scenes = SaveCatalogBuilder.BuildSceneEntries();

            foreach (SaveSystemSettings.PrefabEntry entry in prefabs.Where(entry => !settings.Prefabs.Any(known => known.Id == entry.Id && known.Prefab == entry.Prefab)))
                problems.Add($"{settings.name}: the prefab '{entry.Prefab.name}' is missing from the catalog");
            foreach (SaveSystemSettings.PrefabEntry entry in settings.Prefabs.Where(known => !prefabs.Any(entry => entry.Id == known.Id)))
                problems.Add($"{settings.name}: the prefab {entry.Id} is in the catalog but has no Saveable any more");
            foreach (SaveSystemSettings.SceneEntry entry in scenes.Where(entry => !settings.Scenes.Any(known => known.Guid == entry.Guid && known.Path == entry.Path)))
                problems.Add($"{settings.name}: the scene '{entry.Path}' is missing from the catalog");
            foreach (SaveSystemSettings.SceneEntry entry in settings.Scenes.Where(known => !scenes.Any(entry => entry.Guid == known.Guid)))
                problems.Add($"{settings.name}: the scene '{entry.Path}' is in the catalog but not in the project");

            List<SaveSystemSettings.AssetEntry> assets = SaveCatalogBuilder.BuildAssetEntries(settings);
            foreach (SaveSystemSettings.AssetEntry entry in assets.Where(entry => !settings.Assets.Any(known => known.Key == entry.Key && known.Asset == entry.Asset)))
                problems.Add($"{settings.name}: the asset '{entry.Asset.name}' is missing from the asset table");
            foreach (SaveSystemSettings.AssetEntry entry in settings.Assets.Where(known => !assets.Any(entry => entry.Key == known.Key)))
                problems.Add($"{settings.name}: the asset {entry.Key} is in the asset table but not in its folders any more");

            return problems;
        }
    }
}
