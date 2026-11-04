using System.Collections.Generic;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Spawns one enemy per entry into the trigger, then refuses until the player has left and
    /// the cooldown has elapsed. Standing inside does nothing.
    ///
    /// Occupancy is tracked explicitly instead of being inferred from the number of events: the
    /// trigger reports one event per <see cref="Collider"/> and a character carries several, so
    /// counting events would fire on every limb. EffectAreaBehaviour already hit exactly this.
    /// </summary>
    public sealed class SpawnButtonBehaviour : IEntityInit, IEntityDispose, IEntityTick
    {
        private readonly TriggerEventReceiver _trigger;
        private readonly MonoEntity _enemyPrefab;
        private readonly Transform _spawnPoint;
        private readonly float _cooldown;

        private readonly HashSet<Collider> _occupants = new();

        private float _remainingCooldown;
        private bool _armed = true;

        public SpawnButtonBehaviour(
            TriggerEventReceiver trigger,
            MonoEntity enemyPrefab,
            Transform spawnPoint,
            float cooldown)
        {
            _trigger = trigger;
            _enemyPrefab = enemyPrefab;
            _spawnPoint = spawnPoint;
            _cooldown = cooldown;
        }

        public void Init(IEntity entity)
        {
            _trigger.OnEntered += this.OnEntered;
            _trigger.OnExited += this.OnExited;
        }

        public void Dispose(IEntity entity)
        {
            _trigger.OnEntered -= this.OnEntered;
            _trigger.OnExited -= this.OnExited;
            _occupants.Clear();
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            if (_remainingCooldown <= 0f) return;

            _remainingCooldown -= deltaTime;
        }

        private void OnEntered(Collider other)
        {
            if (!IsPlayer(other)) return;

            bool wasEmpty = _occupants.Count == 0;
            _occupants.Add(other);

            if (!wasEmpty) return;
            if (!_armed || _remainingCooldown > 0f) return;

            _armed = false;
            _remainingCooldown = _cooldown;
            this.Spawn();
        }

        private void OnExited(Collider other)
        {
            if (!_occupants.Remove(other)) return;
            if (_occupants.Count > 0) return;

            _armed = true;
        }

        private static bool IsPlayer(Collider other) =>
            other.TryGetEntity(out IEntity entity) && entity.HasPlayerTag();

        private void Spawn()
        {
            GameContext context = GameContext.Instance;

            SpawnUseCase.SpawnCharacter(
                _enemyPrefab,
                _spawnPoint,
                context.GetEntityWorld(),
                context.GetGameFactory());
        }
    }
}
