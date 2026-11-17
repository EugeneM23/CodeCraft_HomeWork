using System;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// Hits on the tank, the alarm once its health falls below a quarter (again only after a repair above it),
    /// and the blast it goes up in.
    /// </summary>
    public sealed class TankHealthAudioBehaviour : IEntityInit, IEntityDispose
    {
        private const float LOW_HEALTH = 0.25f;

        private readonly Action<TakeDamageArgs> _onDamage;
        private readonly Action<TakeDamageArgs> _onDeath;
        private readonly Action<int> _onHealthChanged;

        private Transform _hull;
        private Health _health;
        private IEvent<TakeDamageArgs> _damageEvent;
        private IEvent<TakeDamageArgs> _deathEvent;
        private bool _lowHealth;

        public TankHealthAudioBehaviour()
        {
            _onDamage = OnDamage;
            _onDeath = OnDeath;
            _onHealthChanged = OnHealthChanged;
        }

        public void Init(IEntity entity)
        {
            _hull = entity.GetTransform();

            if (entity.TryGetDamageTakenEvent(out _damageEvent))
                _damageEvent.OnEvent += _onDamage;
            if (entity.TryGetDeathTakenEvent(out _deathEvent))
                _deathEvent.OnEvent += _onDeath;
            if (entity.TryGetHealth(out _health))
            {
                _health.OnHealthChanged += _onHealthChanged;
                _lowHealth = _health.Exists() && _health.GetPercent() < LOW_HEALTH;
            }
        }

        public void Dispose(IEntity entity)
        {
            if (_damageEvent != null)
                _damageEvent.OnEvent -= _onDamage;
            if (_deathEvent != null)
                _deathEvent.OnEvent -= _onDeath;
            if (_health != null)
                _health.OnHealthChanged -= _onHealthChanged;

            _damageEvent = null;
            _deathEvent = null;
            _health = null;
        }

        private void OnDamage(TakeDamageArgs args) => GameAudio.Play(AudioKeys.Tank.HIT, _hull.position);

        private void OnDeath(TakeDamageArgs args) => GameAudio.Play(AudioKeys.Tank.DESTROYED, _hull.position);

        private void OnHealthChanged(int current)
        {
            bool low = current > 0 && _health.GetPercent() < LOW_HEALTH;
            if (low && !_lowHealth)
                GameAudio.Play(AudioKeys.Tank.LOW_HEALTH);
            _lowHealth = low;
        }
    }
}
