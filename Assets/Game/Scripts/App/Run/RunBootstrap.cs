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

        /// <summary>A level-up offers skills. Off for now (the user): levels still go up, with no choice screen and no pause.</summary>
        private const bool SKILL_OFFERS = false;

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
        private bool _ownsDeaths;
        private bool _waiting;
        private bool _started;
        private float _waited;

        /// <summary>The run of this level; null without the GameContext or the catalog.</summary>
        public RunService Run => _run;

        private void Awake()
        {
            // The level's GameContext may sit in another scene of the level that wakes later (the sandbox loads its
            // gameplay scene first): the run then waits for it (Update) instead of giving up. Without a run the
            // level has no levels-up and no skills at all — the stand-in is not made in the game any more.
            if (!GameContext.TryGetInstance(out _context))
            {
                _waiting = true;
                return;
            }

            Setup();
        }

        private void Setup()
        {

            // Combat may have installed first: it adds the deaths feed and a stand-in run when the level has none.
            // The feed is kept (its listeners are already on it); the stand-in gives way to the real run below.
            if (_context.HasEnemyDeaths() && _context.GetEnemyDeaths() is EnemyDeaths existing)
                _deaths = existing;
            else
            {
                _deaths = new EnemyDeaths();
                if (_context.HasEnemyDeaths())
                    _context.DelEnemyDeaths();
                _context.AddEnemyDeaths(_deaths);
                _ownsDeaths = true;
            }

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
                bestArenaOf, leaveToHangar) { OffersSkills = SKILL_OFFERS };
            _onRunEnded = OnRunEnded;
            _run.RunEnded += _onRunEnded;
            if (_context.TryGetRun(out IRunService standIn))
            {
                (standIn as IDisposable)?.Dispose();
                loop?.Resume(standIn); // a pause the stand-in held must not outlive it
                _context.DelRun();
            }
            _context.AddRun(_run);

            // Combat may have attached the tank before the run was here: its defeat and skill effects bind now.
            if (_context.TryGetBehaviour(out CombatBehaviour combat))
                combat.RebindRun();
        }

        private void OnEnable() => _looped = GameTicks.TryAdd(this);

        private void OnDisable()
        {
            if (_looped)
                GameTicks.Remove(this);
            _looped = false;
        }

        private void Start() => StartRun();

        private void StartRun()
        {
            if (_run == null || _started)
                return;
            _started = true;

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
            if (_waiting)
                WaitForContext();

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
                if (_deaths != null && _ownsDeaths)
                    _context.DelEnemyDeaths();
            }

            _run = null;
            _deaths = null;
            _context = null;
        }

        // The level's other scene has woken: the run is set up and started now. Gives up after a few seconds.
        private void WaitForContext()
        {
            if (GameContext.TryGetInstance(out _context))
            {
                _waiting = false;
                Setup();
                StartRun();
                return;
            }

            _waited += Time.unscaledDeltaTime;
            if (_waited > 5f)
            {
                _waiting = false;
                Debug.LogWarning($"{nameof(RunBootstrap)}: no GameContext in the level, the run is off", this);
            }
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
