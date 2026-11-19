using System;
using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// On the player's tank: its health reaching 0 ends the run as a defeat, once per run (a new run,
    /// IRunService.RunStarted, arms it again).
    /// </summary>
    public sealed class TankDefeatBehaviour : IEntityInit, IEntityDispose
    {
        private readonly IRunService _run;
        private Health _health;
        private Action _onRunStarted;
        private bool _done;

        public TankDefeatBehaviour(IRunService run)
        {
            _run = run;
        }

        public void Init(IEntity entity)
        {
            if (_run == null || !entity.TryGetHealth(out _health) || _health == null)
                return;

            _health.OnHealthEmpty += OnHealthEmpty;
            _onRunStarted = OnRunStarted;
            _run.RunStarted += _onRunStarted;
        }

        public void Dispose(IEntity entity)
        {
            if (_health != null)
                _health.OnHealthEmpty -= OnHealthEmpty;
            if (_onRunStarted != null)
                _run.RunStarted -= _onRunStarted;
            _health = null;
            _onRunStarted = null;
        }

        private void OnRunStarted() => _done = false;

        private void OnHealthEmpty()
        {
            if (_done)
                return;

            _done = true;
            if (_run.State != RunState.Ended)
                _run.EndRun(RunOutcome.Defeat);
        }
    }
}
