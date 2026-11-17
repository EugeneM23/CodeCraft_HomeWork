using Atomic.Elements;
using Atomic.Entities;
using RayFire;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The tank drives through what can be broken: a destructible it runs into — a brick or
    /// concrete wall, a column, a glass pane — breaks around the point of contact the way a blast
    /// would break it (<see cref="BreachUseCase.Breach"/>), and the tank carries on through the
    /// hole. What a blast cannot break, the tank cannot either, and stops against it.
    ///
    /// Two ways in. Striking it: the impact speed of the contact. Pushing against it: the speed the
    /// driver is asking for, while the contact lies in the direction of travel — once the solver
    /// has stopped the hull the contact itself reports no speed at all.
    ///
    /// Loose pieces are not rammed: what is already flying or lying on the floor is only shoved
    /// aside by the hull.
    /// </summary>
    public sealed class TankRamBehaviour : IEntityInit, IEntityDispose
    {
        /// <summary>
        /// A contact counts as ahead when its normal, which points back at the tank, is at least
        /// this much against the direction of travel.
        /// </summary>
        private const float AHEAD = 0.3f;

        /// <summary>Contacts last many physics steps; a breach every step would only repeat itself.</summary>
        private const float COOLDOWN = 0.1f;

        /// <summary>The pieces leave at least this fast, so even a slow push clears the hole.</summary>
        private const float MIN_THROW = 3f;

        /// <summary>Full gas (Shift, stick all the way): the hole this much wider, the pieces this much faster.</summary>
        private const float FULL_GAS_RADIUS = 1.5f;
        private const float FULL_GAS_THROW = 1.6f;
        private const float FULL_STICK = 0.9f;

        private readonly CollisionContactReceiver _receiver;
        private readonly float _radius;
        private readonly float _minSpeed;
        private readonly float _splitChance;

        private Transform _hull;
        private IValue<Vector2> _input;
        private IValue<float> _maxSpeed;
        private IValue<bool> _boost;
        private float _readyAt;

        public TankRamBehaviour(CollisionContactReceiver receiver, float radius, float minSpeed, float splitChance)
        {
            _receiver = receiver;
            _radius = radius;
            _minSpeed = minSpeed;
            _splitChance = splitChance;
        }

        public void Init(IEntity entity)
        {
            _hull = entity.GetTransform();
            entity.TryGetDriveInput(out IReactiveVariable<Vector2> input);
            _input = input;
            _maxSpeed = entity.GetMoveSpeed();
            _boost = entity.TryGetDriveBoost(out IReactiveVariable<bool> boost) ? boost : null;

            _receiver.OnEntered += OnStruck;
            _receiver.OnStayed += OnPressing;
        }

        public void Dispose(IEntity entity)
        {
            _receiver.OnEntered -= OnStruck;
            _receiver.OnStayed -= OnPressing;
        }

        private void OnStruck(Collision collision) => TryRam(collision, collision.relativeVelocity.magnitude);

        private void OnPressing(Collision collision)
        {
            if (_input == null)
                return;

            float throttle = _input.Value.y;
            if (Mathf.Abs(throttle) < 0.1f)
                return;

            Vector3 travel = _hull.forward * Mathf.Sign(throttle);
            Vector3 normal = collision.GetContact(0).normal;

            if (Vector3.Dot(normal, travel) > -AHEAD)
                return;

            TryRam(collision, Mathf.Abs(throttle) * _maxSpeed.Value);
        }

        private void TryRam(Collision collision, float speed)
        {
            if (speed < _minSpeed || Time.time < _readyAt || !IsStanding(collision.collider))
                return;

            bool fullGas = FullGas;
            _readyAt = Time.time + COOLDOWN;
            float radius = _radius * (fullGas ? FULL_GAS_RADIUS : 1f);
            float thrown = Mathf.Max(speed + 2f, MIN_THROW) * (fullGas ? FULL_GAS_THROW : 1f);
            BreachUseCase.Breach(collision.GetContact(0).point, radius, thrown, _splitChance);
        }

        private bool FullGas => _boost != null && _boost.Value && _input != null && Mathf.Abs(_input.Value.y) >= FULL_STICK;

        /// <summary>A destructible that still stands: part of an intact wall, or a whole object not yet broken.</summary>
        private static bool IsStanding(Collider collider)
        {
            Rigidbody body = collider.attachedRigidbody;
            RayfireRigid rigid = body != null ? body.GetComponent<RayfireRigid>() : collider.GetComponent<RayfireRigid>();

            if (rigid == null || rigid.lim.demolished)
                return false;

            return body == null || body.isKinematic
                   || rigid.simTp == SimType.Kinematic || rigid.simTp == SimType.Inactive;
        }
    }
}
