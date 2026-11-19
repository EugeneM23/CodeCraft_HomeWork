using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// Lets a heavy vehicle push through voxel walls. A building (a body with a load table) gives way only
    /// to a ram at <see cref="_breakSpeed"/> or more: the wall ahead is broken before the bow gets there,
    /// a hole the bow's size, the debris thrown on ahead, the speed paying for what broke; the skeleton
    /// (piers, corners) goes too near top speed (BreakAhead). Anything else (a tree) is crushed at the
    /// contact from <see cref="_minSpeed"/>; flying at a wall, it bites the wall ahead before it gets
    /// there (<see cref="Airborne"/>). Put it next to the Rigidbody.
    /// </summary>
    /// <remarks>
    /// The speed the vehicle wants, not the speed it has: a drive that sets the velocity every
    /// physics step is stopped dead by the wall's collider, so the post-solve velocity is zero
    /// exactly when it matters. FixedUpdate runs late (execution order), after the drive has set
    /// this step's velocity and before the step is simulated.
    /// </remarks>
    [DefaultExecutionOrder(5000)]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class VoxelRammer : MonoBehaviour, IVoxelFixedUpdate
    {
        [SerializeField] private float _minSpeed = 1.2f;
        [Tooltip("Speed (m/s) at which the ram hits as hard as it can.")]
        [SerializeField] private float _fullSpeed = 6f;
        [Tooltip("Radius (m) of a bite at the slowest ram, and at full speed.")]
        [SerializeField] private float _radius = 0.55f;
        [SerializeField] private float _fullRadius = 1.3f;
        [Tooltip("Seconds between two bites at the slowest ram, and at full speed.")]
        [SerializeField] private float _interval = 0.12f;
        [SerializeField] private float _fullInterval = 0.04f;
        [Tooltip("Half the width (m) of the bow: a fast ram bites across all of it, not just at the contact.")]
        [SerializeField] private float _halfWidth = 0.75f;

        [Header("Full gas")]
        [Tooltip("On full gas (FullGas) the bites are this much bigger…")]
        [SerializeField] private float _fullGasRadius = 1.45f;
        [Tooltip("…throw the voxels this much harder…")]
        [SerializeField] private float _fullGasForce = 1.7f;
        [Tooltip("…and come this much more often.")]
        [SerializeField] private float _fullGasInterval = 0.6f;

        /// <summary>
        /// Set by the vehicle's drive: the driver is flooring it (Shift, stick all the way). The ram
        /// then bites bigger and more often, across the whole bow and a bite deeper ahead.
        /// </summary>
        public bool FullGas { get; set; }

        [Header("In the air")]
        [Tooltip("Flying (Airborne) at least this fast (m/s, along the ground), the ram bites the wall it is about to hit before it gets there.")]
        [SerializeField] private float _flyThroughSpeed = 4f;
        [Tooltip("Seconds of flight ahead it looks: long enough for the wall's colliders to be rebuilt (at the end of the frame) before the vehicle arrives.")]
        [SerializeField] private float _lookAhead = 0.12f;
        [Tooltip("Mass (kg) of one voxel the flying vehicle knocks out of a wall: 10 cm of brick.")]
        [SerializeField] private float _voxelMass = 1.8f;
        [Tooltip("Share of that mass it carries along with it: its speed along the ground drops by that momentum. 0 — walls cost nothing, and it flies through a house and out the far side; 0.05 — a tank jumping at 10 m/s comes down 1.5–3 m inside; 0.15 — just inside the wall.")]
        [SerializeField] private float _wallDrag = 0.05f;

        [Header("Through a building")]
        [Tooltip("Slowest (m/s, along the ground) the bow breaks through a building's wall (anything with a load table); slower, it stops against it. The drive's pull against a wall does not count: only the speed the hull has.")]
        [SerializeField] private float _breakSpeed = 3.5f;
        [Tooltip("From this speed (m/s, about top speed; full gas counts 15 % more) the bow takes the skeleton (piers, corners, beams) too. Slower, it breaks the walls between and stops at a pier.")]
        [SerializeField] private float _skeletonSpeed = 5.5f;
        [Tooltip("Energy (J) one voxel of wall costs the vehicle: the tank's first hole into a house (wall, plinth, floor: ~1900 voxels) slows a 3 t tank from 6 to about 4 m/s, and stops one doing 4.")]
        [SerializeField] private float _voxelEnergy = 15f;
        [Tooltip("The hole reaches this far (m) beyond the bow's colliders at the sides and the top: a building collides in 20 cm cells, and a voxel left at the edge is a cell in the way.")]
        [SerializeField] private float _holeMargin = 0.3f;
        [Tooltip("…and this far below them: under the bow, not deep into the foundation of the house (it rides over it on its tracks).")]
        [SerializeField] private float _holeFloor = 0.25f;
        [Tooltip("How deep (m) one hole goes into the wall.")]
        [SerializeField] private float _holeDepth = 0.5f;
        [Tooltip("The bow breaks again once it has gone this far (m) on.")]
        [SerializeField] private float _holeStep = 0.3f;
        [Tooltip("Most debris cubes one hole throws.")]
        [SerializeField] private int _holeDebris = 500;
        [Tooltip("Stopped by a building (its speed spent breaking, or pushing against it slower than the break speed), the bow breaks nothing more until the way ahead is clear this far (m): it backs off and rams again, it does not bulldoze on at a crawl with the drive's pull.")]
        [SerializeField] private float _rearmDistance = 1.5f;

        /// <summary>Whether a ram may break a building now (see <see cref="_rearmDistance"/>).</summary>
        public bool Armed { get; private set; } = true;

        /// <summary>
        /// Set by the vehicle: none of its wheels touch the ground. A ram only bites on contact,
        /// after the solver has already stopped the hull against the wall; on the ground the drive
        /// keeps pushing it on into the bite, but in the air nothing does, and a vehicle jumping
        /// at a building would stop dead and drop. Flying, the ram looks ahead along its flight and
        /// bites the wall before the hull reaches it: the hull flies on into the building. It
        /// bites only what faces it — a wall, not a floor it is coming down on.
        /// </summary>
        public bool Airborne { get; set; }

        private Rigidbody _rigidbody;
        private Vector3 _intended;
        private float _next;
        private float _nextHole;

        /// <summary>The bow: the box round the vehicle's own colliders, in its local space (Awake).</summary>
        private Vector3 _bowMin, _bowMax;

        /// <summary>Voxels the last hole took (tests).</summary>
        public int LastHoleVoxels { get; private set; }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            MeasureBow();
        }

        /// <summary>The box round the colliders the body carries (not triggers), in its own space.</summary>
        private void MeasureBow()
        {
            bool any = false;
            Matrix4x4 toLocal = transform.worldToLocalMatrix;
            foreach (Collider part in GetComponentsInChildren<Collider>())
            {
                if (part.isTrigger || part.attachedRigidbody != _rigidbody)
                    continue;
                Bounds b = part.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 world = new Vector3((corner & 1) == 0 ? b.min.x : b.max.x, (corner & 2) == 0 ? b.min.y : b.max.y,
                        (corner & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 local = toLocal.MultiplyPoint3x4(world);
                    _bowMin = any ? Vector3.Min(_bowMin, local) : local;
                    _bowMax = any ? Vector3.Max(_bowMax, local) : local;
                    any = true;
                }
            }

            if (!any)
            {
                _bowMin = new Vector3(-_halfWidth, 0.3f, -1f);
                _bowMax = new Vector3(_halfWidth, 1.2f, 1f);
            }
        }

        private void OnEnable() => VoxelTicks.Add(this);

        private void OnDisable() => VoxelTicks.Remove(this);

        private void FixedUpdate() // perf-ok: ticks itself without the game loop (tests, demo scenes); one flag check when driven
        {
            if (!VoxelTicks.Driven)
                VoxelFixedUpdate(Time.fixedDeltaTime);
        }

        public void VoxelFixedUpdate(float deltaTime)
        {
            _intended = _rigidbody.linearVelocity;
            if (Airborne)
                BiteAhead();
            else
                BreakAhead(deltaTime);
        }

        /// <summary>
        /// On the ground, at least <see cref="_breakSpeed"/>: a building's wall the bow is about to hit is
        /// broken before it gets there (the hull's colliders would stop it dead on contact), a hole the size
        /// of the bow, and the speed pays for what it broke. Slower, nothing: the hull stops against the
        /// wall however hard the drive pulls (user, 2026-11-20: a ram breaks a house at medium or high speed).
        /// </summary>
        private void BreakAhead(float deltaTime)
        {
            if (!Armed)
            {
                Rearm();
                return;
            }

            if (VoxelTicks.Time < _nextHole)
                return;

            Vector3 velocity = _rigidbody.linearVelocity;
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            float speed = flat.magnitude;
            if (speed < _breakSpeed)
                return;

            Vector3 forward = flat / speed;
            float ahead = WallAhead(forward, speed * (_lookAhead + deltaTime));
            if (ahead >= 0f)
                BreakThrough(ahead, forward, speed);
        }

        /// <summary>Armed again once nothing of a building lies within <see cref="_rearmDistance"/> ahead of the bow.</summary>
        private void Rearm()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f)
                return;
            if (WallAhead(forward.normalized, _rearmDistance) < 0f)
                Armed = true;
        }

        private readonly RaycastHit[] _hits = new RaycastHit[16];

        /// <summary>
        /// How far (m) along <paramref name="forward"/> the bow's box meets a building's surface facing it, within
        /// <paramref name="distance"/>; −1 for none. Every hit is looked at, not just the first: through a hole the
        /// first is often its own edge or floor, which faces the bow no more than the ground does.
        /// </summary>
        private float WallAhead(Vector3 forward, float distance)
        {
            Vector3 centre = transform.TransformPoint((_bowMin + _bowMax) * 0.5f);
            Vector3 extents = (_bowMax - _bowMin) * 0.5f;
            int count = Physics.BoxCastNonAlloc(centre, extents, forward, _hits, transform.rotation, distance, ~0,
                QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                // Its own colliders, and anything it already overlaps (distance 0), are not ahead of it.
                if (hit.distance <= 0f || hit.distance >= nearest || hit.collider.attachedRigidbody == _rigidbody)
                    continue;
                // A wall faces the bow; the ground, a ramp or a floor under it does not.
                if (Vector3.Dot(hit.normal, forward) > -0.3f)
                    continue;
                VoxelBody body = hit.collider.GetComponentInParent<VoxelBody>(); // perf-ok: only on a hit ahead, ramming
                if (body == null || !body.HasStrength || body.IsTerrain || body.transform.IsChildOf(transform))
                    continue;
                nearest = hit.distance;
            }

            return nearest == float.MaxValue ? -1f : nearest;
        }

        /// <summary>
        /// The hole: a box the bow's shape (its colliders and a margin round them), <see cref="_holeDepth"/> deep,
        /// where the bow will be after <paramref name="ahead"/> metres along <paramref name="forward"/>; the debris
        /// thrown on ahead of the vehicle; the skeleton taken too from <see cref="_skeletonSpeed"/>. Then the speed
        /// drops by the energy the broken voxels cost (<see cref="_voxelEnergy"/>).
        /// </summary>
        private void BreakThrough(float ahead, Vector3 forward, float speed)
        {
            float half = Mathf.Max(Mathf.Abs(_bowMin.x), Mathf.Abs(_bowMax.x)) + _holeMargin;
            float bottom = _bowMin.y - _holeFloor;
            float top = _bowMax.y + _holeMargin;
            // The bow's face, where it will meet the wall: the front of the colliders, carried ahead; the box starts
            // a little short of it, so nothing is left standing in the gap.
            Vector3 face = transform.TransformPoint(new Vector3(0f, 0f, _bowMax.z)) + forward * ahead;
            Vector3 centre = face + forward * (_holeDepth * 0.5f - 0.1f);
            centre.y = transform.position.y + (bottom + top) * 0.5f;
            var halfSize = new Vector3(half, (top - bottom) * 0.5f, _holeDepth * 0.5f);

            bool skeleton = (FullGas ? speed * 1.15f : speed) >= _skeletonSpeed;
            Vector3 throwVelocity = forward * (speed * 0.8f) + Vector3.up * 1.5f;
            int voxels = VoxelDamage.Ram(centre, Quaternion.LookRotation(forward, Vector3.up), halfSize, throwVelocity,
                skeleton, transform, _holeDebris);
            LastHoleVoxels = voxels;
            _nextHole = VoxelTicks.Time + _holeStep / speed;
            if (voxels == 0)
                return;

            // What it broke costs it: ½ m v² less the voxels' energy. Spent, it stops in the hole it made, and
            // the drive pushes it on through at a crawl.
            float mass = _rigidbody.mass;
            float v2 = speed * speed - 2f * voxels * _voxelEnergy / mass;
            float keep = v2 > 0f ? Mathf.Sqrt(v2) / speed : 0f;
            Vector3 velocity = _rigidbody.linearVelocity;
            _rigidbody.linearVelocity = new Vector3(velocity.x * keep, velocity.y, velocity.z * keep);
            // Spent: it rolls on into its hole at what it has left, but breaks nothing more till it rams again.
            if (speed * keep < _breakSpeed)
                Armed = false;
        }

        private void BiteAhead()
        {
            if (VoxelTicks.Time < _next)
                return;

            Vector3 velocity = _rigidbody.linearVelocity;
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            float speed = flat.magnitude;
            if (speed < _flyThroughSpeed)
                return;

            float travel = velocity.magnitude;
            Vector3 heading = velocity / travel;
            Vector3 forward = flat / speed;

            // Allocates, but only while flying fast: a jump, not every step.
            RaycastHit[] hits = _rigidbody.SweepTestAll(heading, travel * (_lookAhead + Time.fixedDeltaTime),
                QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;
            Vector3 point = default;
            foreach (RaycastHit hit in hits)
            {
                // A wall faces the flight; a floor or a roof under it does not.
                if (hit.distance >= nearest || Vector3.Dot(hit.normal, forward) > -0.3f)
                    continue;

                VoxelBody body = hit.collider.GetComponentInParent<VoxelBody>();
                if (body == null || body.transform.IsChildOf(transform) || body.IsTerrain)
                    continue;

                nearest = hit.distance;
                point = hit.point;
            }

            if (nearest == float.MaxValue)
                return;

            // The wall gives way, but what it was made of has to be shoved aside: the speed along
            // the ground drops as if the vehicle took a share of the bricks along with it. A wall
            // costs some of it; a house full of floors and furniture brings it down inside.
            int voxels = Bite(point, heading, speed);
            float mass = _rigidbody.mass;
            float keep = mass / (mass + voxels * _voxelMass * _wallDrag);
            _rigidbody.linearVelocity = new Vector3(velocity.x * keep, velocity.y, velocity.z * keep);
        }

        private void OnCollisionEnter(Collision collision) => Ram(collision);

        private void OnCollisionStay(Collision collision) => Ram(collision);

        private void Ram(Collision collision)
        {
            if (VoxelTicks.Time < _next)
                return;

            Vector3 flat = new Vector3(_intended.x, 0f, _intended.z);
            float speed = flat.magnitude;
            if (speed < _minSpeed)
                return;

            VoxelBody body = collision.collider.GetComponentInParent<VoxelBody>();
            // Not its own skin, and not the ground it drives on (a hull scraping a crater's edge).
            if (body == null || body.transform.IsChildOf(transform) || body.IsTerrain)
                return;

            // A building gives way only to a ram at speed, and then as a hole the bow's size (BreakAhead
            // breaks it before contact; this is a wall the look ahead missed). Slower, the hull stops.
            if (body.HasStrength)
            {
                if (speed >= _breakSpeed && Armed && VoxelTicks.Time >= _nextHole && !Airborne)
                    BreakThrough(0f, flat / speed, speed);
                return;
            }

            Vector3 direction = flat / speed;
            ContactPoint contact = collision.GetContact(0);

            // Only what lies ahead: scraping along a wall does not dig into it.
            if (Vector3.Dot(contact.point - _rigidbody.worldCenterOfMass, direction) <= 0f)
                return;

            Bite(contact.point, direction, speed);
        }

        /// <summary>
        /// Bites the wall at <paramref name="point"/>, the vehicle coming at it along
        /// <paramref name="direction"/> at <paramref name="speed"/> m/s along the ground.
        /// </summary>
        /// <returns>How many voxels were knocked out.</returns>
        private int Bite(Vector3 point, Vector3 direction, float speed)
        {
            // The faster, the harder: bigger bites, closer together, across the whole bow.
            float hard = Mathf.InverseLerp(_minSpeed, _fullSpeed, speed);
            float radius = Mathf.Lerp(_radius, _fullRadius, hard);
            float force = speed * Mathf.Lerp(0.6f, 1.1f, hard);
            float interval = Mathf.Lerp(_interval, _fullInterval, hard);
            if (FullGas)
            {
                radius *= _fullGasRadius;
                force *= _fullGasForce;
                interval *= _fullGasInterval;
            }

            _next = VoxelTicks.Time + interval;

            Vector3 centre = point + direction * (radius * 0.5f);
            int voxels = VoxelDamage.Blast(centre, radius, force, transform, false);

            if (hard > 0.35f || FullGas)
            {
                Vector3 across = Vector3.Cross(Vector3.up, direction).normalized * _halfWidth;
                float side = radius * 0.8f;
                voxels += VoxelDamage.Blast(centre + across, side, force, transform, false);
                voxels += VoxelDamage.Blast(centre - across, side, force, transform, false);
            }

            // Floored, the bow punches on through: one more bite further into the wall.
            if (FullGas)
                voxels += VoxelDamage.Blast(centre + direction * radius * 0.8f, radius * 0.85f, force, transform, false);

            return voxels;
        }
    }
}
