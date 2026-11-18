using System;
using Ticking;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Puts the run into the level: in the gameplay scene of a level, next to the GameContext. Awake adds
    /// <see cref="EnemyDeaths"/> and a <see cref="RunService"/> to the GameContext (<c>GetEnemyDeaths</c>,
    /// <c>GetRun</c>); Start starts the run of the game flow's chapter (or the catalog's first) with the selected tank.
    /// The run ticks from the game loop, so it stands on pause; without the app (tests, a sandbox) it ticks from Update.
    /// When the run ends its result goes into the meta progress. Without the game catalog only the deaths feed is
    /// added (no run: <c>TryGetRun</c> is false). The arena team's builder is not wired yet (an arena is ready at once).
    /// </summary>
    public sealed class RunBootstrap : MonoBehaviour, IUpdateListener
    {
        private const string HANGAR_SCREEN = "Hangar";

        [Tooltip("The run's timers: ready timeout, shutter, lull, orb pull, victory delay, crush streak (GDD §3)")]
        [SerializeField] private RunTimings _timings = RunTimings.Default;

        private GameContext _context;
        private EnemyDeaths _deaths;
        private RunService _run;
        private IMetaProgress _meta;
        private GameFlow _flow;
        private GameCatalog _catalog;
        private Action<RunResult> _onRunEnded;
        private bool _looped;

        /// <summary>The run of this level; null without the GameContext or the catalog.</summary>
        public RunService Run => _run;

        private void Awake()
        {
            if (!GameContext.TryGetInstance(out _context))
            {
                Debug.LogWarning($"{nameof(RunBootstrap)}: no GameContext in the scene, the run is off", this);
                return;
            }

            _deaths = new EnemyDeaths();
            _context.AddEnemyDeaths(_deaths);

            IGameLoop loop = null;
            if (AppContext.TryGet(out AppContext app))
            {
                app.TryGetGameCatalog(out _catalog);
                if (app.TryGetMetaProgress(out IMetaProgress meta))
                    _meta = meta;
                if (app.TryGetGameFlow(out GameFlow flow))
                    _flow = flow;
                if (app.TryGetGameLoop(out IGameLoop found))
                    loop = found;
            }

            if (_catalog == null)
            {
                Debug.LogWarning($"{nameof(RunBootstrap)}: no GameCatalog in the AppContext, the run is off", this);
                return;
            }

            IMetaProgress progress = _meta;
            GameFlow gameFlow = _flow;
            Func<ChapterId, bool> isCompleted = null;
            Func<ChapterId, int> bestArenaOf = null;
            Action leaveToHangar = null;
            if (progress != null)
            {
                isCompleted = c => progress.GetChapter(c).Completed;
                bestArenaOf = c => progress.GetChapter(c).BestArena;
            }

            if (gameFlow != null)
                leaveToHangar = () => gameFlow.ReturnToMenu(HANGAR_SCREEN);

            _run = new RunService(_catalog, _deaths, new System.Random(), _timings, new ArenaServiceBuilder(_context), loop, isCompleted,
                bestArenaOf, leaveToHangar);
            _onRunEnded = OnRunEnded;
            _run.RunEnded += _onRunEnded;
            _context.AddRun(_run);
        }

        private void OnEnable() => _looped = GameTicks.TryAdd(this);

        private void OnDisable()
        {
            if (_looped)
                GameTicks.Remove(this);
            _looped = false;
        }

        private void Start()
        {
            if (_run == null)
                return;

            ChapterId chapter = PickChapter();
            if (chapter.IsNone)
            {
                Debug.LogWarning($"{nameof(RunBootstrap)}: the catalog has no chapter, the run is not started", this);
                return;
            }

            TankId tank = _meta != null && !_meta.SelectedTank.IsNone ? _meta.SelectedTank : TankIds.Tiger;
            if (!_run.StartRun(chapter, tank))
                Debug.LogWarning($"{nameof(RunBootstrap)}: the run of '{chapter}' did not start (no arenas?)", this);
        }

        private void Update() // perf-ok: ticks itself without the game loop (tests, sandbox); one flag check with it
        {
            if (!_looped)
                OnUpdate(Time.deltaTime);
        }

        // On the game loop with the app: the run stands on pause
        public void OnUpdate(float deltaTime) => _run?.Tick(deltaTime);

        private void OnDestroy()
        {
            if (_run != null)
            {
                _run.RunEnded -= _onRunEnded;
                _run.Dispose();
            }

            if (_context != null)
            {
                if (_run != null)
                    _context.DelRun();
                if (_deaths != null)
                    _context.DelEnemyDeaths();
            }

            _run = null;
            _deaths = null;
            _context = null;
        }

        // The game flow's chapter if it is in the catalog, otherwise the catalog's first
        private ChapterId PickChapter()
        {
            if (_flow != null && !_flow.CurrentChapter.IsNone && _catalog.FindChapter(_flow.CurrentChapter) != null)
                return _flow.CurrentChapter;

            foreach (ChapterConfig chapter in _catalog.Chapters)
            {
                if (chapter != null)
                    return chapter.Id;
            }

            return default;
        }

        private void OnRunEnded(RunResult result) => _meta?.ApplyRun(result);
    }
}
