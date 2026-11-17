using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A body cut into ragdolled pieces, ready to be thrown apart. Sits on the root of a gibs
    /// prefab such as Regax_Gibs, which RegaxGibsBuilder puts together.
    /// </summary>
    /// <remarks>
    /// Each piece is a skinned mesh with a skeleton of its own, cut from the body's model in
    /// Maya. Its bones keep the body's bone names, which is how <see cref="Burst"/> puts every
    /// piece where the matching part of the body stood. Both halves of the torso have a pelvis
    /// and a spine; they take the same pose from the one body bone of that name.
    ///
    /// The body does not always come apart along every cut. Each seam between two pieces holds
    /// with its own chance, so a burst throws a few clusters: sometimes the chest with both arms,
    /// sometimes one half of it with its arm and leg, now and then just a leg on its own.
    /// </remarks>
    public sealed class Gibs : MonoBehaviour
    {
        [SerializeField] private Piece[] _pieces;

        [Tooltip("Where two pieces meet. A seam that holds keeps them together in one cluster.")]
        [SerializeField] private Seam[] _seams;

        [Header("Burst")]
        [Tooltip("Speed (m/s) a cluster leaves at from the weakest hit.")]
        [SerializeField] private float _minSpeed = 1.5f;

        [Tooltip("Speed (m/s) a cluster leaves at from the strongest hit.")]
        [SerializeField] private float _maxSpeed = 4f;

        [Tooltip("Damage of a hit that throws at full speed; weaker hits scale down to the minimum.")]
        [SerializeField] private float _fullSpeedDamage = 100f;

        [Tooltip("Share of the speed added straight up, so pieces arc instead of skimming the floor.")]
        [SerializeField] private float _lift = 0.35f;

        [Tooltip("Random spin (rad/s) per axis, for a lone piece; bigger clusters turn slower.")]
        [SerializeField] private float _spin = 3f;

        [Tooltip("Each cluster's speed is scaled by a random factor within 1 ± this.")]
        [SerializeField] private float _speedJitter = 0.4f;

        [Tooltip("How far (share of the speed) a cluster's direction may stray from straight away.")]
        [SerializeField] private float _scatter = 0.4f;

        [Header("Blast (tank shell, rocket)")]
        [Tooltip("Speed (m/s) a cluster leaves at from the edge of a blast.")]
        [SerializeField] private float _blastMinSpeed = 2f;

        [Tooltip("Speed (m/s) a cluster leaves at from the centre of a blast.")]
        [SerializeField] private float _blastMaxSpeed = 4f;

        [SerializeField] private float _blastLift = 0.4f;
        [SerializeField] private float _blastSpin = 5f;

        [Header("Cleanup")]
        [SerializeField] private float _lifetime = 10f;
        [SerializeField] private float _sinkTime = 2f;
        [SerializeField] private float _sinkDepth = 1f;

        [System.Serializable]
        public sealed class Piece
        {
            public Transform root;
            public Rigidbody[] bodies;
        }

        /// <summary>
        /// Two pieces that meet, and the bodies on either side of the cut. When the seam holds,
        /// <see cref="bodyB"/> is jointed to <see cref="bodyA"/>.
        /// </summary>
        [System.Serializable]
        public sealed class Seam
        {
            public int pieceA;
            public int pieceB;
            public Rigidbody bodyA;
            public Rigidbody bodyB;

            [Tooltip("Welded rather than hinged: the two torso halves are one block of flesh.")]
            public bool rigid;

            [Range(0f, 1f)] public float holdChance = 0.35f;
        }

        public IReadOnlyList<Piece> Pieces => _pieces;

        private GibSpeeds Speeds => new()
        {
            minSpeed = _minSpeed, maxSpeed = _maxSpeed, fullSpeedDamage = _fullSpeedDamage, lift = _lift, spin = _spin,
            speedJitter = _speedJitter, scatter = _scatter, blastMinSpeed = _blastMinSpeed,
            blastMaxSpeed = _blastMaxSpeed, blastLift = _blastLift, blastSpin = _blastSpin
        };

        /// <summary>
        /// Takes the pose of <paramref name="skeleton"/>, decides which seams hold, and throws
        /// each resulting cluster away from <paramref name="origin"/>.
        /// </summary>
        /// <param name="skeleton">The body's animated hierarchy, holding bones by the same names.</param>
        /// <param name="origin">Where the killing hit came from; a piece right on top of it flies up.</param>
        /// <param name="damage">The killing hit's damage, after its falloff.</param>
        /// <param name="blast">
        /// Killed by an explosion. Its clusters get the blast's own kick, scaled by
        /// <paramref name="blastShare"/> (1 at the centre, less towards the edge) rather than by
        /// damage.
        /// </param>
        public void Burst(Transform skeleton, Vector3 origin, float damage, bool blast = false, float blastShare = 1f)
        {
            // Posed while switched off: the joints are made when the object comes on, and a joint
            // takes the pose it is made in as its rest. Posed while on, every joint would measure
            // its limits from the model's T-pose and yank the limbs back towards it.
            gameObject.SetActive(false);

            Transform self = transform;
            self.SetPositionAndRotation(skeleton.position, skeleton.rotation);
            self.localScale = skeleton.lossyScale;
            CopyPose(skeleton);

            gameObject.SetActive(true);

            // After the pose and the switch-on, so the seam joints also rest in the death pose.
            int[] cluster = HoldSeams();

            GibSpeeds speeds = Speeds;
            GibRules.Throw(speeds, new KillingHit(origin, damage, blast, blastShare),
                out float speed, out float lift, out float spinSpeed);

            var members = new Dictionary<int, List<Rigidbody>>();
            for (int i = 0; i < _pieces.Length; i++)
            {
                if (!members.TryGetValue(cluster[i], out List<Rigidbody> bodies))
                    members[cluster[i]] = bodies = new List<Rigidbody>();
                bodies.AddRange(_pieces[i].bodies);
            }

            foreach (List<Rigidbody> bodies in members.Values)
            {
                Vector3 velocity = GibRules.ClusterVelocity(Centre(bodies), origin, speed, lift, speeds);
                // Three bodies make a lone limb.
                Vector3 spin = GibRules.ClusterSpin(spinSpeed, bodies.Count / 3f);

                foreach (Rigidbody body in bodies)
                {
                    body.isKinematic = false;
                    body.linearVelocity = velocity;
                    body.angularVelocity = spin;
                }
            }

            StartCoroutine(Cleanup());
        }

        /// <summary>
        /// Rolls every seam, joints the ones that hold, and returns each piece's cluster id.
        /// At least two clusters always come out: a body that stayed whole is no burst.
        /// </summary>
        private int[] HoldSeams()
        {
            Seam[] seams = _seams ?? System.Array.Empty<Seam>();
            var links = new SeamLink[seams.Length];
            for (int i = 0; i < seams.Length; i++)
                links[i] = new SeamLink(seams[i].pieceA, seams[i].pieceB, seams[i].holdChance);

            var held = new List<int>();
            int[] cluster = GibRules.RollClusters(_pieces.Length, links, () => Random.value, held);
            foreach (int i in held)
                Join(seams[i]);
            return cluster;
        }

        private static void Join(Seam seam)
        {
            if (seam.bodyA == null || seam.bodyB == null)
                return;

            if (seam.rigid)
            {
                FixedJoint weld = seam.bodyB.gameObject.AddComponent<FixedJoint>();
                weld.connectedBody = seam.bodyA;
                return;
            }

            CharacterJoint joint = seam.bodyB.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = seam.bodyA;
            joint.axis = Vector3.right;
            joint.swingAxis = Vector3.forward;
            joint.lowTwistLimit = new SoftJointLimit { limit = -25f };
            joint.highTwistLimit = new SoftJointLimit { limit = 25f };
            joint.swing1Limit = new SoftJointLimit { limit = 35f };
            joint.swing2Limit = new SoftJointLimit { limit = 25f };
            joint.enableProjection = true;
        }

        private void CopyPose(Transform skeleton)
        {
            var bones = new Dictionary<string, Transform>();
            foreach (Transform bone in skeleton.GetComponentsInChildren<Transform>())
                bones.TryAdd(bone.name, bone);

            // Parents come before their children in this list, so setting the world pose top-down
            // leaves every bone exactly where its namesake is.
            foreach (Piece piece in _pieces)
            foreach (Transform bone in piece.root.GetComponentsInChildren<Transform>())
            {
                if (bones.TryGetValue(bone.name, out Transform source))
                    bone.SetPositionAndRotation(source.position, source.rotation);
            }
        }

        private static Vector3 Centre(List<Rigidbody> bodies)
        {
            Vector3 sum = Vector3.zero;
            foreach (Rigidbody body in bodies)
                sum += body.worldCenterOfMass;
            return sum / Mathf.Max(1, bodies.Count);
        }

        private IEnumerator Cleanup()
        {
            yield return new WaitForSeconds(_lifetime);

            // Frozen where they lie, with nothing to bump into, and lowered through the floor.
            foreach (Piece piece in _pieces)
            foreach (Rigidbody body in piece.bodies)
            {
                body.isKinematic = true;
                foreach (Collider collider in body.GetComponents<Collider>())
                    collider.enabled = false;
            }

            Vector3 start = transform.position;
            for (float t = 0f; t < _sinkTime; t += Time.deltaTime)
            {
                transform.position = start + Vector3.down * (_sinkDepth * t / _sinkTime);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
