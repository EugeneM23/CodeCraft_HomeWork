using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The visible gun nods on its trunnion as the hull rocks. When the hull's pitch speeds up or slows down
    /// (starting off, braking, a bump, the recoil) the gun lags behind it by part of the turn — not all of it:
    /// a gun that kept its own pitch would take the whole of the hull's rocking on the springs, and that rocking
    /// is close to the gun's own swing and pumps it into the stops. It also lifts when the tank speeds up,
    /// nods when it brakes, and tips against a jolt up or down. A spring brings it back; it settles in a few
    /// swings, each smaller. Never more than <c>limit</c> degrees off its rest.
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

        /// <summary>Most acceleration (m/s²) the gun feels: a driven start or stop, not a shot's kick or a crash.</summary>
        private const float MAX_ACCELERATION = 6f;

        /// <summary>Most angular acceleration (rad/s²) of the hull's pitch the gun feels.</summary>
        private const float MAX_PITCH_ACCELERATION = 12f;

        /// <summary>Seconds over which the hull's motion reaches the gun.</summary>
        private const float SMOOTHING = 0.12f;

        private readonly Transform _gun;
        private readonly float _limit;
        private readonly float _stiffness;
        private readonly float _damping;
        private readonly float _inertia;
        private readonly float _heave;
        private readonly float _surge;

        private Rigidbody _body;
        private Quaternion _rest;
        private Vector3 _lastVelocity;
        private float _lastFixedTime;
        private float _jolt;
        private float _shove;
        private float _lift;
        private float _push;
        private float _hullRate;
        private float _angle;
        private float _rate;

        /// <param name="gun">The gun group, pivoting on the trunnion about its local X.</param>
        /// <param name="limit">Most it nods either way (degrees).</param>
        /// <param name="frequency">Its own swing (Hz).</param>
        /// <param name="dampingRatio">How fast the swing dies: 1 — no overshoot.</param>
        /// <param name="inertia">Share of the hull's change of pitch the gun lags behind (0 — none, 1 — all).</param>
        /// <param name="heave">Barrel tip per vertical jolt (rad per m/s² of the trunnion's acceleration).</param>
        /// <param name="surge">Nod per m/s² along the barrel (rad): up when speeding up, down when braking.</param>
        public TankGunSwayBehaviour(Transform gun, float limit, float frequency, float dampingRatio, float inertia,
            float heave, float surge)
        {
            _gun = gun;
            _limit = limit * Mathf.Deg2Rad;
            float w = 2f * Mathf.PI * frequency;
            _stiffness = w * w;
            _damping = 2f * dampingRatio * w;
            _inertia = inertia;
            _heave = heave * _stiffness;
            _surge = surge * _stiffness;
        }

        /// <summary>
        /// One step of the gun's pitch off its rest (rad, up positive) and its rate (rad/s), for a hull whose
        /// pitch (nose up positive) speeds up at <paramref name="hullAcceleration"/> (rad/s²), a tank speeding up
        /// along the barrel at <paramref name="push"/> (m/s²) and a trunnion accelerating up at
        /// <paramref name="lift"/> (m/s²). At ±limit it hits the cradle stop and bounces back off it, losing most
        /// of its swing.
        /// </summary>
        public static void Step(ref float angle, ref float rate, float hullAcceleration, float lift, float push,
            float stiffness, float damping, float inertia, float heave, float surge, float limit, float deltaTime)
        {
            rate += (-stiffness * angle - damping * rate - inertia * hullAcceleration - heave * lift + surge * push) *
                    deltaTime;
            angle += rate * deltaTime;
            if (angle > limit || angle < -limit)
            {
                angle = Mathf.Clamp(angle, -limit, limit);
                rate = -rate * BOUNCE;
            }
        }

        public void Init(IEntity entity)
        {
            _body = entity.GetRiggedBody();
            _rest = _gun.localRotation;
            if (_body != null)
                _lastVelocity = _body.linearVelocity;
            _lastFixedTime = Time.fixedTime;
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
                _lastFixedTime = Time.fixedTime;
                _hullRate = 0f;
                return;
            }

            Transform frame = _gun.parent;

            // The velocity only changes on a physics step: the acceleration is taken over the steps themselves
            // (frame by frame it would read zero, then twice the real value), capped — a shot's kick or a hard
            // landing is one step of tens of m/s² and would slam the gun into its stop — and smoothed.
            float fixedTime = Time.fixedTime;
            if (fixedTime > _lastFixedTime)
            {
                Vector3 change = (velocity - _lastVelocity) / (fixedTime - _lastFixedTime);
                _jolt = Mathf.Clamp(Vector3.Dot(change, frame.up), -MAX_ACCELERATION, MAX_ACCELERATION);
                _shove = Mathf.Clamp(Vector3.Dot(change, frame.forward), -MAX_ACCELERATION, MAX_ACCELERATION);
                _lastVelocity = velocity;
                _lastFixedTime = fixedTime;
            }

            float smooth = 1f - Mathf.Exp(-deltaTime / SMOOTHING);
            _lift = Mathf.Lerp(_lift, _jolt, smooth);
            _push = Mathf.Lerp(_push, _shove, smooth);

            // About the turret's right axis a positive turn dips the nose: nose up is the negative. Smoothed:
            // voxel ground knocks the hull's pitch rate about from step to step.
            float hullRate = Mathf.Lerp(_hullRate, -Vector3.Dot(_body.angularVelocity, frame.right), smooth);
            float hullAcceleration = Mathf.Clamp((hullRate - _hullRate) / deltaTime, -MAX_PITCH_ACCELERATION,
                MAX_PITCH_ACCELERATION);
            _hullRate = hullRate;

            Step(ref _angle, ref _rate, hullAcceleration, _lift, _push, _stiffness, _damping, _inertia, _heave,
                _surge, _limit, deltaTime);
            _gun.localRotation = _rest * Quaternion.Euler(-_angle * Mathf.Rad2Deg, 0f, 0f);
        }
    }
}
