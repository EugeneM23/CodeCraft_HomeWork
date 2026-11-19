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

        /// <summary>A building's piece breaks into one piece per this many voxels (1.3 t of brick)...</summary>
        private const int STRUCTURE_PIECE_VOXELS = 700;

        /// <summary>...and at most this many.</summary>
        private const int MAX_STRUCTURE_PIECES = 16;

        /// <summary>A building's pieces break again this many times over (rubble ends as clods and crumbs).</summary>
        private const int STRUCTURE_MAX_GENERATION = 3;

        /// <summary>A building's piece landing faster than this (m/s, a fall of about a storey at 2 g) breaks up evenly.</summary>
        private const float HARD_LANDING = 6f;

        /// <summary>...into one piece per this many voxels, up to HARD_MAX_PIECES.</summary>
        private const int HARD_PIECE_VOXELS = 250;
        private const int HARD_MAX_PIECES = 32;

        /// <summary>
        /// Most voxels breaking up in one frame: a falling house's pieces that land together break
        /// over a few frames (each later hit is its own chance), not all in one.
        /// </summary>
        private const int FRAME_FRACTURE_VOXELS = 40000;

        private static int s_fractureFrame = -1;
        private static int s_fracturedVoxels;

        /// <summary>A piece lighter than this (kg) presses on nothing it lands on: rubble does not bring a house down.</summary>
        private const float PRESSING_MASS = 800f;

        private static readonly ContactPoint[] Contacts = new ContactPoint[8];

        [Tooltip("Pieces this many breaks from a standing body no longer break on impact: they lie as rubble.")]
        [SerializeField] private int _maxGeneration = 2;

        [Tooltip("Each break further out needs a harder hit: the threshold grows by this share per generation.")]
        [SerializeField] private float _hardenPerGeneration = 0.6f;

        /// <summary>A new piece cannot break at once: it starts out overlapping its neighbours.</summary>
        [SerializeField] private float _armDelay = 0.25f;

        private VoxelBody _body;
        private Rigidbody _rigidbody;
        private float _armedAt;
        private bool _broken;

        private void Awake()
        {
            _body = GetComponent<VoxelBody>();
            Arm();
        }

        /// <summary>A body going back to a pool (VoxelBody.Recycle): it may break again when it is used.</summary>
        internal void ResetForReuse()
        {
            _broken = false;
            _rigidbody = null;
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
            {
                Destroy(gameObject);
                return;
            }

            // A building's piece falls heavy, as the clods do (VoxelDebris): with ordinary gravity a
            // storey drifts down like cardboard.
            if (_body != null && _body.HasStrength && _body.IsLoose && (_rigidbody != null || TryGetComponent(out _rigidbody)) &&
                !_rigidbody.isKinematic)
                _rigidbody.AddForce(Physics.gravity * STRUCTURE_EXTRA_GRAVITY, ForceMode.Acceleration);
        }

        /// <summary>Gravity a building's piece gets on top of the world's: it falls at twice g.</summary>
        private const float STRUCTURE_EXTRA_GRAVITY = 1f;

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

            if (_body.Generation > (_body.HasStrength ? STRUCTURE_MAX_GENERATION : _maxGeneration))
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
            // Landing hard it breaks all through, into twice as many.
            bool hard = _body.HasStrength && speed > HARD_LANDING;
            if (_body.HasStrength)
                pieces = Mathf.Max(pieces, Mathf.Min(hard ? HARD_MAX_PIECES : MAX_STRUCTURE_PIECES,
                    _body.SolidCount / (hard ? HARD_PIECE_VOXELS : STRUCTURE_PIECE_VOXELS)));

            // Within the frame's budget, the first break always: the rest break on a later hit.
            if (Time.frameCount != s_fractureFrame)
            {
                s_fractureFrame = Time.frameCount;
                s_fracturedVoxels = 0;
            }

            if (s_fracturedVoxels > 0 && s_fracturedVoxels + _body.SolidCount > FRAME_FRACTURE_VOXELS)
                return;
            s_fracturedVoxels += _body.SolidCount;

            // One break per hit: the pieces get their own chance on their next hit. A building's
            // pieces part slowly (they do not bounce apart), the rest at a quarter of the hit.
            _armedAt = VoxelTicks.Time + _armDelay;
            float spread = _body.HasStrength ? Mathf.Min(1f, speed * 0.1f) : Mathf.Min(3f, speed * 0.25f);
            VoxelSignals.RaiseFractured(collision.GetContact(0).point, speed);
            if (_body.Fracture(collision.GetContact(0).point, pieces, spread, hard) && _body == null)
                _broken = true;
        }

        /// <summary>
        /// A piece that lands on a standing building presses on it with its hit (the impulse
        /// spread over VoxelBody.IMPACT_TIME), once: a floor that falls on the one below can bring
        /// it down too.
        /// </summary>
        private void PressOnto(Collision collision)
        {
            if (_rigidbody == null && !TryGetComponent(out _rigidbody))
                return;
            if (_rigidbody.mass < PRESSING_MASS)
                return;

            // A standing building has no Rigidbody: anything that has one (another piece, the tank)
            // is not looked up at all.
            if (collision.contactCount == 0 || collision.collider == null || collision.rigidbody != null)
                return;

            VoxelBody target = collision.collider.GetComponentInParent<VoxelBody>();
            if (target == null || !target.HasStrength || !target.IsStatic)
                return;

            // Only what lands on it presses on it: a piece glancing off a wall does not load it.
            float kg = Mathf.Abs(collision.impulse.y) / (Physics.gravity.magnitude * VoxelBody.IMPACT_TIME);
            if (kg < VoxelBody.MIN_IMPACT_LOAD)
                return;
            // Spread over where it lands: a falling storey bears on all the walls it comes down on.
            int count = collision.GetContacts(Contacts);
            for (int i = 0; i < count; i++)
                target.AddImpactLoad(Contacts[i].point, kg / count);

            Crush(collision, target, count);
        }

        /// <summary>Joules of a landing that start crushing what it lands on.</summary>
        private const float CRUSH_ENERGY = 40000f;

        /// <summary>Crushed radius (m) per cube root of joules at one contact: 500 kJ crush 2.4 m (capped at CRUSH_MAX_RADIUS).</summary>
        private const float CRUSH_PER_ENERGY = 0.03f;

        private const float CRUSH_MAX_RADIUS = 2f;

        /// <summary>Contacts of one landing that crush, at least this far (m) apart.</summary>
        private const int CRUSH_SPOTS = 6;
        private const float CRUSH_SPACING = 1f;

        /// <summary>Crumbs one crushed spot throws (the rest is dust).</summary>
        private const int CRUSH_DEBRIS = 250;

        /// <summary>Most crushed spots in one frame: a whole falling house lands over a few.</summary>
        private const int FRAME_CRUSH_SPOTS = 16;

        private static int s_crushFrame = -1;
        private static int s_crushSpots;
        private static readonly Vector3[] Spots = new Vector3[CRUSH_SPOTS];

        /// <summary>
        /// A heavy piece coming down hard crushes what it lands on: at each spot it touches, a
        /// ball of the standing building goes to crumbs (and of the piece's own underside), as big
        /// as the landing's energy (½ m v², its downward part) allows. A storey dropped on the one
        /// below presses into it instead of sitting on it like a box on a table.
        /// </summary>
        private void Crush(Collision collision, VoxelBody target, int count)
        {
            float down = Mathf.Abs(collision.relativeVelocity.y);
            float energy = 0.5f * _rigidbody.mass * down * down;
            if (energy < CRUSH_ENERGY || count == 0)
                return;

            if (Time.frameCount != s_crushFrame)
            {
                s_crushFrame = Time.frameCount;
                s_crushSpots = 0;
            }

            int spots = 0;
            for (int i = 0; i < count && spots < CRUSH_SPOTS; i++)
            {
                Vector3 p = Contacts[i].point;
                bool near = false;
                for (int k = 0; k < spots && !near; k++)
                    near = (Spots[k] - p).sqrMagnitude < CRUSH_SPACING * CRUSH_SPACING;
                if (!near)
                    Spots[spots++] = p;
            }

            float radius = Mathf.Min(CRUSH_MAX_RADIUS, CRUSH_PER_ENERGY * Mathf.Pow(energy / spots, 1f / 3f));
            for (int k = 0; k < spots && s_crushSpots < FRAME_CRUSH_SPOTS; k++, s_crushSpots++)
            {
                target.CarveSphere(Spots[k], radius, 2f, CRUSH_DEBRIS);
                if (_body != null && _body.IsCreated)
                    _body.CarveSphere(Spots[k], radius * 0.7f, 2f, CRUSH_DEBRIS / 2);
            }
        }
    }
}
