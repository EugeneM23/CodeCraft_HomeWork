using System.Collections.Generic;
using System.Diagnostics;
using RayFire;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Spreads explosion demolition over several frames.
    ///
    /// A runtime-fragmented brick costs ~3 ms to split with RayFire's V2 engine (~20 ms with V1, so
    /// keep destructible walls on V2), and a rocket breaks dozens of them at once. Splitting them all inside the contact
    /// callback froze the game for up to 1.5 s. Here each frame splits bricks, nearest to the blast
    /// first, until the frame budget is spent, so the hole opens at the impact point at once and
    /// widens over the next few frames.
    /// The same budget splits bricks a blast knocked out whole into 2-3 pieces in flight.
    /// </summary>
    public sealed class DemolitionQueue : MonoBehaviour
    {
        // Milliseconds of splitting per frame. At least one piece is split every frame regardless.
        private const double FRAME_BUDGET_MS = 4.0;

        // A flying brick that splits breaks into this many pieces, picked at random.
        private const int SPLIT_MIN_PIECES = 2;
        private const int SPLIT_MAX_PIECES = 3;
        // Speed (m/s) the pieces drift apart with, on top of the brick's own flight.
        private const float SPLIT_SEPARATION_SPEED = 1.5f;
        // How long the pieces may pass through the wall pieces the brick is inside of at that moment.
        private const float SPLIT_IGNORE_SECONDS = 0.5f;
        private const float TOUCH_GAP = 0.02f;

        private static readonly Collider[] Touching = new Collider[64];

        private struct Job
        {
            public RayfireRigid rigid;
            public bool split;
            public float damage;
            public Vector3 blastPoint;
            public float radius;
            public float push;
            public float distance;
        }

        private static DemolitionQueue _instance;
        private readonly List<Job> _jobs = new();
        private readonly Stopwatch _stopwatch = new();
        private bool _sorted = true;

        public static void Enqueue(RayfireRigid rigid, float damage, Vector3 blastPoint, float radius, float push)
        {
            Add(new Job
            {
                rigid = rigid,
                damage = damage,
                blastPoint = blastPoint,
                radius = radius,
                push = push,
                distance = Vector3.Distance(rigid.transform.position, blastPoint)
            });
        }

        /// <summary>
        /// Breaks a brick that is already flying into 2-3 irregular pieces, so not every brick a
        /// blast knocks out stays a clean block. Works on walls whose demolition type is None too.
        /// </summary>
        public static void EnqueueSplit(RayfireRigid brick, Vector3 blastPoint)
        {
            Add(new Job
            {
                rigid = brick,
                split = true,
                blastPoint = blastPoint,
                distance = Vector3.Distance(brick.transform.position, blastPoint)
            });
        }

        private static void Add(Job job)
        {
            if (_instance == null)
            {
                var go = new GameObject("[DemolitionQueue]") { hideFlags = HideFlags.DontSave };
                _instance = go.AddComponent<DemolitionQueue>();
            }

            _instance._jobs.Add(job);
            _instance._sorted = false;
        }

        private void Update()
        {
            if (_jobs.Count == 0)
                return;

            // Farthest last, so taking from the end of the list takes the nearest one first.
            if (!_sorted)
            {
                _jobs.Sort((a, b) => b.distance.CompareTo(a.distance));
                _sorted = true;
            }

            _stopwatch.Restart();
            do
            {
                int last = _jobs.Count - 1;
                Job job = _jobs[last];
                _jobs.RemoveAt(last);

                if (job.split)
                    Split(job.rigid);
                else
                    Demolish(job);
            }
            while (_jobs.Count > 0 && _stopwatch.Elapsed.TotalMilliseconds < FRAME_BUDGET_MS);
        }

        private static void Demolish(Job job)
        {
            RayfireRigid rigid = job.rigid;
            if (rigid == null || !rigid.gameObject.activeInHierarchy || rigid.lim.demolished)
                return;

            rigid.ApplyDamage(job.damage, job.blastPoint, job.radius);

            if (!rigid.lim.demolished || !rigid.HasFragments)
                return;

            // The bomb no longer touches these pieces (they did not exist when it went off), so throw
            // the new shards away from the blast ourselves. Same falloff as the damage.
            for (int i = 0; i < rigid.fragments.Count; i++)
            {
                Rigidbody shard = rigid.fragments[i] != null ? rigid.fragments[i].physics.rb : null;
                if (shard == null || shard.isKinematic)
                    continue;

                shard.AddExplosionForce(job.push, job.blastPoint, job.radius, 0.3f, ForceMode.VelocityChange);
            }
        }

        private static void Split(RayfireRigid brick)
        {
            if (brick == null || !brick.gameObject.activeInHierarchy || brick.lim.demolished)
                return;

            Rigidbody body = brick.physics.rb;
            if (body == null || body.isKinematic)
                return;

            Vector3 velocity = body.linearVelocity;
            Vector3 spin = body.angularVelocity;
            Renderer brickRenderer = brick.GetComponent<Renderer>();
            Vector3 centre = brickRenderer != null ? brickRenderer.bounds.center : body.worldCenterOfMass;

            // A brick knocked out sideways may still be passing through the wall, its collisions
            // with those pieces ignored for a moment. Its pieces have to pass through them as well.
            Collider own = brick.physics.mc != null ? brick.physics.mc : body.GetComponent<Collider>();
            int touching = 0;
            if (own != null)
            {
                Bounds bounds = own.bounds;
                touching = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents + Vector3.one * TOUCH_GAP,
                    Touching, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            }

            // V2 splits a brick in ~3 ms, V1 in ~22 ms, and the brick walls are set to V1.
            brick.mshDemol.engTp = RayfireShatter.RFEngineType.V2;
            brick.mshDemol.am = Random.Range(SPLIT_MIN_PIECES, SPLIT_MAX_PIECES + 1);
            brick.mshDemol.var = 0;
            brick.DemolishForced();

            if (!brick.lim.demolished || !brick.HasFragments)
                return;

            for (int f = 0; f < brick.fragments.Count; f++)
            {
                RayfireRigid fragment = brick.fragments[f];
                Rigidbody piece = fragment != null ? fragment.physics.rb : null;
                if (piece == null || piece.isKinematic)
                    continue;

                // The pieces are loose, and loose pieces are looked for on Debris and Rubble
                // (PassThroughLoosePiecesBehaviour, the explosion's shove). RayFire gives them the
                // brick's layer or the wall's fragment layer, which every wall here sets to Debris or
                // Rubble; a wall set up otherwise would leave them among the standing pieces on
                // Destructible.
                if (fragment.gameObject.layer == PhysicsLayers.Destructible)
                    fragment.gameObject.layer = PhysicsLayers.Debris;

                Collider collider = fragment.physics.mc != null ? fragment.physics.mc : piece.GetComponent<Collider>();
                if (collider != null)
                    for (int i = 0; i < touching; i++)
                        if (Touching[i] != own && Touching[i].attachedRigidbody != null)
                            TemporaryCollisionIgnore.Ignore(collider, Touching[i], SPLIT_IGNORE_SECONDS);

                // The pieces keep the brick's flight and drift apart from its centre, so it reads as
                // one brick snapping in two or three. A fresh fragment's colliders reach the physics
                // scene only on the next step, so its renderer says where it sits inside the brick.
                Renderer pieceRenderer = fragment.GetComponent<Renderer>();
                Vector3 apart = pieceRenderer != null ? pieceRenderer.bounds.center - centre : Vector3.zero;
                apart = apart.sqrMagnitude > 0.000001f ? apart.normalized : Random.onUnitSphere;

                piece.linearVelocity = velocity + apart * SPLIT_SEPARATION_SPEED;
                piece.angularVelocity = spin + Random.insideUnitSphere * 3f;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
