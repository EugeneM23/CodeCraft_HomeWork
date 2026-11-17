using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Turns the helicopter's nose to the aim (RotateDirection, written from the cursor). The turn
    /// rate ramps up and down, so the nose swings round with some weight instead of snapping. On the
    /// ground, or with nobody inside, the heading stays where it is.
    /// </summary>
    public sealed class HelicopterYawBehaviour : IEntityInit, IEntityFixedTick
    {
        /// <summary>Turn rate (°/s) wanted per degree still to go: eases the nose in on the aim.</summary>
        private const float GAIN = 4f;

        private readonly float _turnAcceleration;

        private Rigidbody _body;
        private IValue<Vector3> _direction;
        private IValue<float> _maxTurnSpeed;
        private IValue<IEntity> _driver;
        private IValue<HelicopterFlightState> _state;

        private float _turnRate;

        public HelicopterYawBehaviour(float turnAcceleration) => _turnAcceleration = turnAcceleration;

        public void Init(IEntity entity)
        {
            _body = entity.GetRiggedBody();
            _direction = entity.GetRotateDirection();
            _maxTurnSpeed = entity.GetRotationSpeed();
            _driver = entity.GetDriver();
            _state = entity.GetFlightState();
        }

        public void FixedTick(IEntity entity, float deltaTime)
        {
            float wanted = 0f;
            float yaw = _body.rotation.eulerAngles.y;

            Vector3 direction = _direction.Value;
            direction.y = 0f;

            if (_driver.Value != null && _state.Value != HelicopterFlightState.Landed && direction.sqrMagnitude > 1e-6f)
            {
                float targetYaw = Quaternion.LookRotation(direction).eulerAngles.y;
                float max = _maxTurnSpeed.Value;
                wanted = Mathf.Clamp(Mathf.DeltaAngle(yaw, targetYaw) * GAIN, -max, max);
            }

            _turnRate = Mathf.MoveTowards(_turnRate, wanted, _turnAcceleration * deltaTime);

            // Knocks from blasts or walls must not set it spinning: the heading is this behaviour's.
            // Only an actual spin is cleared: zero written over zero changes nothing but wakes the
            // body, and it kept a parked helicopter awake every step.
            if (!_body.isKinematic)
            {
                Vector3 spin = _body.angularVelocity;
                if (spin.x != 0f || spin.y != 0f || spin.z != 0f)
                    _body.angularVelocity = Vector3.zero;
            }

            if (Mathf.Abs(_turnRate) > 1e-4f)
                _body.MoveRotation(Quaternion.Euler(0f, yaw + _turnRate * deltaTime, 0f));
        }
    }
}
