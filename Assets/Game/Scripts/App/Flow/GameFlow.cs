using System;
using System.Collections.Generic;
using Loading;
using Ticking;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game
{
    /// <summary>
    /// The course of the game: Starting → MainMenu ⇄ Level. Every transition is one loading tree run behind the
    /// loading screen on real time (AppTicker ticks the flow): the screen covers, the scenes change, the level layer
    /// goes into the level, the screen goes. One transition at a time: requests while busy are refused (false). A
    /// failed transition is logged and keeps the state. After a transition every pause is let go, so a level never
    /// starts standing.
    /// </summary>
    public sealed class GameFlow : IGameFlow, IAppTickable
    {
        // A screen that never covers (switched off, broken) must not hold the game: the scenes change anyway
        private const float MAX_COVER_WAIT_SECONDS = 3f;

        private readonly LevelCatalog _catalog;
        private readonly ILevelLoader _loader;
        private readonly ILoadingScreen _screen;
        private readonly IGameLoop _loop;
        private readonly LoadingRunner _runner = new();
        private readonly Action<LoadingResult> _finish;
        private readonly Func<bool> _isCovered;

        private GameState _from;
        private GameState _target;
        private LevelConfig _targetLevel;
        private float _coverWait;

        /// <param name="screen">May be null: transitions run uncovered.</param>
        /// <param name="loop">May be null; otherwise every pause is let go after a transition.</param>
        public GameFlow(LevelCatalog catalog, ILevelLoader loader, ILoadingScreen screen = null, IGameLoop loop = null)
        {
            _catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _screen = screen;
            _loop = loop;
            _finish = Finish;
            _isCovered = IsCovered;
        }

        public event Action<GameState> StateChanged;

        /// <summary>A transition starts (from, to): before any scene changes — the autosave writes the progress here.</summary>
        public event Action<GameState, GameState> TransitionStarted;

        public event Action LoadingStarted;

        /// <summary>True — the transition succeeded.</summary>
        public event Action<bool> LoadingFinished;

        public GameState State { get; private set; } = GameState.Starting;
        public bool IsBusy { get; private set; }

        /// <summary>The level being played; null outside a level.</summary>
        public LevelConfig CurrentLevel { get; private set; }

        public string CurrentLevelId => CurrentLevel != null ? CurrentLevel.Id : null;
        public string RequestedMenuScreen { get; private set; }

        /// <summary>0..1 of the running transition; 0 when none runs.</summary>
        public float LoadingProgress => IsBusy ? _runner.Progress : 0f;

        public LevelCatalog Catalog => _catalog;

        /// <summary>The start from the Boot scene: from black into the main menu.</summary>
        public bool Boot()
        {
            if (IsBusy || State != GameState.Starting)
                return false;

            return TryBuild(LoadMenuTree, out LoadingOperation tree)
                   && Go(tree, GameState.MainMenu, null, true, null);
        }

        public bool StartLevel(string levelId)
        {
            LevelConfig level = _catalog.Find(levelId);
            if (level != null)
                return OpenLevel(level);

            Debug.LogError($"{nameof(GameFlow)}: no level '{levelId}' in the catalog '{_catalog.name}'");
            return false;
        }

        /// <summary>From the menu or a level: the level's scenes replace whatever is open.</summary>
        public bool OpenLevel(LevelConfig level)
        {
            if (level == null || IsBusy || (State != GameState.MainMenu && State != GameState.Level))
                return false;

            _targetLevel = level;
            return TryBuild(LoadLevelTree, out LoadingOperation tree)
                   && Go(tree, GameState.Level, level, false, null);
        }

        public bool RestartLevel() => State == GameState.Level && CurrentLevel != null && OpenLevel(CurrentLevel);

        public bool ReturnToMenu(string screen = null)
        {
            if (IsBusy || State != GameState.Level)
                return false;

            return TryBuild(LoadMenuTree, out LoadingOperation tree)
                   && Go(tree, GameState.MainMenu, null, false, screen);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// The editor cold start: the scene opened in the editor is loaded and nothing in it has started. The menu is
        /// entered as it is; a level gets its missing scenes and its level layer. Other scenes are left alone.
        /// </summary>
        public void EnterOpenScene(Scene scene)
        {
            if (IsBusy || State != GameState.Starting)
                return;

            if (_catalog.IsMenuScene(scene.path))
            {
                Enter(GameState.MainMenu);
                return;
            }

            LevelConfig level = _catalog.FindByScene(scene.path);
            if (level == null)
            {
                Debug.LogWarning($"{nameof(GameFlow)}: '{scene.path}' is neither the menu nor a level of the catalog");
                return;
            }

            _targetLevel = level;
            if (TryBuild(LoadMissingTree, out LoadingOperation tree))
                Go(tree, GameState.Level, level, true, null);
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!_runner.IsRunning)
                return;

            _coverWait += unscaledDeltaTime;
            _runner.Tick(unscaledDeltaTime);
            if (_runner.IsRunning)
                _screen?.SetProgress(_runner.Progress);
        }

        // ---------- The trees: what every transition does ----------

        private LoadingOperation LoadMenuTree() => _loader.LoadScene(_catalog.MainMenuScene.Path);

        private LoadingOperation LoadLevelTree() => new SequenceOperation(
            _loader.Load(_targetLevel),
            new ActionOperation(EnterTargetLevel));

        private LoadingOperation LoadMissingTree() => new SequenceOperation(
            _loader.LoadMissing(_targetLevel),
            new ActionOperation(EnterTargetLevel));

        // Synchronous, behind the screen: the HUD and the pause screen are in the level before the player sees it
        private void EnterTargetLevel()
        {
            IReadOnlyList<GameObject> layer = _catalog.LevelLayer;
            if (layer.Count == 0)
                return;

            Scene scene = SceneManager.GetSceneByPath(_targetLevel.GameplayScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = SceneManager.GetActiveScene();

            for (int i = 0; i < layer.Count; i++)
            {
                GameObject prefab = layer[i];
                if (prefab == null)
                    continue;

                GameObject instance = Object.Instantiate(prefab);
                instance.name = prefab.name;
                SceneManager.MoveGameObjectToScene(instance, scene);
            }
        }

        // ---------- Running ----------

        private bool TryBuild(Func<LoadingOperation> build, out LoadingOperation tree)
        {
            try
            {
                tree = build();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"{nameof(GameFlow)}: the transition from {State} cannot start: {exception}");
                tree = null;
                return false;
            }
        }

        private bool Go(LoadingOperation tree, GameState target, LevelConfig level, bool instantScreen,
            string menuScreen)
        {
            _from = State;
            _target = target;
            _targetLevel = level;
            _coverWait = 0f;
            RequestedMenuScreen = menuScreen;

            // Busy before Run: an instant tree finishes inside it
            IsBusy = true;
            Raise(TransitionStarted, _from, target);
            Raise(LoadingStarted);

            LoadingOperation root = tree;
            if (_screen != null)
            {
                _screen.Show(instantScreen);
                root = new SequenceOperation(new WaitUntilOperation(_isCovered), tree);
            }

            _runner.Run(root, _finish);
            return true;
        }

        private bool IsCovered() => _screen.IsCovering || _coverWait >= MAX_COVER_WAIT_SECONDS;

        // Copies and clears the transition first: a StateChanged handler may start the next transition at once
        private void Finish(LoadingResult result)
        {
            GameState from = _from;
            GameState target = _target;
            LevelConfig level = _targetLevel;
            _targetLevel = null;
            IsBusy = false;

            if (_screen != null)
            {
                if (result.Succeeded)
                    _screen.SetProgress(1f);
                _screen.Hide();
            }

            if (result.Succeeded)
            {
                CurrentLevel = level;
                ResumeAll();
            }
            else
            {
                Debug.LogError($"{nameof(GameFlow)}: {from} → {target} failed, the game stays in {State}: {result.Error}");
            }

            Raise(LoadingFinished, result.Succeeded);
            if (result.Succeeded)
                Enter(target);
        }

        private void Enter(GameState state)
        {
            State = state;
            if (state != GameState.Level)
                CurrentLevel = null;

            Raise(StateChanged, state);
        }

        private void ResumeAll()
        {
            if (_loop == null)
                return;

            IReadOnlyList<object> sources = _loop.PauseSources;
            for (int guard = sources.Count; guard > 0 && sources.Count > 0; guard--)
                _loop.Resume(sources[sources.Count - 1]);
        }

        // A throwing subscriber must not leave the flow busy forever
        private static void Raise(Action handler)
        {
            try
            {
                handler?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void Raise<T>(Action<T> handler, T arg)
        {
            try
            {
                handler?.Invoke(arg);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void Raise<T1, T2>(Action<T1, T2> handler, T1 arg1, T2 arg2)
        {
            try
            {
                handler?.Invoke(arg1, arg2);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
