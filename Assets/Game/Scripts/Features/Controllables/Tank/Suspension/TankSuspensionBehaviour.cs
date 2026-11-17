using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Raycast suspension: every road wheel is a sphere cast down from its mount on the hull, and
    /// the spring between them pushes the hull up at the mount. Nothing else holds the tank off the
    /// ground, so it rests on the springs, sinks into craters, rocks over floor chips and leans
    /// under the drive's push and the cannon's kick.
    ///
    /// A sphere the size of the wheel, rather than a thin ray, so a crater rim is met by the
    /// wheel's rounded front and rolled over instead of being found only once the wheel is past it.
    ///
    /// Also keeps the tank from turning over: past <see cref="SOFT_TILT"/> a righting torque grows
    /// with the excess, strong enough that no blast or wall tips it much further.
    ///
    /// A parked tank — nobody inside, upright and standing still on its springs for
    /// <see cref="REST_DELAY"/> — is put to sleep, and while it sleeps nothing is cast and nothing
    /// pushes it (<see cref="TankSuspension.Resting"/>). It used to cast ten spheres and apply ten
    /// forces every step for a hull that did not move, and the forces alone kept the body awake for
    /// good. Gravity goes off with the springs that carried the weight, so a step in which a hit,
    /// a blast or a body walking into it wakes the tank drops it no more than it lifts it. Anything
    /// that wakes it, a new driver, or the ground under a wheel changing — looked at again
    /// every <see cref="REST_PROBE_INTERVAL"/> — gives gravity back, and that step runs as before.
    /// </summary>
    public sealed class TankSuspensionBehaviour : IEntityInit, IEntityFixedTick
    {
        /// <summary>
        /// What a wheel can stand on: not the tank itself or the player (9 Character), not enemies
        /// (8) or dummies (7) — the hull shoves those, it does not climb them — and not layer 6 (Interactable)
        /// or anything flying (11 ThrowItem, 12 Bullet, 13 Weapon). Floor, walls and platforms
        /// count; of debris and floor chips only what is not loose (see FindGround).
        /// Layer names: ProjectSettings/TagManager.asset.
        /// </summary>
        private const int GROUND_MASK = ~((1 << PhysicsLayers.Interactable) | (1 << PhysicsLayers.Dummy) | (1 << PhysicsLayers.Enemy) | (1 << PhysicsLayers.Character) | (1 << PhysicsLayers.ThrowItem) | (1 << PhysicsLayers.Bullet) | (1 << PhysicsLayers.Weapon));

        /// <summary>Lean (degrees) the hull may take freely; past it, it is pulled back up.</summary>
        private const float SOFT_TILT = 30f;

        /// <summary>
        /// How far (m) above a wheel's mount its ground cast starts. More than a puddle sinks the
        /// tank (a mud thickness, 0.32 m), so the cast never starts inside the floor.
        /// </summary>
        private const float CAST_LIFT = 0.4f;

        /// <summary>Righting acceleration (rad/s²) per radian past the soft limit.</summary>
        private const float UPRIGHT_STIFFNESS = 60f;

        /// <summary>Damping (1/s) of the lean's angular speed while it is being righted.</summary>
        private const float UPRIGHT_DAMPING = 8f;

        /// <summary>Same cap as the character's: an overlap must not launch the hull.</summary>
        private const float MAX_DEPENETRATION = 1f;

        /// <summary>Slower than this (m/s) the hull counts as standing still.</summary>
        private const float REST_SPEED = 0.01f;

        /// <summary>Slower than this (rad/s) the hull counts as not turning.</summary>
        private const float REST_SPIN = 0.01f;

        /// <summary>How long (s) a tank nobody drives has to stand still before it is put to sleep.</summary>
        private const float REST_DELAY = 0.5f;

        /// <summary>How often (s) a sleeping tank looks under its wheels again: a crater blown under it wakes it.</summary>
        private const float REST_PROBE_INTERVAL = 0.2f;

        /// <summary>A wheel's ground counts as changed once it is this much (m) nearer or further.</summary>
        private const float REST_PROBE_TOLERANCE = 0.002f;

        private Rigidbody _body;
        private TankSuspension _suspension;

        // Optional: a tank without a driver slot (tests) never rests.
        private IValue<IEntity> _driver;
        private float _stillFor;
        private float _sinceProbe;
        private bool _gravityBeforeRest;

        public void Init(IEntity entity)
        {
            _body = entity.GetRiggedBody();
            _suspension = entity.GetSuspension();
            _driver = entity.TryGetDriver(out IReactiveVariable<IEntity> driver) ? driver : null;

            _body.maxDepenetrationVelocity = MAX_DEPENETRATION;

            int wheels = _suspension.Wheels.Length;
            float gravity = Mathf.Abs(Physics.gravity.y);
            _suspension.Stiffness = SuspensionMath.Stiffness(_body.mass, gravity, wheels, _suspension.SagDistance);
            _suspension.Damping = SuspensionMath.Damping(_suspension.Stiffness, _body.mass, wheels, _suspension.DampingRatio);
        }

        public void FixedTick(IEntity entity, float deltaTime)
        {
            if (KeepResting(deltaTime))
                return;

            Vector3 up = _body.rotation * Vector3.up;

            // Before anything is pushed: a force applied in the step the body is put to sleep would
            // either be dropped or wake it again.
            if (StoodStillLongEnough(up, deltaTime))
            {
                Rest();
                return;
            }

            int grounded = 0;
            Vector3 contactSum = Vector3.zero;
            Vector3 normalSum = Vector3.zero;

            foreach (TankWheel wheel in _suspension.Wheels)
            {
                Vector3 mount = wheel.Mount.position;
                wheel.PreviousLength = wheel.Length;

                bool hit = FindGround(mount, -up, out RaycastHit ground);
                float sink = hit ? SinkUnder(ground) : 0f;

                wheel.Grounded = hit;
                wheel.Length = hit ? ground.distance + sink : _suspension.MaxLength;
                wheel.Ground = hit ? ground.collider : null;

                if (!hit)
                    continue;

                wheel.Contact = ground.point - up * sink;
                wheel.Normal = ground.normal;

                float force = SuspensionMath.Force(wheel.Length, wheel.PreviousLength, deltaTime,
                    _suspension.MaxLength, _suspension.MinLength, _suspension.Stiffness, _suspension.Damping);

                _body.AddForceAtPosition(up * force, mount, ForceMode.Force);

                grounded++;
                contactSum += ground.point;
                normalSum += ground.normal;
            }

            _suspension.GroundedCount = grounded;
            _suspension.ContactCentre = grounded > 0 ? contactSum / grounded : _body.worldCenterOfMass;
            _suspension.GroundNormal = grounded > 0 ? normalSum.normalized : Vector3.up;

            KeepUpright(up);
        }

        /// <summary>
        /// True while the parked tank sleeps on: the step is skipped. Otherwise ends the rest —
        /// woken by anything at all, a driver in, the ground under a wheel changed — and the step
        /// runs as normal.
        /// </summary>
        private bool KeepResting(float deltaTime)
        {
            if (!_suspension.Resting)
                return false;

            bool driven = _driver != null && _driver.Value != null;
            if (!driven && _body.IsSleeping() && !GroundChanged(deltaTime))
                return true;

            _suspension.Resting = false;
            _stillFor = 0f;
            _body.useGravity = _gravityBeforeRest;
            _body.WakeUp();
            return false;
        }

        /// <summary>
        /// Nobody inside, upright, and slower than <see cref="REST_SPEED"/> and
        /// <see cref="REST_SPIN"/> for <see cref="REST_DELAY"/> on end. Upright, because past
        /// <see cref="SOFT_TILT"/> the righting torque is still at work.
        /// </summary>
        private bool StoodStillLongEnough(Vector3 up, float deltaTime)
        {
            bool still = _driver != null && _driver.Value == null && !_body.isKinematic
                         && _body.linearVelocity.sqrMagnitude < REST_SPEED * REST_SPEED
                         && _body.angularVelocity.sqrMagnitude < REST_SPIN * REST_SPIN
                         && Vector3.Angle(up, Vector3.up) <= SOFT_TILT;

            _stillFor = still ? _stillFor + deltaTime : 0f;
            return _stillFor >= REST_DELAY;
        }

        private void Rest()
        {
            _gravityBeforeRest = _body.useGravity;
            _body.useGravity = false;
            _body.Sleep();

            _suspension.Resting = true;
            _sinceProbe = 0f;
        }

        /// <summary>
        /// Every <see cref="REST_PROBE_INTERVAL"/>: whether any wheel would now find other ground
        /// than it stood on when the tank went to sleep — a crater blown under it, the piece it stood
        /// on knocked loose. Nothing touches the hull then, so nothing else would wake it.
        /// </summary>
        private bool GroundChanged(float deltaTime)
        {
            _sinceProbe += deltaTime;
            if (_sinceProbe < REST_PROBE_INTERVAL)
                return false;

            _sinceProbe = 0f;
            Vector3 down = -(_body.rotation * Vector3.up);

            foreach (TankWheel wheel in _suspension.Wheels)
            {
                bool hit = FindGround(wheel.Mount.position, down, out RaycastHit ground);

                if (hit != wheel.Grounded)
                    return true;

                if (hit && (ground.collider != wheel.Ground ||
                            Mathf.Abs(ground.distance + SinkUnder(ground) - wheel.Length) > REST_PROBE_TOLERANCE))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The nearest thing under the wheel that can carry it. Loose bodies — wall pieces knocked
        /// out, floor chips, casings, a crate — are not ground: a wheel standing on a brick would
        /// lift the hull onto a pile of rubble, tip it and hang it up. The hull shoves loose things
        /// aside instead, and the wheels roll on the floor beneath them.
        /// </summary>
        private bool FindGround(Vector3 mount, Vector3 down, out RaycastHit ground)
        {
            // Cast from above the mount: sunk into a puddle the mount comes down to the floor's
            // collider, and a sphere starting inside the floor would not find it at all. Distances
            // are then measured from the mount again.
            int count = Physics.SphereCastNonAlloc(mount - down * CAST_LIFT, _suspension.WheelRadius, down, Hits,
                _suspension.MaxLength + CAST_LIFT, GROUND_MASK, QueryTriggerInteraction.Ignore);

            ground = default;
            float nearest = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];

                // Something the sphere already overlaps at the mount: SphereCast left these out too.
                if (hit.distance <= 0f && hit.point == Vector3.zero)
                    continue;

                Rigidbody body = hit.rigidbody;
                if (body != null && !body.isKinematic)
                    continue;

                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    ground = hit;
                }
            }

            if (nearest == float.MaxValue)
                return false;

            ground.distance = Mathf.Max(0f, ground.distance - CAST_LIFT);
            return true;
        }

        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        // The last collider looked up for mud, and its MudSurface (null for none): the wheels
        // nearly always stand on the same floor, so this spares a GetComponent per wheel per step.
        private Collider _mudCollider;
        private MudSurface _mud;

        /// <summary>
        /// How far below <paramref name="ground"/> the wheel really stands: in mud, under a puddle,
        /// the bottom is deeper than the floor's collider (MudSurface.SinkAt) and the track sinks
        /// into it. Nothing elsewhere.
        /// </summary>
        private float SinkUnder(RaycastHit ground)
        {
            if (ground.collider != _mudCollider)
            {
                _mudCollider = ground.collider;
                _mudCollider.TryGetComponent(out _mud);
            }

            return _mud != null ? _mud.SinkAt(ground.point) : 0f;
        }

        private void KeepUpright(Vector3 up)
        {
            float tilt = Vector3.Angle(up, Vector3.up);
            if (tilt <= SOFT_TILT)
                return;

            Vector3 axis = Vector3.Cross(up, Vector3.up);
            if (axis.sqrMagnitude < 1e-8f)
                axis = _body.rotation * Vector3.right; // upside down: any horizontal axis rights it

            axis.Normalize();

            float excess = (tilt - SOFT_TILT) * Mathf.Deg2Rad;
            float righting = Vector3.Dot(_body.angularVelocity, axis);

            _body.AddTorque(axis * (UPRIGHT_STIFFNESS * excess - UPRIGHT_DAMPING * righting), ForceMode.Acceleration);
        }
    }
}
