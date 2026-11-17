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

            // One break per hit: the pieces get their own chance on their next hit.
            _armedAt = VoxelTicks.Time + _armDelay;
            VoxelSignals.RaiseFractured(collision.GetContact(0).point, speed);
            if (_body.Fracture(collision.GetContact(0).point, pieces, Mathf.Min(3f, speed * 0.25f)) && _body == null)
                _broken = true;
        }
    }
}
