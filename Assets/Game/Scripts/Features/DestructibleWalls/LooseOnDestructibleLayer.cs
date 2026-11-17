using System.Collections.Generic;
using RayFire;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Loose bodies whose colliders RayFire leaves on the Destructible layer, among the standing wall
    /// pieces.
    ///
    /// A piece knocked out of a wall moves to Debris or Rubble (every wall sets RayFire's activation
    /// layer), and so do the chunks connectivity cuts off and the pieces a loose piece splits into;
    /// the standing pieces stay on Destructible, hundreds of them around every wall. So what looks for
    /// loose pieces (<see cref="PassThroughLoosePiecesBehaviour"/>, the explosion's shove in
    /// <see cref="ExplosionUseCase"/>) looks on Debris and Rubble and leaves Destructible out. A few
    /// loose bodies stay on Destructible all the same: the groups of pieces a connected cluster (the
    /// slab wall) sheds keep their pieces' layer, and so do the pieces of anything that inherits its
    /// layer and is demolished before it is activated. They are collected here from RayFire's own
    /// events, so those queries still find them without sweeping Destructible.
    /// </summary>
    public static class LooseOnDestructibleLayer
    {
        private sealed class Entry
        {
            public Rigidbody Body;
            public Collider[] Colliders;
        }

        private static readonly List<Entry> Entries = new();
        private static readonly List<Collider> Found = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Subscribe()
        {
            Entries.Clear();

            RFActivationEvent.GlobalEvent -= Consider;
            RFActivationEvent.GlobalEvent += Consider;
            RFDemolitionEvent.GlobalEvent -= OnDemolished;
            RFDemolitionEvent.GlobalEvent += OnDemolished;
            RFConnectivityEvent.GlobalEvent -= OnConnectivity;
            RFConnectivityEvent.GlobalEvent += OnConnectivity;
        }

        /// <summary>
        /// Adds to <paramref name="result"/> the colliders on Destructible of these bodies, while they
        /// are simulated, whose bounds reach into <paramref name="area"/>.
        /// </summary>
        public static void CollectInside(Bounds area, List<Collider> result)
        {
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                Entry entry = Entries[i];
                if (IsGone(entry))
                {
                    RemoveAt(i);
                    continue;
                }

                if (entry.Body.isKinematic)
                    continue;

                Collider[] colliders = entry.Colliders;
                for (int c = 0; c < colliders.Length; c++)
                {
                    Collider collider = colliders[c];
                    if (IsOnDestructible(collider) && collider.enabled && !collider.isTrigger
                        && collider.gameObject.activeInHierarchy && collider.bounds.Intersects(area))
                        result.Add(collider);
                }
            }
        }

        /// <summary>
        /// Whether one of these bodies, simulated, has a collider on Destructible within
        /// <paramref name="radius"/> of <paramref name="point"/>.
        /// </summary>
        public static bool AnyWithin(Vector3 point, float radius)
        {
            float sqrRadius = radius * radius;

            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                Entry entry = Entries[i];
                if (IsGone(entry))
                {
                    RemoveAt(i);
                    continue;
                }

                if (entry.Body.isKinematic)
                    continue;

                Collider[] colliders = entry.Colliders;
                for (int c = 0; c < colliders.Length; c++)
                {
                    Collider collider = colliders[c];
                    if (IsOnDestructible(collider) && collider.enabled && collider.gameObject.activeInHierarchy
                        && collider.bounds.SqrDistance(point) <= sqrRadius)
                        return true;
                }
            }

            return false;
        }

        // Destroyed, or none of its colliders is on Destructible any more (RayFire moves the pieces of a
        // broken cluster to its fragment layer right after the demolition event): the ordinary queries
        // see it.
        private static bool IsGone(Entry entry)
        {
            if (entry.Body == null)
                return true;

            Collider[] colliders = entry.Colliders;
            for (int c = 0; c < colliders.Length; c++)
                if (IsOnDestructible(colliders[c]))
                    return false;

            return true;
        }

        private static bool IsOnDestructible(Collider collider) =>
            collider != null && collider.gameObject.layer == PhysicsLayers.Destructible;

        private static void RemoveAt(int index)
        {
            int last = Entries.Count - 1;
            Entries[index] = Entries[last];
            Entries.RemoveAt(last);
        }

        // The pieces and the groups of pieces a demolished object or cluster broke into.
        private static void OnDemolished(RayfireRigid rigid)
        {
            List<RayfireRigid> fragments = rigid != null ? rigid.fragments : null;
            if (fragments == null)
                return;

            for (int i = 0; i < fragments.Count; i++)
                Consider(fragments[i]);
        }

        // The chunks and single pieces a wall's connectivity check set loose.
        private static void OnConnectivity(RayfireConnectivity connectivity, List<RFShard> shards, List<RFCluster> clusters)
        {
            if (clusters != null)
                for (int i = 0; i < clusters.Count; i++)
                    if (clusters[i] != null)
                        Consider(clusters[i].rigid);

            if (shards != null)
                for (int i = 0; i < shards.Count; i++)
                    if (shards[i] != null)
                        Consider(shards[i].rigid);
        }

        // A body RayFire has just set loose, kept when some of its colliders sit on Destructible.
        private static void Consider(RayfireRigid rigid)
        {
            if (rigid == null)
                return;

            Rigidbody body = rigid.physics.rb;
            if (body == null || body.isKinematic)
                return;

            Found.Clear();
            if (rigid.IsCluster)
            {
                List<Collider> colliders = rigid.physics.cc;
                if (colliders != null)
                    for (int i = 0; i < colliders.Count; i++)
                        if (IsOnDestructible(colliders[i]))
                            Found.Add(colliders[i]);
            }
            else
            {
                Collider collider = rigid.physics.mc != null ? rigid.physics.mc : rigid.GetComponent<Collider>();
                if (IsOnDestructible(collider))
                    Found.Add(collider);
            }

            if (Found.Count == 0)
                return;

            Collider[] found = Found.ToArray();
            Found.Clear();

            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Body == body)
                {
                    Entries[i].Colliders = found;
                    return;
                }
            }

            Entries.Add(new Entry { Body = body, Colliders = found });
        }
    }
}
