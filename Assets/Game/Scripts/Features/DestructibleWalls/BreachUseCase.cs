using System.Collections.Generic;
using RayFire;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Punches a hole: every brick / shard of a prefractured wall within the breach radius is
    /// knocked out whole and thrown in a random direction (a wall with <see cref="BreakInChunks"/>
    /// sheds them in chunks), everything farther away is not touched at all (no damage, no
    /// push), so the rest of the wall stands. A standalone destructible
    /// (a glass pane, a wooden column) caught in the breach is broken instead, through the
    /// frame-budgeted queue because splitting a piece is costly.
    ///
    /// Used by anything that breaks destructibles by force: a blast
    /// (<see cref="ExplosionUseCase"/>) and a tank driving into a wall (<see cref="TankRamBehaviour"/>).
    /// </summary>
    public static class BreachUseCase
    {
        private static readonly Collider[] DestructibleHits = new Collider[512];
        private static readonly HashSet<RayfireRigid> BreachedRigids = new();
        private static readonly Collider[] NeighbourHits = new Collider[64];
        private static readonly Dictionary<BreakInChunks, List<RayfireRigid>> ChunkedWalls = new();
        private static readonly List<Rigidbody> ReleasedBodies = new();
        private static readonly List<int> ReleasedChunks = new();
        private static readonly Dictionary<int, Vector3> ChunkVelocities = new();
        private static readonly List<Collider> BodyColliders = new();
        private static readonly List<Renderer> BodyRenderers = new();

        // How far around a knocked-out piece its neighbours are looked for, and how long it may
        // pass through them: at breach speed a brick is a metre or more clear of the wall by then.
        private const float NEIGHBOUR_GAP = 0.02f;
        private const float NEIGHBOUR_IGNORE_SECONDS = 0.5f;

        /// <summary>
        /// Pieces within <paramref name="radius"/> of the point are knocked out at up to
        /// <paramref name="speed"/>; standalone destructibles there are broken.
        /// </summary>
        public static void Breach(Vector3 point, float radius, float speed, float splitChance)
        {
            if (radius <= 0f)
                return;

            int count = Physics.OverlapSphereNonAlloc(point, radius, DestructibleHits, ~0,
                QueryTriggerInteraction.Ignore);
            BreachedRigids.Clear();

            for (int i = 0; i < count; i++)
            {
                Collider hit = DestructibleHits[i];
                Rigidbody body = hit.attachedRigidbody;
                RayfireRigid rigid = body != null ? body.GetComponent<RayfireRigid>() : hit.GetComponent<RayfireRigid>();

                if (rigid == null || rigid.lim.demolished || !rigid.gameObject.activeInHierarchy)
                    continue;

                float distance = Vector3.Distance(point, hit.bounds.ClosestPoint(point));

                if (distance > radius || !BreachedRigids.Add(rigid))
                    continue;

                float falloff = 1f - 0.5f * Mathf.Clamp01(distance / radius);

                // A piece of a MeshRoot is a brick or shard of a prefractured wall. A wall
                // that breaks in chunks gets all its pieces at once, after the scan.
                if (rigid.meshRoot != null && rigid.meshRoot.TryGetComponent(out BreakInChunks chunks))
                    PieceListOf(chunks).Add(rigid);
                else if (rigid.meshRoot != null)
                    KnockOut(rigid, point, speed * falloff, splitChance);
                else if (rigid.damage.en)
                    DemolitionQueue.Enqueue(rigid, rigid.damage.max, point, radius, speed);
            }

            foreach (KeyValuePair<BreakInChunks, List<RayfireRigid>> wall in ChunkedWalls)
            {
                if (wall.Value.Count > 0)
                    KnockOutChunks(wall.Key, wall.Value, point, radius, speed, splitChance);

                wall.Value.Clear();
            }

            BreachedRigids.Clear();
        }

        private static List<RayfireRigid> PieceListOf(BreakInChunks wall)
        {
            if (!ChunkedWalls.TryGetValue(wall, out List<RayfireRigid> pieces))
                ChunkedWalls[wall] = pieces = new List<RayfireRigid>();
            return pieces;
        }

        private static void KnockOut(RayfireRigid piece, Vector3 point, float speed, float splitChance)
        {
            // Activation turns a kinematic wall piece dynamic and asks the wall's connectivity to
            // release whatever lost its support through the hole. Unyielding pieces refuse.
            if (piece.simTp == SimType.Kinematic || piece.simTp == SimType.Inactive)
                piece.Activate();

            Rigidbody body = piece.physics.rb;
            if (body == null || body.isKinematic)
                return;

            Launch(body, ThrowDirection(body.worldCenterOfMass, point) * speed, true);

            // Some bricks snap into 2-3 pieces in the air instead of flying whole. A split costs a
            // few milliseconds, so the queue does it over the next frames, nearest to the blast first.
            if (Random.value < splitChance)
                DemolitionQueue.EnqueueSplit(piece, point);
        }

        /// <summary>
        /// The breach of a wall that breaks in chunks: the pieces right at the blast fly off one by
        /// one, the rest in chunks of neighbouring pieces. Every chunk is thrown as one: a single
        /// direction and speed for all its bricks, so a chunk of a wall that does not clusterize
        /// leaves still tied together.
        /// </summary>
        private static void KnockOutChunks(BreakInChunks wall, List<RayfireRigid> pieces, Vector3 point,
            float radius, float speed, float splitChance)
        {
            wall.Release(pieces, point, ReleasedBodies, ReleasedChunks);
            ChunkVelocities.Clear();

            for (int i = 0; i < ReleasedBodies.Count; i++)
            {
                Rigidbody body = ReleasedBodies[i];
                Vector3 centre = CentreOf(body);

                if (!ChunkVelocities.TryGetValue(ReleasedChunks[i], out Vector3 velocity))
                {
                    float falloff = 1f - 0.5f * Mathf.Clamp01(Vector3.Distance(point, centre) / radius);
                    ChunkVelocities[ReleasedChunks[i]] = velocity = ThrowDirection(centre, point) * (speed * falloff);
                }

                // A body that is a whole chunk spins freely; bricks of one chunk spinning each their
                // own way would tear it apart.
                bool whole = ChunkSize(i) == 1;
                Launch(body, velocity, whole);

                RayfireRigid piece = body.GetComponent<RayfireRigid>();
                if (whole && piece != null && piece.objTp == ObjectType.Mesh && Random.value < splitChance)
                    DemolitionQueue.EnqueueSplit(piece, point);
            }
        }

        private static int ChunkSize(int body)
        {
            int size = 0;
            for (int i = 0; i < ReleasedChunks.Count; i++)
                if (ReleasedChunks[i] == ReleasedChunks[body])
                    size++;
            return size;
        }

        // Any direction at all, uniformly over the sphere: some pieces fly on through the hole,
        // some back at the shooter, some into the floor or along the wall. A piece lying below
        // the blast is thrown up only: driven down it would only wedge against the ground.
        private static Vector3 ThrowDirection(Vector3 centre, Vector3 point)
        {
            Vector3 direction = Random.onUnitSphere;
            if (centre.y < point.y)
                direction.y = Mathf.Abs(direction.y);
            return direction;
        }

        private static void Launch(Rigidbody body, Vector3 velocity, bool spin)
        {
            // The piece is wedged between its neighbours: let it slide past them for a moment, or
            // their contacts stop it in the wall. Only other rigidbodies (wall pieces), never the
            // floor or static level geometry, so it cannot drop through the ground. A chunk is
            // several pieces, each with a collider of its own.
            body.GetComponentsInChildren(BodyColliders);
            for (int c = 0; c < BodyColliders.Count; c++)
            {
                Collider own = BodyColliders[c];
                Bounds bounds = own.bounds;
                int touching = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents + Vector3.one * NEIGHBOUR_GAP,
                    NeighbourHits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

                for (int i = 0; i < touching; i++)
                {
                    Collider other = NeighbourHits[i];
                    if (other.attachedRigidbody != null && !other.transform.IsChildOf(body.transform))
                        TemporaryCollisionIgnore.Ignore(own, other, NEIGHBOUR_IGNORE_SECONDS);
                }
            }

            BodyColliders.Clear();

            body.linearVelocity = velocity;
            body.angularVelocity = spin ? Random.insideUnitSphere * 6f : Vector3.zero;
        }

        // A chunk RayFire has just put together has no colliders of its own yet: its centre is
        // where its pieces are drawn.
        private static Vector3 CentreOf(Rigidbody body)
        {
            body.GetComponentsInChildren(BodyRenderers);
            if (BodyRenderers.Count == 0)
                return body.position;

            Bounds bounds = BodyRenderers[0].bounds;
            for (int i = 1; i < BodyRenderers.Count; i++)
                bounds.Encapsulate(BodyRenderers[i].bounds);

            BodyRenderers.Clear();
            return bounds.center;
        }
    }
}
