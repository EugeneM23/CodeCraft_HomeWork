using System;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// A monster (Regax): a roar when it first appears, a growl every 4–10 s while it lives, steps while it
    /// moves, the swing of the weapon in its hand, a cry when hit, the death cry. Sounds play at its position.
    /// </summary>
    public sealed class MonsterAudioBehaviour : IEntityInit, IEntityEnable, IEntityTick, IEntityDispose
    {
        private const float GROWL_MIN = 4f;
        private const float GROWL_MAX = 10f;

        /// <summary>Seconds between steps while moving.</summary>
        private const float STEP_INTERVAL = 0.45f;

        /// <summary>Speed (m/s) below which it stands still.</summary>
        private const float MOVING = 0.1f;

        private readonly Action<TakeDamageArgs> _onDamage;
        private readonly Action<TakeDamageArgs> _onDeath;
        private readonly Action<IEntity> _onAttack;

        private Transform _transform;
        private Health _health;
        private IValue<float> _velocity;
        private IEvent<TakeDamageArgs> _damageEvent;
        private IEvent<TakeDamageArgs> _deathEvent;
        private HeldWeaponFireListener _weaponFire;

        private bool _roared;
        private float _growlTimer;
        private float _stepTimer;

        public MonsterAudioBehaviour()
        {
            _onDamage = OnDamage;
            _onDeath = OnDeath;
            _onAttack = OnAttack;
        }

        public void Init(IEntity entity)
        {
            _transform = entity.GetTransform();
            entity.TryGetHealth(out _health);
            if (entity.TryGetVelocity(out IReactiveVariable<float> velocity))
                _velocity = velocity;

            if (entity.TryGetDamageTakenEvent(out _damageEvent))
                _damageEvent.OnEvent += _onDamage;
            if (entity.TryGetDeathTakenEvent(out _deathEvent))
                _deathEvent.OnEvent += _onDeath;
            if (entity.TryGetWeapon(out IReactiveVariable<IEntity> weapon))
                _weaponFire = new HeldWeaponFireListener(weapon, _onAttack);
        }

        public void Enable(IEntity entity)
        {
            _growlTimer = Random.Range(GROWL_MIN, GROWL_MAX);
            _stepTimer = STEP_INTERVAL;

            if (_roared)
                return;

            _roared = true;
            GameAudio.Play(AudioKeys.Monsters.ROAR, _transform.position);
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            if (_health != null && !_health.Exists())
                return;

            _growlTimer -= deltaTime;
            if (_growlTimer <= 0f)
            {
                _growlTimer = Random.Range(GROWL_MIN, GROWL_MAX);
                GameAudio.Play(AudioKeys.Monsters.GROWL, _transform.position);
            }

            if (_velocity == null)
                return;

            if (_velocity.Value < MOVING)
            {
                _stepTimer = STEP_INTERVAL;
                return;
            }

            _stepTimer -= deltaTime;
            if (_stepTimer > 0f)
                return;

            _stepTimer = STEP_INTERVAL;
            GameAudio.Play(AudioKeys.Monsters.STEP, _transform.position);
        }

        public void Dispose(IEntity entity)
        {
            if (_damageEvent != null)
                _damageEvent.OnEvent -= _onDamage;
            if (_deathEvent != null)
                _deathEvent.OnEvent -= _onDeath;
            _weaponFire?.Dispose();

            _damageEvent = null;
            _deathEvent = null;
            _weaponFire = null;
        }

        private void OnDamage(TakeDamageArgs args) => GameAudio.Play(AudioKeys.Monsters.HIT, _transform.position);

        // Where the level reports deaths, RunEventAudio plays them by kind; a second cry here would double it.
        private void OnDeath(TakeDamageArgs args)
        {
            if (!GameAudio.DeathsReported)
                GameAudio.Play(AudioKeys.Monsters.DEATH, _transform.position);
        }

        private void OnAttack(IEntity weapon) => GameAudio.Play(AudioKeys.Monsters.ATTACK, _transform.position);
    }
}
