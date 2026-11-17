using System;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Takes off, holds cruise height, flies by the stick and lands.
    ///
    /// Landed: gravity on, nothing driven, the body held where it stands. A pilot getting in, or a
    /// pilot left inside after a refused exit moving the stick, starts the take-off.
    /// TakingOff / Flying: gravity off, the stick (x → world X, y → world Z, as on foot) sets the
    /// wanted horizontal velocity and the height is pulled to ground + cruise.
    /// Landing: the stick is ignored, the speed runs out and the height is pulled to the ground;
    /// on touchdown <see cref="_onTouchdown"/> runs — the installer puts the pilot out there.
    ///
    /// Velocity is written into the body, not pushed with forces: the ramps (HelicopterFlightMath)
    /// are the inertia. A speed the body is not making — nosed into a wall — is dropped, so backing
    /// off starts from rest, as with the tank.
    ///
    /// The ground is the height the helicopter stood at when it was installed. No ray looks for
    /// it: the Playground floor is flat, and parts of it have no colliders a ray could find.
    /// </summary>
    public sealed class HelicopterFlightBehaviour : IEntityInit, IEntityFixedTick
    {
        /// <summary>How close to cruise height counts as there.</summary>
        private const float CRUISE_TOLERANCE = 0.1f;

        private readonly float _cruiseHeight;
        private readonly float _acceleration;
        private readonly float _braking;
        private readonly float _climbGain;
        private readonly float _maxClimbSpeed;
        private readonly float _verticalAcceleration;
        private readonly Action _onTouchdown;
        private readonly float _boostMultiplier;
        private readonly TouchdownDetector _touchdown = new();

        private Rigidbody _body;
        private IValue<Vector2> _input;
        private IValue<bool> _boost;
        private IValue<float> _maxSpeed;
        private IValue<IEntity> _driver;
        private Health _health;
        private IReactiveVariable<HelicopterFlightState> _state;
        private float _ground;

        private Vector3 _velocity;
        private IEntity _lastDriver;

        public HelicopterFlightBehaviour(float groundHeight, float cruiseHeight, float acceleration, float braking,
            float climbGain, float maxClimbSpeed, float verticalAcceleration, Action onTouchdown,
            float boostMultiplier = 1f)
        {
            _ground = groundHeight;
            _cruiseHeight = cruiseHeight;
            _acceleration = acceleration;
            _braking = braking;
            _climbGain = climbGain;
            _maxClimbSpeed = maxClimbSpeed;
            _verticalAcceleration = verticalAcceleration;
            _onTouchdown = onTouchdown;
            _boostMultiplier = Mathf.Max(1f, boostMultiplier);
        }

        public void Init(IEntity entity)
        {
            _body = entity.GetRiggedBody();
            _input = entity.GetDriveInput();
            _boost = entity.TryGetDriveBoost(out IReactiveVariable<bool> boost) ? boost : null;
            _maxSpeed = entity.GetMoveSpeed();
            _driver = entity.GetDriver();
            _health = entity.GetHealth();
            _state = entity.GetFlightState();
        }

        public void FixedTick(IEntity entity, float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            IEntity driver = _health.Exists() ? _driver.Value : null;
            bool newDriver = driver != null && driver != _lastDriver;
            _lastDriver = driver;

            switch (_state.Value)
            {
                case HelicopterFlightState.Landed:
                    if (driver != null && (newDriver || _input.Value.sqrMagnitude > 0.01f))
                    {
                        _state.Value = HelicopterFlightState.TakingOff;
                        break;
                    }

                    Hold();
                    return;

                case HelicopterFlightState.TakingOff:
                case HelicopterFlightState.Flying:
                    if (driver == null)
                        StartLanding();
                    break;
            }

            bool landing = _state.Value == HelicopterFlightState.Landing;

            _body.useGravity = false;

            // Horizontal: the stick, or nothing while landing.
            Vector2 stick = landing || driver == null ? Vector2.zero : Vector2.ClampMagnitude(_input.Value, 1f);
            // Shift: a higher top speed, reached as quickly as the normal one.
            float boost = _boost != null && _boost.Value && stick.sqrMagnitude > 0f ? _boostMultiplier : 1f;
            Vector3 wanted = new Vector3(stick.x, 0f, stick.y) * (_maxSpeed.Value * boost);

            Vector3 actual = _body.linearVelocity;
            actual.y = 0f;
            if (actual.sqrMagnitude < _velocity.sqrMagnitude)
                _velocity = actual;

            _velocity = HelicopterFlightMath.ApproachPlanar(_velocity, wanted, _acceleration * boost, _braking, deltaTime);

            // Vertical: to cruise height, or down to the ground.
            float y = _body.position.y;
            float targetY = landing ? _ground : _ground + _cruiseHeight;
            float climb = HelicopterFlightMath.ClimbSpeed(y, targetY, _climbGain, _maxClimbSpeed);
            float verticalSpeed = Mathf.MoveTowards(_body.linearVelocity.y, climb, _verticalAcceleration * deltaTime);

            _body.linearVelocity = new Vector3(_velocity.x, verticalSpeed, _velocity.z);

            if (_state.Value == HelicopterFlightState.TakingOff && Mathf.Abs(y - targetY) < CRUISE_TOLERANCE)
                _state.Value = HelicopterFlightState.Flying;

            if (landing && _touchdown.Step(y - _ground, _body.linearVelocity.y, deltaTime))
                TouchDown();
        }

        private void StartLanding()
        {
            _touchdown.Reset();
            _state.Value = HelicopterFlightState.Landing;
        }

        /// <summary>Sitting on the ground: gravity holds it down, the frictionless skids must not slide.</summary>
        /// <remarks>
        /// Each is written only when it differs. Writing a body's velocity wakes it, even with the
        /// value it already has, so the parked helicopter was kept awake every step; left alone, it
        /// falls asleep on its skids, and a pilot getting in, a hit or a blast wakes it again.
        /// </remarks>
        private void Hold()
        {
            if (!_body.useGravity)
                _body.useGravity = true;
            _velocity = Vector3.zero;

            if (_body.isKinematic)
                return;

            Vector3 velocity = _body.linearVelocity;
            Vector3 held = new Vector3(0f, Mathf.Min(velocity.y, 0f), 0f);
            if (!held.Equals(velocity))
                _body.linearVelocity = held;
        }

        private void TouchDown()
        {
            _state.Value = HelicopterFlightState.Landed;
            Hold();
            _onTouchdown?.Invoke();
        }

        /// <summary>Asked from the exit key while in the air.</summary>
        public void RequestLanding()
        {
            HelicopterFlightState state = _state.Value;

            if (state == HelicopterFlightState.TakingOff || state == HelicopterFlightState.Flying)
                StartLanding();
        }
    }
}
