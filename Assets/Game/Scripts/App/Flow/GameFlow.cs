using System;
using System.Collections.Generic;
using System.Reflection;
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
    /// starts standing. A level may be played as a chapter of the game catalog (StartChapter): the chapter is current
    /// together with its level, survives a restart and is cleared by the return to the menu.
    /// </summary>
    public sealed class GameFlow : IGameFlow, IAppTickable
    {
        // A screen that never covers (switched off, broken) must not hold the game: the scenes change anyway
        private const float MAX_COVER_WAIT_SECONDS = 3f;

        // Compile time per frame behind the loading screen: it keeps about 30 frames a second
        private const double WARM_UP_FRAME_MS = 16d;

        private readonly LevelCatalog _catalog;
        private readonly GameCatalog _gameCatalog;
        private readonly ILevelLoader _loader;
        private readonly ILoadingScreen _screen;
        private readonly IGameLoop _loop;
        private readonly IReadOnlyList<Assembly> _warmUp;
        private readonly LoadingRunner _runner = new();
        private readonly Action<LoadingResult> _finish;
        private readonly Func<bool> _isCovered;

        private GameState _from;
        private GameState _target;
        private LevelConfig _targetLevel;
        private ChapterId _targetChapter;
        private float _coverWait;

        /// <param name="screen">May be null: transitions run uncovered.</param>
        /// <param name="loop">May be null; otherwise every pause is let go after a transition.</param>
        /// <param name="gameCatalog">May be null: no chapters (StartChapter is refused, CurrentChapter stays none).</param>
        public GameFlow(LevelCatalog catalog, ILevelLoader loader, ILoadingScreen screen = null, IGameLoop loop = null,
            IReadOnlyList<Assembly> warmUp = null, GameCatalog gameCatalog = null)
        {
            _catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            // A missing asset is Unity-null: keep a real null
            _gameCatalog = gameCatalog != null ? gameCatalog : null;
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _screen = screen;
            _loop = loop;
            _warmUp = warmUp;
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

        /// <summary>
        /// The chapter being played; none outside a level or in a level opened without a chapter. Set together with
        /// CurrentLevel once the level is loaded, kept by RestartLevel, cleared by the return to the menu.
        /// </summary>
        public ChapterId CurrentChapter { get; private set; }
        public string RequestedMenuScreen { get; private set; }

        /// <summary>0..1 of the running transition; 0 when none runs.</summary>
        public float LoadingProgress => IsBusy ? _runner.Progress : 0f;

        public LevelCatalog Catalog => _catalog;

        /// <summary>The tanks, modules, skills and chapters; null when the flow was made without it.</summary>
        public GameCatalog GameCatalog => _gameCatalog;

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

        /// <summary>
        /// From the menu or a level: the chapter's level is played as that chapter. Refused (false, logged) when the
        /// chapter is not in the game catalog or has no level; refused silently while busy or outside menu and level.
        /// </summary>
        public bool StartChapter(ChapterId chapter)
        {
            ChapterConfig config = _gameCatalog != null ? _gameCatalog.FindChapter(chapter) : null;
            if (config == null)
            {
                string where = _gameCatalog != null ? $"the game catalog '{_gameCatalog.name}'" : "no game catalog";
                Debug.LogError($"{nameof(GameFlow)}: no chapter '{chapter}' in {where}");
                return false;
            }

            if (config.Level == null)
            {
                Debug.LogError($"{nameof(GameFlow)}: the chapter '{chapter}' has no level");
                return false;
            }

            return OpenLevel(config.Level, chapter);
        }

        /// <summary>From the menu or a level: the level's scenes replace whatever is open. Played without a chapter.</summary>
        public bool OpenLevel(LevelConfig level) => OpenLevel(level, default);

        /// <summary>The current level again, as the same chapter.</summary>
        public bool RestartLevel() =>
            State == GameState.Level && CurrentLevel != null && OpenLevel(CurrentLevel, CurrentChapter);

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
        public void EnterOpenScene(Scene scene) => EnterOpenScene(scene.path);

        /// <summary>
        /// EnterOpenScene by the scene's path. A level is played as the first chapter of the game catalog that is
        /// played on it; none when no chapter is.
        /// </summary>
        public void EnterOpenScene(string scenePath)
        {
            if (IsBusy || State != GameState.Starting)
                return;

            if (_catalog.IsMenuScene(scenePath))
            {
                Enter(GameState.MainMenu);
                return;
            }

            LevelConfig level = _catalog.FindByScene(scenePath);
            if (level == null)
            {
                Debug.LogWarning($"{nameof(GameFlow)}: '{scenePath}' is neither the menu nor a level of the catalog");
                return;
            }

            _targetLevel = level;
            if (TryBuild(LoadMissingTree, out LoadingOperation tree))
                Go(tree, GameState.Level, level, true, null, ChapterOf(level));
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

        private bool OpenLevel(LevelConfig level, ChapterId chapter)
        {
            if (level == null || IsBusy || (State != GameState.MainMenu && State != GameState.Level))
                return false;

            _targetLevel = level;
            return TryBuild(LoadLevelTree, out LoadingOperation tree)
                   && Go(tree, GameState.Level, level, false, null, chapter);
        }

        // The first chapter of the game catalog played on the level; none without one
        private ChapterId ChapterOf(LevelConfig level)
        {
            if (_gameCatalog == null)
                return default;

            IReadOnlyList<ChapterConfig> chapters = _gameCatalog.Chapters;
            for (int i = 0; i < chapters.Count; i++)
            {
                if (chapters[i] != null && chapters[i].Level == level)
                    return chapters[i].Id;
            }

            return default;
        }

        // ---------- The trees: what every transition does ----------

        private LoadingOperation LoadMenuTree() => _loader.LoadScene(_catalog.MainMenuScene.Path);

        // The code of the fight is compiled behind the screen, once a session, while the scenes load: the first
        // crush must not stall on the JIT, and the wait for the level grows as little as it can
        private LoadingOperation LoadLevelTree() => new SequenceOperation(
            new ParallelOperation(_loader.Load(_targetLevel), WarmUp()),
            new ActionOperation(EnterTargetLevel));

        private LoadingOperation LoadMissingTree() => new SequenceOperation(
            new ParallelOperation(_loader.LoadMissing(_targetLevel), WarmUp()),
            new ActionOperation(EnterTargetLevel));

        private LoadingOperation WarmUp() =>
            _warmUp != null && _warmUp.Count > 0 ? new WarmUpCodeOperation(_warmUp, WARM_UP_FRAME_MS) : new SequenceOperation();

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
            string menuScreen, ChapterId chapter = default)
        {
            _from = State;
            _target = target;
            _targetLevel = level;
            _targetChapter = chapter;
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
            ChapterId chapter = _targetChapter;
            _targetLevel = null;
            _targetChapter = default;
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
                CurrentChapter = chapter;
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
            {
                CurrentLevel = null;
                CurrentChapter = default;
            }

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
