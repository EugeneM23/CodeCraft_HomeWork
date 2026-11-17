using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Spins the main rotor about its up and the tail rotor about its right. They spin up while
    /// somebody is inside or the helicopter is off the ground, and wind down once it stands empty.
    /// </summary>
    public sealed class RotorSpinBehaviour : IEntityInit, IEntityTick
    {
        private readonly Transform _main;
        private readonly Transform _tail;
        private readonly float _mainSpeed;
        private readonly float _tailSpeed;
        private readonly float _spinUpTime;
        private readonly float _spinDownTime;

        private IValue<IEntity> _driver;
        private IValue<HelicopterFlightState> _state;
        private float _throttle;

        public RotorSpinBehaviour(Transform main, Transform tail, float mainSpeed, float tailSpeed, float spinUpTime,
            float spinDownTime)
        {
            _main = main;
            _tail = tail;
            _mainSpeed = mainSpeed;
            _tailSpeed = tailSpeed;
            _spinUpTime = Mathf.Max(0.01f, spinUpTime);
            _spinDownTime = Mathf.Max(0.01f, spinDownTime);
        }

        public void Init(IEntity entity)
        {
            _driver = entity.GetDriver();
            _state = entity.GetFlightState();
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            bool running = _driver.Value != null || _state.Value != HelicopterFlightState.Landed;

            _throttle = running
                ? Mathf.MoveTowards(_throttle, 1f, deltaTime / _spinUpTime)
                : Mathf.MoveTowards(_throttle, 0f, deltaTime / _spinDownTime);

            if (_throttle <= 0f)
                return;

            if (_main != null)
                _main.localRotation *= Quaternion.Euler(0f, _mainSpeed * _throttle * deltaTime, 0f);

            if (_tail != null)
                _tail.localRotation *= Quaternion.Euler(_tailSpeed * _throttle * deltaTime, 0f, 0f);
        }
    }
}
