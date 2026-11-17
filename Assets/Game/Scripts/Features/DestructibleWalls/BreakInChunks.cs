using System.Collections.Generic;
using RayFire;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Makes a prefragmented wall shed chunks rather than single pieces.
    ///
    /// A bullet or a blast knocks out every piece in its reach, and RayFire would release each of
    /// them on its own: the wall pours out like peas. Here the pieces nearest the impact still come
    /// off one by one as rubble, while the rest are grouped into chunks of neighbouring pieces. The
    /// seams around every chunk are cut and RayfireConnectivity releases each chunk the way it
    /// releases any part that lost its hold: as one connected cluster that flies, lands and gets
    /// shot as a whole (or, on a wall that does not clusterize, as bricks that stay tied together).
    /// </summary>
    [RequireComponent(typeof(RayfireConnectivity))]
    public sealed class BreakInChunks : MonoBehaviour
    {
        [Tooltip("Fewest pieces in a chunk, when the knocked-out area has that many next to each other.")]
        [Min(1)]
        [SerializeField] private int _minPieces = 3;

        [Tooltip("Most pieces in a chunk.")]
        [Min(1)]
        [SerializeField] private int _maxPieces = 8;

        [Tooltip("Share of the knocked-out pieces, nearest the impact first, that come off one by one as rubble.")]
        [Range(0f, 1f)]
        [SerializeField] private float _rubble = 0.25f;

        private readonly HashSet<RayfireRigid> _wanted = new();
        private readonly List<RFShard> _pieces = new();
        private readonly Dictionary<RFShard, int> _chunkOf = new();
        private readonly Queue<RFShard> _frontier = new();

        private RayfireConnectivity _connectivity;

        private void Awake()
        {
            _connectivity = GetComponent<RayfireConnectivity>();
        }

        /// <summary>
        /// Knocks <paramref name="pieces"/> out of the wall in chunks and fills
        /// <paramref name="bodies"/> with what was released: a rigidbody per chunk and per rubble
        /// piece, or per brick where the wall does not clusterize. <paramref name="chunks"/> says
        /// which chunk each body belongs to, so the bricks of one chunk can be sent off together.
        /// Anchored pieces stay in the wall.
        /// </summary>
        /// <param name="impact">Where the wall was hit: the rubble comes from around it.</param>
        public void Release(List<RayfireRigid> pieces, Vector3 impact, List<Rigidbody> bodies, List<int> chunks)
        {
            bodies.Clear();
            chunks.Clear();

            List<RFShard> shards = _connectivity.cluster?.shards;
            if (shards == null)
                return;

            _wanted.Clear();
            for (int i = 0; i < pieces.Count; i++)
                _wanted.Add(pieces[i]);

            _pieces.Clear();
            for (int i = 0; i < shards.Count; i++)
            {
                RFShard shard = shards[i];
                if (!shard.uny && WallSeams.IsStanding(shard) && _wanted.Contains(shard.rigid))
                    _pieces.Add(shard);
            }

            if (_pieces.Count == 0)
                return;

            Group(impact);
            CutAroundChunks();

            // RayFire takes every chunk now cut off from the anchors out of the wall and turns it
            // into a body of its own, together with anything that was hanging on the chunks.
            _connectivity.CheckConnectivity();

            for (int i = 0; i < _pieces.Count; i++)
            {
                Rigidbody body = BodyOf(_pieces[i]);
                if (body == null || body.isKinematic || bodies.Contains(body))
                    continue;

                bodies.Add(body);
                chunks.Add(_chunkOf[_pieces[i]]);
            }
        }

        // The pieces nearest the impact become rubble, one per chunk; the others grow into chunks
        // of neighbours outwards from the impact.
        private void Group(Vector3 impact)
        {
            _pieces.Sort((a, b) =>
                (a.bnd.center - impact).sqrMagnitude.CompareTo((b.bnd.center - impact).sqrMagnitude));

            _chunkOf.Clear();
            int rubble = Mathf.RoundToInt(_pieces.Count * _rubble);
            int chunk = 0;

            for (int i = 0; i < _pieces.Count; i++)
            {
                RFShard seed = _pieces[i];
                if (_chunkOf.ContainsKey(seed))
                    continue;

                _chunkOf[seed] = chunk;
                if (i >= rubble)
                    Grow(seed, chunk, Random.Range(_minPieces, Mathf.Max(_minPieces, _maxPieces) + 1), rubble);

                chunk++;
            }
        }

        private void Grow(RFShard seed, int chunk, int size, int rubble)
        {
            int count = 1;
            _frontier.Clear();
            _frontier.Enqueue(seed);

            while (_frontier.Count > 0 && count < size)
            {
                RFShard piece = _frontier.Dequeue();
                for (int n = 0; n < piece.neibShards.Count && count < size; n++)
                {
                    RFShard neighbour = piece.neibShards[n];
                    if (_chunkOf.ContainsKey(neighbour))
                        continue;

                    // Only pieces that were knocked out, and never the ones meant for rubble.
                    int index = _pieces.IndexOf(neighbour);
                    if (index < rubble)
                        continue;

                    _chunkOf[neighbour] = chunk;
                    _frontier.Enqueue(neighbour);
                    count++;
                }
            }
        }

        private void CutAroundChunks()
        {
            for (int i = 0; i < _pieces.Count; i++)
            {
                RFShard piece = _pieces[i];
                int chunk = _chunkOf[piece];

                for (int n = piece.neibShards.Count - 1; n >= 0; n--)
                {
                    RFShard neighbour = piece.neibShards[n];
                    if (!_chunkOf.TryGetValue(neighbour, out int other) || other != chunk)
                        WallSeams.Cut(piece, neighbour);
                }
            }
        }

        // A clusterized chunk hangs under a root of its own that carries the rigidbody; RayFire
        // destroys the pieces' own rigidbodies at the end of the frame.
        private Rigidbody BodyOf(RFShard piece)
        {
            Transform parent = piece.tm.parent;
            if (parent != null && parent != transform && parent.TryGetComponent(out Rigidbody chunk))
                return chunk;

            return piece.rigid.physics.rb;
        }
    }
}
