using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Tank drive on top of the raycast suspension: the throttle runs the hull along its own
    /// forward over the ground, the turn stick spins it about its own up.
    ///
    /// The speed is not written into the body. The change of velocity it takes is applied as a
    /// force at the centre of the tracks' contact patch, below the centre of mass. Linearly that
    /// is the same thing; it also gives the torque real tracks give, so the hull squats when
    /// pulling away, dips when braking and leans out of a turn on its springs by itself. The same
    /// push removes any sideways slide at once: tracks hold.
    ///
    /// Except at high speed. Going at least <see cref="_driftSpeed"/> along the hull, the tracks take
    /// the slide out at no more than <see cref="_sideGrip"/> m/s². Holding the line in a turn takes
    /// speed × turn rate sideways, so only a sharp turn at that speed asks for more; the hull then
    /// slides out by just the excess, and grips again as soon as the turn eases. The cap is high
    /// enough that a bump's small slip still goes in one step.
    ///
    /// Grip is the share of wheels on the ground. With none, nothing is driven: a tank in the air
    /// cannot steer.
    ///
    /// The speeds still ramp (TankDriveMath), and a speed the hull is not actually making — nosed
    /// into a wall — is dropped, so backing off starts from rest.
    ///
    /// Shift is engine power: <see cref="_boostMultiplier"/> times the top speed, the turn rate and
    /// both ramps while the stick is off centre. Braking is the brakes', not the engine's: unchanged.
    ///
    /// The squat and the dip are then made <see cref="_pitchLean"/> times stronger: the push along
    /// the hull is given that much longer a lever below the centre of mass, as an added torque
    /// only, so the speeds and the lean in a turn stay as they are.
    /// </summary>
    public sealed class TankDriveBehaviour : IEntityInit, IEntityFixedTick
    {
        private readonly float _acceleration;
        private readonly float _braking;
        private readonly float _turnAcceleration;
        private readonly float _pitchLean;
        private readonly float _boostMultiplier;
        private readonly float _driftSpeed;
        private readonly float _sideGrip;

        private bool _drifting;

        /// <summary>Last step the tracks could not take the whole slide out: the hull is drifting.</summary>
        public bool Drifting => _drifting;

        private Rigidbody _body;
        private TankSuspension _suspension;
        private IValue<Vector2> _input;
        private IValue<bool> _boost;
        private IValue<float> _maxSpeed;
        private IValue<float> _turnSpeed;
        private IValue<IEntity> _driver;
        private Health _health;

        private float _speed;
        private float _turnRate;

        /// <summary>Holds the stick at zero until it runs. None: always running (the tests).</summary>
        private readonly TankEngine _engine;

        public TankDriveBehaviour(float acceleration, float braking, float turnAcceleration, float pitchLean = 1f,
            float boostMultiplier = 1f, TankEngine engine = null, float driftSpeed = float.PositiveInfinity,
            float sideGrip = float.PositiveInfinity)
        {
            _driftSpeed = Mathf.Max(0f, driftSpeed);
            _sideGrip = Mathf.Max(0f, sideGrip);
            _engine = engine;
            _acceleration = acceleration;
            _braking = braking;
            _turnAcceleration = turnAcceleration;
            _pitchLean = Mathf.Max(0f, pitchLean);
            _boostMultiplier = Mathf.Max(1f, boostMultiplier);
        }

        public void Init(IEntity entity)
        {
            _body = entity.GetRiggedBody();
            _suspension = entity.GetSuspension();
            _input = entity.GetDriveInput();
            _boost = entity.TryGetDriveBoost(out IReactiveVariable<bool> boost) ? boost : null;
            _maxSpeed = entity.GetMoveSpeed();
            _turnSpeed = entity.GetRotationSpeed();
            _driver = entity.GetDriver();
            _health = entity.GetHealth();
        }

        public void FixedTick(IEntity entity, float deltaTime)
        {
            // Parked and put to sleep by the suspension, which runs first: there is nothing to brake,
            // and the push or the spin written below would only wake the body again.
            if (_suspension.Resting && _driver.Value == null)
                return;

            bool engine = _engine == null || _engine.Running;
            Vector2 input = engine && _driver.Value != null && _health.Exists() ? _input.Value : Vector2.zero;
            input.x = Mathf.Clamp(input.x, -1f, 1f);
            input.y = Mathf.Clamp(input.y, -1f, 1f);

            Vector3 ground = _suspension.GroundNormal;
            Vector3 forward = Vector3.ProjectOnPlane(_body.rotation * Vector3.forward, ground);
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : _body.rotation * Vector3.forward;

            float actual = Vector3.Dot(_body.linearVelocity, forward);
            if (Mathf.Abs(actual) < Mathf.Abs(_speed))
                _speed = actual;

            // Shift: a stronger engine, not a shove. Whatever the stick asks for comes quicker and goes
            // further — the speed and the turn rate both, and how fast each is reached — so a boosted
            // turn traces the same arc, only faster. With the stick centred Shift asks for nothing.
            float boost = _boost != null && _boost.Value && input != Vector2.zero ? _boostMultiplier : 1f;
            _speed = TankDriveMath.Approach(_speed, input.y * _maxSpeed.Value * boost, _acceleration * boost, _braking,
                deltaTime);
            _turnRate = Mathf.MoveTowards(_turnRate, input.x * _turnSpeed.Value * boost,
                _turnAcceleration * boost * deltaTime);

            float grip = _suspension.Grip;
            if (grip <= 0f || deltaTime <= 0f)
                return;

            // Along the ground: the wanted velocity, minus what the hull already has in that plane.
            Vector3 planar = Vector3.ProjectOnPlane(_body.linearVelocity, ground);
            Vector3 change = (forward * _speed - planar) * grip;

            // At high speed the tracks hold sideways only up to _sideGrip: a sharp turn slides out by the
            // excess. Below the drift speed the limit climbs with (driftSpeed / speed)², so a drift that
            // slows past it grips back smoothly instead of snapping straight in one step.
            _drifting = false;
            Vector3 side = Vector3.Cross(ground, forward);
            float moving = Mathf.Abs(actual);
            if (!float.IsPositiveInfinity(_sideGrip) && moving > 0.5f * _driftSpeed && side.sqrMagnitude > 1e-6f)
            {
                side.Normalize();
                float lateral = Vector3.Dot(change, side);
                float ratio = Mathf.Max(1f, _driftSpeed / moving);
                float hold = _sideGrip * ratio * ratio * grip * deltaTime;
                if (Mathf.Abs(lateral) > hold)
                {
                    _drifting = true;
                    change += side * (Mathf.Clamp(lateral, -hold, hold) - lateral);
                }
            }

            Vector3 push = change * (_body.mass / deltaTime);
            _body.AddForceAtPosition(push, _suspension.ContactCentre, ForceMode.Force);

            Vector3 up = _body.rotation * Vector3.up;

            // The rest of the squat and the dip: the same push along the hull, from a lever
            // (_pitchLean - 1) times as long again below the centre of mass.
            float drop = Vector3.Dot(_body.worldCenterOfMass - _suspension.ContactCentre, up);
            if (drop > 0f && !Mathf.Approximately(_pitchLean, 1f))
            {
                Vector3 along = forward * Vector3.Dot(push, forward);
                _body.AddTorque(Vector3.Cross(-up * (drop * (_pitchLean - 1f)), along), ForceMode.Force);
            }

            // Spin about the hull's own up; pitch and roll stay the springs' business.
            Vector3 spin = _body.angularVelocity;
            float yaw = Vector3.Dot(spin, up);
            _body.angularVelocity = spin + up * ((_turnRate * Mathf.Deg2Rad - yaw) * grip);
        }
    }
}
