using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Leans the visible body (HullVisual) with the flight: nose down speeding up, nose up
    /// slowing down, banked towards a sideways move and into a turn. Only the visual leans — the
    /// body and its colliders stay level.
    ///
    /// The acceleration is read off the body in FixedTick; two damped springs (pitch, roll) carry
    /// the visual to the lean in Tick, with a little overshoot that gives it weight.
    /// </summary>
    public sealed class HelicopterTiltBehaviour : IEntityInit, IEntityFixedTick, IEntityTick
    {
        /// <summary>A long frame must not kick the springs.</summary>
        private const float MAX_DELTA = 1f / 30f;

        private readonly float _degreesPerAcceleration;
        private readonly float _degreesPerSpeed;
        private readonly float _degreesPerYawRate;
        private readonly float _maxAngle;
        private readonly float _frequency;
        private readonly float _dampingRatio;

        private Rigidbody _body;
        private Transform _visual;

        private Vector3 _lastVelocity;
        private float _lastYaw;
        private Vector2 _target;

        private float _pitch;
        private float _pitchSpeed;
        private float _roll;
        private float _rollSpeed;

        // The lean last written and what the visual held right after. The same lean again, onto a
        // visual nobody has turned since, is not written: once a parked helicopter's springs come to
        // rest, rewriting it every frame only marked the whole visible body changed.
        private Quaternion _written;
        private Quaternion _held;
        private bool _leaning;

        public HelicopterTiltBehaviour(float degreesPerAcceleration, float degreesPerSpeed, float degreesPerYawRate,
            float maxAngle, float frequency, float dampingRatio)
        {
            _degreesPerAcceleration = degreesPerAcceleration;
            _degreesPerSpeed = degreesPerSpeed;
            _degreesPerYawRate = degreesPerYawRate;
            _maxAngle = maxAngle;
            _frequency = frequency;
            _dampingRatio = dampingRatio;
        }

        public void Init(IEntity entity)
        {
            _body = entity.GetRiggedBody();
            _visual = entity.GetHullVisual();
            _lastYaw = _body.rotation.eulerAngles.y;
        }

        public void FixedTick(IEntity entity, float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            Vector3 velocity = _body.linearVelocity;
            velocity.y = 0f;
            float yaw = _body.rotation.eulerAngles.y;

            Vector3 acceleration = (velocity - _lastVelocity) / deltaTime;
            float yawRate = Mathf.DeltaAngle(_lastYaw, yaw) / deltaTime;

            _lastVelocity = velocity;
            _lastYaw = yaw;

            Quaternion toLocal = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f));
            _target = HelicopterTiltMath.TargetAngles(toLocal * acceleration, toLocal * velocity, yawRate,
                _degreesPerAcceleration, _degreesPerSpeed, _degreesPerYawRate, _maxAngle);
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            float dt = Mathf.Min(deltaTime, MAX_DELTA);

            HelicopterTiltMath.Spring(ref _pitch, ref _pitchSpeed, _target.x, _frequency, _dampingRatio, dt);
            HelicopterTiltMath.Spring(ref _roll, ref _rollSpeed, _target.y, _frequency, _dampingRatio, dt);

            Quaternion lean = Quaternion.Euler(_pitch, 0f, _roll);
            if (_leaning && lean.Equals(_written) && _visual.localRotation.Equals(_held))
                return;

            _visual.localRotation = lean;
            _written = lean;
            _held = _visual.localRotation;
            _leaning = true;
        }
    }
}
