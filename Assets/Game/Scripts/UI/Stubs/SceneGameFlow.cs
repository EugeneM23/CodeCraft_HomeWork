using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game
{
    /// <summary>
    /// STUB of the core team's game flow (no Boot, no loading screen), loading levels the way the core's flow will:
    /// the level's scenes by path, the first Single and the rest additively, then its active scene and the catalog's
    /// level layer (HUD, pause, results) instantiated into its gameplay scene. Level ids map to levels through the
    /// core catalog the menus register (AppServices.RegisterLevels). In the editor scenes load without being in
    /// Build Settings.
    /// </summary>
    public sealed class SceneGameFlow : IGameFlow
    {
        public const string MENU_SCENE = "MainMenu";

        private const string SCENE_EXTENSION = ".unity";

        public event Action<GameState> StateChanged;

        private LevelConfig _currentLevel;
        private bool _loading;

        public LevelCatalog Levels { get; set; }

        public GameState State { get; private set; } = GameState.Starting;
        public bool IsBusy => _loading;
        public string CurrentLevelId { get; private set; }
        public string RequestedMenuScreen { get; private set; }

        public SceneGameFlow()
        {
            // Started straight from a level scene in the editor: the flow still knows where it is.
            if (SceneManager.GetActiveScene().name == MENU_SCENE)
                Enter(GameState.MainMenu);
        }

        public bool StartLevel(string levelId)
        {
            if (_loading)
                return false;

            LevelConfig level = Levels != null ? Levels.Find(levelId) : null;
            if (level == null)
            {
                Debug.LogWarning($"SceneGameFlow: no level '{levelId}' in the registered catalog.");
                return false;
            }

            BeginLevel(level);
            return true;
        }

        public bool RestartLevel()
        {
            if (_loading || State == GameState.MainMenu)
                return false;

            // Started straight from a level scene in the editor: the level is found by the open scene.
            string activePath = SceneManager.GetActiveScene().path;
            LevelConfig level = _currentLevel;
            if (level == null && Levels != null)
                level = Levels.FindByScene(activePath);

            if (level != null)
            {
                BeginLevel(level);
                return true;
            }

            CurrentLevelId = null;
            Begin(LoadScene(activePath, GameState.Level));
            return true;
        }

        public bool ReturnToMenu(string screen = null)
        {
            if (_loading)
                return false;

            RequestedMenuScreen = screen;
            CurrentLevelId = null;
            _currentLevel = null;
            string menuPath = Levels != null && Levels.MainMenuScene != null && !Levels.MainMenuScene.IsEmpty
                ? Levels.MainMenuScene.Path
                : MENU_SCENE;
            Begin(LoadScene(menuPath, GameState.MainMenu));
            return true;
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void BeginLevel(LevelConfig level)
        {
            _currentLevel = level;
            CurrentLevelId = level.Id;
            Begin(LoadLevel(level));
        }

        // The player's pause stays on while the scenes load: the battle under the pause or results screen must
        // not run until the new scenes are in (Finish or Fail lifts it).
        private void Begin(IEnumerator routine)
        {
            _loading = true;
            SceneGameFlowRunner.Run(routine);
        }

        private void Finish(GameState state)
        {
            _loading = false;
            AppServices.Pause.Resume();
            Enter(state);
        }

        // The level layer goes in only after every scene of the level has loaded.
        private IEnumerator LoadLevel(LevelConfig level)
        {
            IReadOnlyList<SceneReference> scenes = level.Scenes;
            bool first = true;
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i] == null || scenes[i].IsEmpty)
                    continue;

                AsyncOperation operation = LoadAsync(scenes[i].Path, first ? LoadSceneMode.Single : LoadSceneMode.Additive);
                if (operation == null)
                {
                    Fail(scenes[i].Path);
                    yield break;
                }

                first = false;
                yield return operation;
            }

            if (first)
            {
                Fail(level.Id);
                yield break;
            }

            Scene active = SceneManager.GetSceneByPath(level.ActiveScenePath);
            if (active.IsValid() && active.isLoaded)
                SceneManager.SetActiveScene(active);

            Scene gameplay = SceneManager.GetSceneByPath(level.GameplayScenePath);
            if (!gameplay.IsValid() || !gameplay.isLoaded)
                gameplay = SceneManager.GetActiveScene();

            IReadOnlyList<GameObject> layer = Levels != null ? Levels.LevelLayer : null;
            if (layer != null)
            {
                for (int i = 0; i < layer.Count; i++)
                {
                    if (layer[i] == null)
                        continue;

                    GameObject instance = Object.Instantiate(layer[i]);
                    instance.name = layer[i].name;
                    SceneManager.MoveGameObjectToScene(instance, gameplay);
                }
            }

            Finish(GameState.Level);
        }

        private IEnumerator LoadScene(string path, GameState state)
        {
            AsyncOperation operation = LoadAsync(path, LoadSceneMode.Single);
            if (operation == null)
            {
                Fail(path);
                yield break;
            }

            yield return operation;
            Finish(state);
        }

        private static AsyncOperation LoadAsync(string path, LoadSceneMode mode)
        {
#if UNITY_EDITOR
            // A path loads without the scene in Build Settings; a bare name (the menu fallback) goes the usual way.
            if (path.EndsWith(SCENE_EXTENSION, StringComparison.Ordinal))
                return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path,
                    new LoadSceneParameters(mode));
#endif
            return SceneManager.LoadSceneAsync(path, mode);
        }

        private void Fail(string what)
        {
            _loading = false;
            AppServices.Pause.Resume();
            Debug.LogError($"SceneGameFlow: cannot load '{what}'.");
        }

        private void Enter(GameState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
