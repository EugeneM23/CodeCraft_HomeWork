using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// Lets a heavy vehicle push through voxel walls: while it drives into one faster than
    /// <see cref="_minSpeed"/>, the voxels at the contact are crushed; flying at one, it bites the
    /// wall ahead before it gets there (<see cref="Airborne"/>). Put it next to the Rigidbody.
    /// </summary>
    /// <remarks>
    /// The speed the vehicle wants, not the speed it has: a drive that sets the velocity every
    /// physics step is stopped dead by the wall's collider, so the post-solve velocity is zero
    /// exactly when it matters. FixedUpdate runs late (execution order), after the drive has set
    /// this step's velocity and before the step is simulated.
    /// </remarks>
    [DefaultExecutionOrder(5000)]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class VoxelRammer : MonoBehaviour
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

        private void Awake() => _rigidbody = GetComponent<Rigidbody>();

        private void FixedUpdate()
        {
            _intended = _rigidbody.linearVelocity;
            if (Airborne)
                BiteAhead();
        }

        private void BiteAhead()
        {
            if (Time.time < _next)
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
            if (Time.time < _next)
                return;

            Vector3 flat = new Vector3(_intended.x, 0f, _intended.z);
            float speed = flat.magnitude;
            if (speed < _minSpeed)
                return;

            VoxelBody body = collision.collider.GetComponentInParent<VoxelBody>();
            // Not its own skin, and not the ground it drives on (a hull scraping a crater's edge).
            if (body == null || body.transform.IsChildOf(transform) || body.IsTerrain)
                return;

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

            _next = Time.time + interval;

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
