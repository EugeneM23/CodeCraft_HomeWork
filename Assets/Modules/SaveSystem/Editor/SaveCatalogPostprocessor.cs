using System.Collections.Generic;
using System.Linq;
using Saving.Objects;
using UnityEditor;

namespace Saving.Editor
{
    /// <summary>
    /// After prefabs or scenes are imported, moved or deleted: bake prefab ids and rebuild the catalogs, once per batch;
    /// the asset table only when something in its folders changed.
    /// </summary>
    internal sealed class SaveCatalogPostprocessor : AssetPostprocessor
    {
        private static readonly HashSet<string> TouchedPrefabs = new();
        private static bool _scheduled;
        private static bool _assetsTouched;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            string[] all = imported.Concat(deleted).Concat(moved).Concat(movedFrom).ToArray();
            bool catalogs = all.Any(path => path.EndsWith(".prefab") || path.EndsWith(".unity"));
            string[] folders = AssetFolderPrefixes();
            bool assets = all.Any(path => InAssetFolders(path, folders));
            if (!catalogs && !assets)
                return;

            foreach (string path in imported.Concat(moved).Where(path => path.EndsWith(".prefab")))
                TouchedPrefabs.Add(path);

            _assetsTouched |= assets;
            if (_scheduled)
                return;

            _scheduled = true;
            EditorApplication.delayCall += Run;
        }

        // Searched once per batch: a domain reload hands over thousands of paths, a project search per path took minutes
        private static string[] AssetFolderPrefixes() =>
            SaveCatalogBuilder.AllSettings()
                .SelectMany(settings => settings.AssetFolders)
                .Select(folder => folder.TrimEnd('/') + "/")
                .Distinct()
                .ToArray();

        private static bool InAssetFolders(string path, string[] folders) =>
            folders.Any(folder => path.StartsWith(folder, System.StringComparison.Ordinal));

        private static void Run()
        {
            _scheduled = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // Assets change during play too (the Test Framework's InitTestScene): catch up once back in edit mode
                _scheduled = true;
                EditorApplication.playModeStateChanged += RunAfterPlay;
                return;
            }

            string[] prefabs = TouchedPrefabs.ToArray();
            TouchedPrefabs.Clear();
            bool assets = _assetsTouched;
            _assetsTouched = false;
            SaveCatalogBuilder.BakePrefabIds(prefabs);
            foreach (SaveSystemSettings settings in SaveCatalogBuilder.AllSettings())
                SaveCatalogBuilder.Rebuild(settings, includeAssets: assets);
        }

        private static void RunAfterPlay(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode)
                return;

            EditorApplication.playModeStateChanged -= RunAfterPlay;
            Run();
        }
    }
}
