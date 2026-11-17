using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// Makes a loose voxel piece break when it hits something hard: the harder the hit, the more
    /// pieces (see <see cref="VoxelBody.Fracture"/>). The pieces carry it too, so a tower top that
    /// falls breaks into chunks, the chunks break again where they land, and the last bits drop
    /// as clods that fall apart on the ground.
    /// </summary>
    [RequireComponent(typeof(VoxelBody))]
    public sealed class VoxelImpactFracture : MonoBehaviour, IVoxelFixedUpdate
    {
        /// <summary>Slowest impact (m/s) that breaks a piece.</summary>
        [SerializeField] private float _minSpeed = 3.5f;

        /// <summary>Slowest impact (m/s) that breaks this piece; a body's FractureSpeed sets it.</summary>
        public float MinSpeed
        {
            get => _minSpeed;
            set => _minSpeed = value;
        }

        /// <summary>Extra pieces per m/s above the threshold.</summary>
        [SerializeField] private float _piecesPerSpeed = 0.7f;

        [SerializeField] private int _maxPieces = 8;

        /// <summary>A building's piece breaks into one piece per this many voxels (3.6 t of brick)...</summary>
        private const int STRUCTURE_PIECE_VOXELS = 2000;

        /// <summary>...and at most this many.</summary>
        private const int MAX_STRUCTURE_PIECES = 12;

        [Tooltip("Pieces this many breaks from a standing body no longer break on impact: they lie as rubble.")]
        [SerializeField] private int _maxGeneration = 2;

        [Tooltip("Each break further out needs a harder hit: the threshold grows by this share per generation.")]
        [SerializeField] private float _hardenPerGeneration = 0.6f;

        /// <summary>A new piece cannot break at once: it starts out overlapping its neighbours.</summary>
        [SerializeField] private float _armDelay = 0.25f;

        private VoxelBody _body;
        private float _armedAt;
        private bool _broken;

        private void Awake()
        {
            _body = GetComponent<VoxelBody>();
            Arm();
        }

        /// <summary>Holds off breaking for the arm delay: the piece is new, or was just woken where it lay.</summary>
        internal void Arm() => _armedAt = VoxelTicks.Time + _armDelay;

        /// <summary>A piece that slipped through the ground (it was thrown while the ground's collider was being rebuilt) is let go.</summary>
        private const float FELL_OUT_Y = -60f;

        private void OnEnable() => VoxelTicks.Add(this);

        private void OnDisable() => VoxelTicks.Remove(this);

        private void FixedUpdate() // perf-ok: ticks itself without the game loop (tests, demo scenes); one flag check when driven
        {
            if (!VoxelTicks.Driven)
                VoxelFixedUpdate(Time.fixedDeltaTime);
        }

        public void VoxelFixedUpdate(float deltaTime)
        {
            if (transform.position.y < FELL_OUT_Y)
                Destroy(gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_body != null && !_broken && _body.IsLoose)
                PressOnto(collision);

            if (_broken || VoxelTicks.Time < _armedAt || _body == null)
                return;

            // A settled piece keeps this component (and its colliders): something landing on it
            // must not break it. Collision messages reach a disabled behaviour too, so ask the body.
            if (!_body.IsLoose)
                return;

            if (_body.Generation > _maxGeneration)
                return;

            float threshold = _minSpeed * (1f + _hardenPerGeneration * Mathf.Max(0, _body.Generation - 1));
            float speed = collision.relativeVelocity.magnitude;
            if (speed < threshold)
                return;

            int pieces = Mathf.Clamp(2 + Mathf.RoundToInt((speed - threshold) * _piecesPerSpeed), 2, _maxPieces);
            // Bigger pieces split into more.
            pieces = Mathf.Min(_maxPieces, pieces + _body.SolidCount / 400);
            // A storey or a roof that lands breaks into rooms and slopes, not into a few: one piece
            // per STRUCTURE_PIECE_VOXELS, along its weak joints (VoxelBody.Fracture).
            if (_body.HasStrength)
                pieces = Mathf.Max(pieces, Mathf.Min(MAX_STRUCTURE_PIECES, _body.SolidCount / STRUCTURE_PIECE_VOXELS));

            // One break per hit: the pieces get their own chance on their next hit.
            _armedAt = VoxelTicks.Time + _armDelay;
            VoxelSignals.RaiseFractured(collision.GetContact(0).point, speed);
            if (_body.Fracture(collision.GetContact(0).point, pieces, Mathf.Min(3f, speed * 0.25f)) && _body == null)
                _broken = true;
        }

        /// <summary>
        /// A piece that lands on a standing building presses on it with its hit (the impulse
        /// spread over VoxelBody.IMPACT_TIME), once: a floor that falls on the one below can bring
        /// it down too.
        /// </summary>
        private static void PressOnto(Collision collision)
        {
            if (collision.contactCount == 0 || collision.collider == null)
                return;

            VoxelBody target = collision.collider.GetComponentInParent<VoxelBody>();
            if (target == null || !target.HasStrength || !target.IsStatic)
                return;

            // Only what lands on it presses on it: a piece glancing off a wall does not load it.
            float kg = Mathf.Abs(collision.impulse.y) / (Physics.gravity.magnitude * VoxelBody.IMPACT_TIME);
            target.AddImpactLoad(collision.GetContact(0).point, kg);
        }
    }
}
