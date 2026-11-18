using System;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// Listens to the core's run (<see cref="IRunService"/>) and the enemy deaths (<see cref="IEnemyDeaths"/>) of the
    /// level and turns them into sounds:
    ///   fight start — run.arena_start;
    ///   arena cleared, gate opened, the shutter closing — run.arena_cleared, run.gate_open, run.transition;
    ///   the run ended — the battle music fades for the menu's victory or defeat stinger;
    ///   a death — the crunch and the crush streak when crushed, else the kind's death cry.
    /// A new level, the skill choice and the skill taken are the menu's UI sounds (they play over the paused battle);
    /// the elite's arrival is AI's EliteStatus (<see cref="GameSignalAudio"/>). Either service may be missing (the
    /// sandbox has no run): what is there is listened to.
    /// </summary>
    public sealed class RunEventAudio : IDisposable
    {
        private readonly IRunService _run;
        private readonly IEnemyDeaths _deaths;

        private readonly Action<int> _onFightStarted;
        private readonly Action<int> _onArenaCleared;
        private readonly Action<int> _onGateOpened;
        private readonly Action<bool> _onShutterChanged;
        private readonly Action<RunResult> _onRunEnded;
        private readonly Action<EnemyDeath> _onDied;

        public RunEventAudio(IRunService run, IEnemyDeaths deaths)
        {
            _run = run;
            _deaths = deaths;

            _onFightStarted = OnFightStarted;
            _onArenaCleared = OnArenaCleared;
            _onGateOpened = OnGateOpened;
            _onShutterChanged = OnShutterChanged;
            _onRunEnded = OnRunEnded;
            _onDied = OnDied;

            if (_run != null)
            {
                _run.FightStarted += _onFightStarted;
                _run.ArenaCleared += _onArenaCleared;
                _run.GateOpened += _onGateOpened;
                _run.ShutterChanged += _onShutterChanged;
                _run.RunEnded += _onRunEnded;
            }

            if (_deaths != null)
                _deaths.Died += _onDied;
        }

        /// <summary>The level reports deaths: they are heard from here.</summary>
        public bool HasDeaths => _deaths != null;

        public void Dispose()
        {
            if (_run != null)
            {
                _run.FightStarted -= _onFightStarted;
                _run.ArenaCleared -= _onArenaCleared;
                _run.GateOpened -= _onGateOpened;
                _run.ShutterChanged -= _onShutterChanged;
                _run.RunEnded -= _onRunEnded;
            }

            if (_deaths != null)
                _deaths.Died -= _onDied;
        }

        // The battle music too: a run restarted in the same scene stopped it at RunEnded. Idempotent while it plays.
        private static void OnFightStarted(int arena)
        {
            GameAudio.Run.ArenaStarted();
            if (AppAudio.Instance != null)
                AppAudio.Instance.PlayBattleMusic();
        }

        private static void OnArenaCleared(int arena) => GameAudio.Run.ArenaCleared();

        private static void OnGateOpened(int arena) => GameAudio.Run.GateOpened();

        // Closing (true): the tank is through the gate. Opening is the next arena coming up: its fight start sounds.
        private static void OnShutterChanged(bool closing)
        {
            if (closing)
                GameAudio.Run.Transition();
        }

        private static void OnRunEnded(RunResult result)
        {
            AppAudio app = AppAudio.Instance;
            if (app != null)
                app.StopMusic();
        }

        private static void OnDied(EnemyDeath death) => PlayDeath(death);

        /// <summary>The sound of one death: a crush crunches (and builds the streak), the rest cry by their kind.</summary>
        public static void PlayDeath(in EnemyDeath death)
        {
            if (death.Crushed)
                GameAudio.Run.Crushed(death.Point);

            if (death.Elite || death.Kind == EnemyKind.EliteRegax)
            {
                GameAudio.Elite.Died(death.Point);
                return;
            }

            if (death.Crushed)
                return;

            switch (death.Kind)
            {
                case EnemyKind.Crawler:
                    GameAudio.Horde.CrawlerDied(death.Point);
                    break;
                case EnemyKind.Spitter:
                    GameAudio.Horde.SpitterDied(death.Point);
                    break;
                default:
                    GameAudio.Play(AudioKeys.Monsters.DEATH, death.Point);
                    break;
            }
        }
    }
}
