using System;
using Atomic.Elements;
using Atomic.Entities;
using Game.Horde;
using Unity.Collections;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// TEMPORARY bridge (combat team) until the AI team ships its own: puts the horde into combat. On the horde
    /// entity, after <see cref="HordeBehaviour"/> (HordeInstaller adds it): registers a <see cref="HordeTargetSource"/>
    /// in the level's <c>GameContext.GetEnemyTargets()</c> as soon as both exist, and reports every monster's death to
    /// <c>GameContext.GetEnemyDeaths()</c> as <c>EnemyDeath(Crawler, feet, crushed, false)</c> — crushed when the
    /// simulation's death cause is <see cref="HordeDeathCause.Crushed"/>.
    /// <para>
    /// Deaths: HordeBehaviour raises HordeMonsterKilled once per entry of <see cref="HordeSimulation.Deaths"/>, in
    /// order, before it clears them; the handler finds the entry by its point to read the cause.
    /// </para>
    /// </summary>
    public sealed class HordeCombatBridge : IEntityInit, IEntityTick, IEntityDispose
    {
        private const float AT_HEIGHT = 0.6f; // HordeBehaviour raises the death 0.6 m above the feet
        private const float MATCH_SQ = 0.01f;

        private readonly HordeBehaviour _horde;

        private HordeTargetSource _source;
        private GameContext _context;
        private IEnemyTargets _targets;
        private int _sourceId = -1;
        private IEvent<Vector3> _killed;
        private Action<Vector3> _onKilled;
        private int _cursor;

        public HordeCombatBridge(HordeBehaviour horde) => _horde = horde;

        public void Init(IEntity entity)
        {
            if (_horde?.Simulation == null)
                return;

            _source = new HordeTargetSource(_horde);
            if (entity.TryGetHordeMonsterKilled(out _killed) && _killed != null)
            {
                _onKilled = OnKilled;
                _killed.OnEvent += _onKilled;
            }

            TryRegister();
        }

        public void Dispose(IEntity entity)
        {
            if (_killed != null && _onKilled != null)
                _killed.OnEvent -= _onKilled;
            _killed = null;

            // Only from the targets it was registered in: a new level's targets may have reused the id.
            if (_targets != null && _sourceId >= 0 && GameContext.TryGetInstance(out GameContext context) &&
                context.TryGetEnemyTargets(out IEnemyTargets current) && ReferenceEquals(current, _targets))
                _targets.Unregister(_sourceId);
            _targets = null;
            _context = null;
            _sourceId = -1;
            _source = null;
        }

        // Every tick: the level's combat may be installed after the horde started, or reinstalled (a GameContext
        // restarted in the same scene, new EnemyTargets). A live cached context costs a native null check and a key
        // lookup.
        public void Tick(IEntity entity, float deltaTime)
        {
            if (_source != null)
                TryRegister();
        }

        /// <summary>Registers the source in the context's current EnemyTargets, leaving the ones it was in before.</summary>
        private void TryRegister()
        {
            if (_context == null)
            {
                if (!GameContext.TryGetInstance(out _context) || _context == null)
                {
                    _context = null;
                    return;
                }
            }

            _context.TryGetEnemyTargets(out IEnemyTargets targets);
            if (ReferenceEquals(targets, _targets))
                return;

            // The old targets were dropped with their context or combat install: leaving them frees nothing shared.
            if (_targets != null && _sourceId >= 0)
                _targets.Unregister(_sourceId);

            _targets = targets;
            _sourceId = targets != null ? targets.Register(_source) : -1;
        }

        private void OnKilled(Vector3 at)
        {
            if (!GameContext.TryGetInstance(out GameContext context) ||
                !context.TryGetEnemyDeaths(out IEnemyDeaths deaths) || deaths == null)
                return;

            Vector3 feet = at - Vector3.up * AT_HEIGHT;
            bool crushed = false;
            HordeSimulation simulation = _horde.Simulation;
            if (simulation != null && !simulation.IsRunning)
            {
                NativeArray<HordeDeathEvent> list = simulation.Deaths;
                int index = Find(list, feet);
                if (index >= 0)
                {
                    crushed = list[index].Cause == HordeDeathCause.Crushed;
                    feet = list[index].Position;
                    _cursor = index + 1;
                }
            }

            deaths.Report(new EnemyDeath(EnemyKind.Crawler, feet, crushed, false));
        }

        /// <summary>The death entry at this point: the next one in order first, then any.</summary>
        private int Find(NativeArray<HordeDeathEvent> list, Vector3 feet)
        {
            if (_cursor >= list.Length)
                _cursor = 0;
            for (int n = 0; n < list.Length; n++)
            {
                int i = (_cursor + n) % list.Length;
                if (((Vector3)list[i].Position - feet).sqrMagnitude <= MATCH_SQ)
                    return i;
            }

            return -1;
        }
    }
}
