using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The visible gun nods on its trunnion as the hull rocks: it keeps its own pitch for a moment while
    /// the hull pitches under it (starting off, braking, a bump, the recoil), then a stiff spring brings it
    /// back and it settles in a couple of swings. A jolt up or down the trunnion tips the heavy barrel the
    /// other way, and the gun nods down on braking and lifts when the tank speeds up. Never more than <c>limit</c> degrees off its rest.
    ///
    /// Only the visible gun turns. The fire point stays level on the turret pivot (see TurretAimBehaviour),
    /// so the nod costs no aim.
    /// </summary>
    public sealed class TankGunSwayBehaviour : IEntityInit, IEntityDispose, IEntityTick
    {
        /// <summary>Below this (rad, rad/s) the gun counts as resting and is left alone.</summary>
        private const float REST = 1e-4f;

        /// <summary>Share of its swing the gun keeps when it bounces off the cradle stop.</summary>
        private const float BOUNCE = 0.4f;

        private readonly Transform _gun;
        private readonly float _limit;
        private readonly float _stiffness;
        private readonly float _damping;
        private readonly float _heave;
        private readonly float _surge;

        private Rigidbody _body;
        private Quaternion _rest;
        private Vector3 _lastVelocity;
        private float _lift;
        private float _push;
        private float _angle;
        private float _rate;

        /// <param name="gun">The gun group, pivoting on the trunnion about its local X.</param>
        /// <param name="limit">Most it nods either way (degrees).</param>
        /// <param name="frequency">Its own swing (Hz).</param>
        /// <param name="dampingRatio">How fast the swing dies: 1 — no overshoot.</param>
        /// <param name="heave">Barrel tip per vertical jolt (rad per m/s² of the trunnion's acceleration).</param>
        /// <param name="surge">Nod per m/s² along the barrel (rad): up when speeding up, down when braking.</param>
        public TankGunSwayBehaviour(Transform gun, float limit, float frequency, float dampingRatio, float heave, float surge)
        {
            _gun = gun;
            _limit = limit * Mathf.Deg2Rad;
            float w = 2f * Mathf.PI * frequency;
            _stiffness = w * w;
            _damping = 2f * dampingRatio * w;
            _heave = heave * _stiffness;
            _surge = surge * _stiffness;
        }

        /// <summary>
        /// One step of the gun's pitch off its rest (rad, up positive) and its pitch rate in the world, for a
        /// hull pitching at <paramref name="hullRate"/> (rad/s, nose up positive), speeding up along the barrel at
        /// <paramref name="push"/> (m/s²) and a trunnion accelerating
        /// up at <paramref name="lift"/> (m/s²). At ±limit it hits the cradle stop and bounces back off it, losing most of its swing.
        /// </summary>
        public static void Step(ref float angle, ref float rate, float hullRate, float lift, float push, float stiffness,
            float damping, float heave, float surge, float limit, float deltaTime)
        {
            rate += (-stiffness * angle - damping * (rate - hullRate) - heave * lift + surge * push) * deltaTime;
            angle += (rate - hullRate) * deltaTime;
            if (angle > limit || angle < -limit)
            {
                angle = Mathf.Clamp(angle, -limit, limit);
                rate = hullRate - (rate - hullRate) * BOUNCE;
            }
        }

        public void Init(IEntity entity)
        {
            _body = entity.GetRiggedBody();
            _rest = _gun.localRotation;
            if (_body != null)
                _lastVelocity = _body.linearVelocity;
        }

        public void Dispose(IEntity entity)
        {
            if (_gun != null)
                _gun.localRotation = _rest;
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            if (_body == null || deltaTime <= 0f)
                return;

            Vector3 velocity = _body.linearVelocity;
            bool resting = _body.IsSleeping() && Mathf.Abs(_angle) < REST && Mathf.Abs(_rate) < REST;
            if (resting)
            {
                _lastVelocity = velocity;
                return;
            }

            Transform frame = _gun.parent;
            // About the turret's right axis a positive turn dips the nose: nose up is the negative.
            float hullRate = -Vector3.Dot(_body.angularVelocity, frame.right);
            // Physics steps do not land on every frame: the jolt is smoothed over a few of them.
            Vector3 change = (velocity - _lastVelocity) / deltaTime;
            float jolt = Vector3.Dot(change, frame.up);
            float push = Vector3.Dot(change, frame.forward);
            _lastVelocity = velocity;
            float smooth = 1f - Mathf.Exp(-deltaTime / 0.05f);
            _lift = Mathf.Lerp(_lift, jolt, smooth);
            _push = Mathf.Lerp(_push, push, smooth);

            Step(ref _angle, ref _rate, hullRate, _lift, _push, _stiffness, _damping, _heave, _surge, _limit, deltaTime);
            _gun.localRotation = _rest * Quaternion.Euler(-_angle * Mathf.Rad2Deg, 0f, 0f);
        }
    }
}
