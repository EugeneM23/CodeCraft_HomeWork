using System;
using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The battle's counters and its end for VoxelSandbox, until the core team and game design give levels
    /// their own rules. Kills: every entity with the Enemy tag in the GameContext's world is followed by its
    /// DeathTakenEvent (no polling); enemies spawned later are picked up by the world's OnAdded. Win: the
    /// kills reach the level's target. Loss: the player's tank (the last vehicle the player sat in) or the
    /// player's character on foot runs out of health. Time is scaled time, so a pause does not count.
    /// </summary>
    public sealed class SandboxBattleStats : MonoBehaviour, IBattleStats, IBattleResultSource
    {
        public event Action<BattleResult> Finished;

        [Tooltip("Finds the level being played by IGameFlow.CurrentLevelId")]
        [SerializeField] private LevelCatalog _levelCatalog;
        [Tooltip("Kill target and reward of the level by its id")]
        [SerializeField] private LevelDesignConfig _levelDesign;
        [Tooltip("Kills to win when the scene runs without a level (started straight from the editor)")]
        [SerializeField, Min(0)] private int _defaultKillTarget = 10;

        private readonly Dictionary<IEntity, IEvent<TakeDamageArgs>> _enemies = new();

        private IEntityWorld<IEntity> _world;
        private Subscription<IEntity> _characterSubscription;
        private Health _vehicleHealth;
        private Health _characterHealth;
        private LevelConfig _level;
        private float _startTime;
        private float _endTime = -1f;
        private bool _finished;

        private Action<IEntity> _onEntityAdded;
        private Action<IEntity> _onEntityRemoved;
        private Action<TakeDamageArgs> _onEnemyDeath;
        private Action _onPlayerDestroyed;

        public int Kills { get; private set; }
        public int KillTarget { get; private set; }
        public float ElapsedTime => (_finished ? _endTime : Time.time) - _startTime;

        private void Awake()
        {
            _onEntityAdded = Track;
            _onEntityRemoved = Untrack;
            _onEnemyDeath = HandleEnemyDeath;
            _onPlayerDestroyed = HandlePlayerDestroyed;
        }

        // After every Awake: the scene's entities are installed and the world has collected them.
        private void Start()
        {
            _startTime = Time.time;
            string levelId = AppServices.GameFlow.CurrentLevelId;
            _level = _levelCatalog != null && !string.IsNullOrEmpty(levelId) ? _levelCatalog.Find(levelId) : null;
            KillTarget = _level != null && _levelDesign != null ? _levelDesign.KillTarget(_level) : _defaultKillTarget;

            if (!GameContext.TryGetInstance(out GameContext context))
            {
                Debug.LogWarning($"{nameof(SandboxBattleStats)}: no GameContext in the scene, kills and the tank are not counted.", this);
                return;
            }

            _world = context.GetEntityWorld();
            foreach (IEntity entity in _world) // once at start, not in a tick
                Track(entity);
            _world.OnAdded += _onEntityAdded;
            _world.OnRemoved += _onEntityRemoved;

            _characterSubscription = context.GetPlayerContext().GetCharacter().Observe(HandleCharacterChanged);
        }

        private void OnDestroy()
        {
            _characterSubscription.Dispose();
            SetVehicleHealth(null);
            SetCharacterHealth(null);

            if (_world != null)
            {
                _world.OnAdded -= _onEntityAdded;
                _world.OnRemoved -= _onEntityRemoved;
                _world = null;
            }

            foreach (KeyValuePair<IEntity, IEvent<TakeDamageArgs>> enemy in _enemies)
                enemy.Value.OnEvent -= _onEnemyDeath;
            _enemies.Clear();
        }

        private void Track(IEntity entity)
        {
            if (entity == null || !entity.HasEnemyTag() || _enemies.ContainsKey(entity))
                return;
            if (!entity.TryGetHealth(out Health health) || !health.Exists())
                return;
            if (!entity.TryGetDeathTakenEvent(out IEvent<TakeDamageArgs> death))
                return;

            death.OnEvent += _onEnemyDeath;
            _enemies.Add(entity, death);
        }

        // A dead enemy leaves the world (DeathBehaviour); a pooled one may come back and is tracked again.
        private void Untrack(IEntity entity)
        {
            if (entity == null || !_enemies.TryGetValue(entity, out IEvent<TakeDamageArgs> death))
                return;

            death.OnEvent -= _onEnemyDeath;
            _enemies.Remove(entity);
        }

        private void HandleEnemyDeath(TakeDamageArgs args)
        {
            if (_finished)
                return;

            Kills++;
            Evaluate(false);
        }

        private void HandleCharacterChanged(IEntity character)
        {
            if (character == null)
            {
                SetCharacterHealth(null);
                return;
            }

            character.TryGetHealth(out Health health);
            if (character.HasVehicleTag())
            {
                // The tank stays "the player's tank" after the player gets out, until another vehicle.
                SetVehicleHealth(health);
                SetCharacterHealth(null);
            }
            else
            {
                SetCharacterHealth(health);
            }
        }

        private void SetVehicleHealth(Health health)
        {
            if (_vehicleHealth != null)
                _vehicleHealth.OnHealthEmpty -= _onPlayerDestroyed;
            _vehicleHealth = health;
            if (_vehicleHealth != null)
                _vehicleHealth.OnHealthEmpty += _onPlayerDestroyed;
        }

        private void SetCharacterHealth(Health health)
        {
            if (_characterHealth != null)
                _characterHealth.OnHealthEmpty -= _onPlayerDestroyed;
            _characterHealth = health;
            if (_characterHealth != null)
                _characterHealth.OnHealthEmpty += _onPlayerDestroyed;
        }

        private void HandlePlayerDestroyed() => Evaluate(true);

        private void Evaluate(bool playerDestroyed)
        {
            if (_finished)
                return;

            BattleOutcome outcome = BattleOutcomeUseCase.Decide(Kills, KillTarget, playerDestroyed);
            if (outcome == BattleOutcome.Ongoing)
                return;

            _finished = true;
            _endTime = Time.time;
            // The one reward of the battle: the results screen shows it and BattleFlow pays exactly it.
            int levelReward = _level != null && _levelDesign != null ? _levelDesign.Reward(_level) : LevelDesignConfig.DEFAULT_REWARD;
            int reward = BattleOutcomeUseCase.Reward(outcome, levelReward);
            Finished?.Invoke(new BattleResult(outcome == BattleOutcome.Victory, Kills, ElapsedTime, reward));
        }
    }
}
