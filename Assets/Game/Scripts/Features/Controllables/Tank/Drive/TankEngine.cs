using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The tank's engine. Off while nobody is inside; once a driver gets in it cranks for
    /// <see cref="StartTime"/> seconds — the tank cannot drive yet, the exhaust belches ever bigger
    /// jets of smoke — and then runs. Getting out, or the hull going up, stops it at once.
    ///
    /// TankDriveBehaviour holds the stick at zero until it runs; TankExhaustBehaviour reads
    /// <see cref="State"/> and <see cref="StartProgress"/>. Ticks before both.
    /// </summary>
    public sealed class TankEngine : IEntityInit, IEntityTick
    {
        public enum EngineState { Off, Starting, Running }

        public const float StartTime = 2f;

        private IValue<IEntity> _driver;
        private Health _health;
        private float _cranking;

        public EngineState State { get; private set; }

        public bool Running => State == EngineState.Running;

        /// <summary>0 as the starter turns over, 1 as it catches. 1 while running, 0 while off.</summary>
        public float StartProgress => State switch
        {
            EngineState.Running => 1f,
            EngineState.Starting => Mathf.Clamp01(_cranking / StartTime),
            _ => 0f
        };

        public void Init(IEntity entity)
        {
            _driver = entity.GetDriver();
            _health = entity.GetHealth();
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            if (_driver.Value == null || !_health.Exists())
            {
                State = EngineState.Off;
                _cranking = 0f;
                return;
            }

            if (State == EngineState.Off)
            {
                State = EngineState.Starting;
                _cranking = 0f;
            }

            if (State == EngineState.Starting)
            {
                _cranking += deltaTime;
                if (_cranking >= StartTime)
                    State = EngineState.Running;
            }
        }
    }
}
