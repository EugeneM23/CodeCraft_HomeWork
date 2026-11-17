using System;
using System.Collections.Generic;
using Loading;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// The scene trees of a level (see ILevelLoader). Scenes are loaded by path: in the editor a scene outside Build
    /// Settings loads too (LoadSceneOperation), a build needs every scene of the catalog in Build Settings.
    /// </summary>
    public sealed class LevelLoader : ILevelLoader
    {
        private readonly Func<string, bool> _canLoad;

        /// <param name="canLoad">Whether a scene path can be loaded; null — Build Settings, and in the editor any scene asset.</param>
        public LevelLoader(Func<string, bool> canLoad = null) => _canLoad = canLoad ?? CanLoad;

        public LoadingOperation LoadScene(string scenePath) =>
            new LoadSceneOperation(scenePath, LoadSceneMode.Single);

        public LoadingOperation Load(LevelConfig level)
        {
            Validate(level);
            // The first scene unloads the menu: a level that cannot load whole must fail before it
            for (int i = 0; i < level.Scenes.Count; i++)
            {
                if (!_canLoad(level.Scenes[i].Path))
                    throw new ArgumentException($"Level '{level.Id}': scene '{level.Scenes[i].Path}' cannot be loaded (not in Build Settings)", nameof(level));
            }

            IReadOnlyList<SceneReference> scenes = level.Scenes;

            var additive = new LoadingOperation[scenes.Count - 1];
            for (int i = 1; i < scenes.Count; i++)
                additive[i - 1] = new LoadSceneOperation(scenes[i].Path, LoadSceneMode.Additive);

            return new SequenceOperation(
                new LoadSceneOperation(scenes[0].Path, LoadSceneMode.Single),
                new ParallelOperation(additive),
                new SetActiveSceneOperation(level.ActiveScenePath));
        }

        public LoadingOperation LoadMissing(LevelConfig level)
        {
            Validate(level);
            IReadOnlyList<SceneReference> scenes = level.Scenes;

            var missing = new List<LoadingOperation>(scenes.Count);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (!SceneManager.GetSceneByPath(scenes[i].Path).isLoaded)
                    missing.Add(new LoadSceneOperation(scenes[i].Path, LoadSceneMode.Additive));
            }

            return new SequenceOperation(
                new ParallelOperation(missing.ToArray()),
                new SetActiveSceneOperation(level.ActiveScenePath));
        }

        public LoadingOperation Unload(LevelConfig level)
        {
            Validate(level);
            IReadOnlyList<SceneReference> scenes = level.Scenes;

            var unloads = new List<LoadingOperation>(scenes.Count);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (i != level.ActiveSceneIndex && SceneManager.GetSceneByPath(scenes[i].Path).isLoaded)
                    unloads.Add(new UnloadSceneOperation(scenes[i].Path));
            }

            return new ParallelOperation(unloads.ToArray());
        }

        private static bool CanLoad(string path)
        {
            if (UnityEngine.Application.CanStreamedLevelBeLoaded(path))
                return true;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(path) != null;
#else
            return false;
#endif
        }

        private static void Validate(LevelConfig level)
        {
            if (level == null)
                throw new ArgumentNullException(nameof(level));
            if (level.Scenes.Count == 0)
                throw new ArgumentException($"Level '{level.Id}' has no scenes", nameof(level));

            for (int i = 0; i < level.Scenes.Count; i++)
            {
                if (level.Scenes[i] == null || level.Scenes[i].IsEmpty)
                    throw new ArgumentException($"Level '{level.Id}': scene {i} is not set", nameof(level));
            }

            if (string.IsNullOrEmpty(level.ActiveScenePath))
                throw new ArgumentException(
                    $"Level '{level.Id}': active scene index {level.ActiveSceneIndex} is out of the scenes",
                    nameof(level));
        }
    }
}
