using System.Collections.Generic;
using RayFire;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Brings down the parts of a prefragmented wall that are still attached but could not carry
    /// their own weight.
    ///
    /// RayfireConnectivity drops a piece only once nothing links it to an anchor, so a thin strip
    /// left at the edge of a hole keeps the whole wall above the hole in the air. Each time the wall
    /// loses pieces, this weighs what is left (<see cref="WallLoadSolver"/>), tears the seams along
    /// which it gives way and lets RayfireConnectivity drop the parts that came loose, the way it
    /// drops any other detached part. A collapse changes the wall again, so a badly damaged wall
    /// can come down in stages.
    ///
    /// Pieces are weighed by the volume of their mesh: RayFire's own masses grow with a piece's
    /// size, not its volume, and differ wildly from wall to wall. So the strengths below are in
    /// metres of the wall's own material, the same for every wall whatever RayFire made of its mass.
    /// </summary>
    [RequireComponent(typeof(RayfireConnectivity))]
    public sealed class CollapseUnderOwnWeight : MonoBehaviour
    {
        [Tooltip("How far (m) a ledge may stick out sideways before it breaks off. A band over a hole held at both " +
                 "ends spans about twice that, more where its pieces can lean on each other in an arch.")]
        [SerializeField] private float _overhang = 0.8f;

        [Tooltip("How long (m) a piece may hang from the seam above it.")]
        [SerializeField] private float _hang = 0.3f;

        [Tooltip("How tall (m) a column of wall a seam carries on top of it before it is crushed.")]
        [SerializeField] private float _crush = 30f;

        [Tooltip("A piece resting on a slope gentler than atan(friction) is held by friction alone.")]
        [SerializeField] private float _friction = 0.6f;

        [Tooltip("Seconds a damaged wall stands before what it cannot carry gives way.")]
        [SerializeField] private float _settleDelay = 0.2f;

        private readonly WallLoadSolver _solver = new();
        private readonly List<RFShard> _pieces = new();
        private readonly Dictionary<RFShard, int> _pieceIndex = new();
        private readonly Dictionary<RFShard, float> _volumes = new();
        private readonly List<int> _brokenSeams = new();
        private readonly List<Vector3> _vertices = new();
        private readonly List<int> _triangles = new();

        private RayfireConnectivity _connectivity;
        private int _standing;
        private float _settleAt = float.PositiveInfinity;

        private void Awake()
        {
            _connectivity = GetComponent<RayfireConnectivity>();
        }

        private void LateUpdate()
        {
            List<RFShard> shards = _connectivity.cluster?.shards;
            if (shards == null)
                return;

            // The wall lost pieces (shot out, split, or dropped by the last collapse). Every further
            // loss restarts the wait, so the wall is weighed once things calm down.
            if (shards.Count < _standing)
                _settleAt = Time.time + _settleDelay;

            _standing = shards.Count;

            // RayFire still lists pieces knocked out this frame until its own connectivity check.
            if (Time.time < _settleAt || _connectivity.connectivityCheckNeed)
                return;

            _settleAt = float.PositiveInfinity;

            if (TearOverloadedSeams(shards))
                _connectivity.CheckConnectivity();
        }

        /// <summary>Tears the seams the wall gives way along. True when it had to.</summary>
        public bool TearOverloadedSeams(List<RFShard> shards)
        {
            _solver.Clear();
            _pieces.Clear();
            _pieceIndex.Clear();

            for (int i = 0; i < shards.Count; i++)
            {
                RFShard shard = shards[i];
                if (!WallSeams.IsStanding(shard))
                    continue;

                _pieceIndex[shard] = _pieces.Count;
                _pieces.Add(shard);
                _solver.AddPiece(shard.bnd.center, VolumeOf(shard), shard.uny);
            }

            for (int a = 0; a < _pieces.Count; a++)
            {
                RFShard shard = _pieces[a];
                for (int n = 0; n < shard.neibShards.Count; n++)
                {
                    // One seam per pair.
                    if (_pieceIndex.TryGetValue(shard.neibShards[n], out int b) && b > a)
                        _solver.AddSeam(a, b, WallSeams.ContactArea(_connectivity, shard, n));
                }
            }

            // Weight in cubic metres of the material, strengths in metres of it: a unit gravity.
            var strength = new SeamStrength(_crush, _overhang, _hang, _friction);
            _solver.FindFailure(strength, Physics.gravity.normalized, _brokenSeams);

            for (int i = 0; i < _brokenSeams.Count; i++)
            {
                _solver.GetSeam(_brokenSeams[i], out int a, out int b);
                WallSeams.Cut(_pieces[a], _pieces[b]);
            }

            return _brokenSeams.Count > 0;
        }

        private float VolumeOf(RFShard shard)
        {
            if (_volumes.TryGetValue(shard, out float volume))
                return volume;

            // Shards RayFire restores from a cached setup come without their mesh filter.
            MeshFilter filter = shard.mf != null ? shard.mf : shard.tm.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            volume = mesh != null && mesh.isReadable ? MeshVolume(mesh) : 0f;

            Vector3 scale = shard.tm.lossyScale;
            volume *= Mathf.Abs(scale.x * scale.y * scale.z);

            // A mesh RayFire cannot read either: half its bounding box is near enough for a shard.
            if (volume <= 0f)
                volume = shard.bnd.size.x * shard.bnd.size.y * shard.bnd.size.z * 0.5f;

            _volumes[shard] = volume;
            return volume;
        }

        // Sum of the signed volumes of the tetrahedra each triangle makes with the origin.
        private float MeshVolume(Mesh mesh)
        {
            mesh.GetVertices(_vertices);
            float volume = 0f;

            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                mesh.GetTriangles(_triangles, sub);
                for (int t = 0; t + 2 < _triangles.Count; t += 3)
                {
                    Vector3 a = _vertices[_triangles[t]];
                    Vector3 b = _vertices[_triangles[t + 1]];
                    Vector3 c = _vertices[_triangles[t + 2]];
                    volume += Vector3.Dot(a, Vector3.Cross(b, c));
                }
            }

            return Mathf.Abs(volume) / 6f;
        }
    }
}
