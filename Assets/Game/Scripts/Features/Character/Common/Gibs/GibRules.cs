using System;
using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game
{
    /// <summary>A seam between two pieces of a body, and its chance to hold when the body bursts.</summary>
    public readonly struct SeamLink
    {
        public readonly int A;
        public readonly int B;
        public readonly float Hold;

        public SeamLink(int a, int b, float hold)
        {
            A = a;
            B = b;
            Hold = hold;
        }
    }

    /// <summary>What killed a body, as far as its burst cares.</summary>
    public readonly struct KillingHit
    {
        /// <summary>Where the hit came from: the bullet, the shell where it went off, the attacker.</summary>
        public readonly Vector3 Origin;

        public readonly float Damage;
        public readonly bool Blast;

        /// <summary>1 at a blast's centre, 0 at its edge.</summary>
        public readonly float BlastShare;

        public KillingHit(Vector3 origin, float damage, bool blast, float blastShare)
        {
            Origin = origin;
            Damage = damage;
            Blast = blast;
            BlastShare = blastShare;
        }
    }

    /// <summary>How hard the clusters of a burst body fly. The defaults are Regax_Gibs'.</summary>
    [Serializable]
    public sealed class GibSpeeds
    {
        [Tooltip("Speed (m/s) a cluster leaves at from the weakest hit.")]
        public float minSpeed = 1.5f;

        [Tooltip("Speed (m/s) a cluster leaves at from the strongest hit.")]
        public float maxSpeed = 4f;

        [Tooltip("Damage of a hit that throws at full speed; weaker hits scale down to the minimum.")]
        public float fullSpeedDamage = 100f;

        [Tooltip("Share of the speed added straight up, so pieces arc instead of skimming the floor.")]
        public float lift = 0.35f;

        [Tooltip("Random spin (rad/s) per axis, for a lone limb; bigger clusters turn slower.")]
        public float spin = 3f;

        [Tooltip("Each cluster's speed is scaled by a random factor within 1 ± this.")]
        public float speedJitter = 0.4f;

        [Tooltip("How far (share of the speed) a cluster's direction may stray from straight away.")]
        public float scatter = 0.4f;

        [Tooltip("Speed (m/s) a cluster leaves at from the edge of a blast.")]
        public float blastMinSpeed = 2f;

        [Tooltip("Speed (m/s) a cluster leaves at from the centre of a blast.")]
        public float blastMaxSpeed = 4f;

        public float blastLift = 0.4f;
        public float blastSpin = 5f;
    }

    /// <summary>
    /// The rules every burst body follows, whether its pieces are Maya-cut ragdolls (Gibs) or voxels
    /// (VoxelGibs): what the killing hit was, which seams hold, how fast and which way each
    /// cluster flies.
    /// </summary>
    public static class GibRules
    {
        /// <remarks>
        /// The death event carries the dying entity, not its killer, so the killer is read from the
        /// last damage event. DamageUseCase raises that from inside Health.Reduce, before
        /// OnHealthEmpty, so the last hit is always known by the time the death event arrives.
        /// </remarks>
        public static KillingHit ReadHit(TakeDamageArgs hit, Vector3 fallback)
        {
            Vector3 origin = fallback;
            float damage = 0f;
            bool blast = false;
            float blastShare = 1f;

            if (hit?.source != null)
            {
                if (TryGetPosition(hit.source, out Vector3 from))
                    origin = from;

                if (hit.source.TryGetDamage(out IReactiveVariable<int> sourceDamage))
                    damage = sourceDamage.Value * hit.damageFactor;

                // A tank shell or a rocket: the shell sits where it went off, and the damage factor is
                // how close to the centre the body stood (1 there, the projectile's minimum at the edge).
                blast = ExplosionUseCase.IsExplosive(hit.source);
                if (blast)
                {
                    float minFactor = hit.source.TryGetExplosionMinDamageFactor(out float min) ? min : 0.25f;
                    blastShare = Mathf.InverseLerp(minFactor, 1f, hit.damageFactor);
                }
            }

            return new KillingHit(origin, damage, blast, blastShare);
        }

        public static bool TryGetPosition(IEntity source, out Vector3 position)
        {
            if (source.TryGetTransform(out Transform transform) && transform != null)
            {
                position = transform.position;
                return true;
            }

            if (source.TryGetGameObject(out GameObject gameObject) && gameObject != null)
            {
                position = gameObject.transform.position;
                return true;
            }

            position = default;
            return false;
        }

        /// <summary>
        /// Rolls every seam and returns each piece's cluster id; <paramref name="held"/> gets the
        /// indices of the seams that held. Two pieces or more always give at least two clusters: a
        /// body that stayed whole is no burst, so random held seams are let go until it splits.
        /// </summary>
        public static int[] RollClusters(int pieces, IReadOnlyList<SeamLink> seams, Func<float> roll, List<int> held)
        {
            int[] cluster = new int[pieces];
            RollClusters(seams, roll, held, cluster);
            return cluster;
        }

        /// <summary>
        /// <see cref="RollClusters(int, IReadOnlyList{SeamLink}, Func{float}, List{int})"/> into
        /// <paramref name="cluster"/> (one entry per piece): the same rolls, nothing allocated.
        /// </summary>
        public static void RollClusters(IReadOnlyList<SeamLink> seams, Func<float> roll, List<int> held, int[] cluster)
        {
            held.Clear();
            for (int i = 0; i < seams.Count; i++)
                if (roll() < seams[i].Hold)
                    held.Add(i);

            Clusters(cluster, seams, held);
            while (held.Count > 0 && Distinct(cluster) < 2)
            {
                held.RemoveAt(Random.Range(0, held.Count));
                Clusters(cluster, seams, held);
            }
        }

        public static void Throw(GibSpeeds speeds, in KillingHit hit, out float speed, out float lift, out float spin)
        {
            speed = hit.Blast
                ? Mathf.Lerp(speeds.blastMinSpeed, speeds.blastMaxSpeed, Mathf.Clamp01(hit.BlastShare))
                : Mathf.Lerp(speeds.minSpeed, speeds.maxSpeed, Mathf.Clamp01(hit.Damage / speeds.fullSpeedDamage));
            lift = hit.Blast ? speeds.blastLift : speeds.lift;
            spin = hit.Blast ? speeds.blastSpin : speeds.spin;
        }

        /// <summary>Away from the hit (never down into the floor), lifted, scattered, at a jittered speed.</summary>
        public static Vector3 ClusterVelocity(Vector3 centre, Vector3 origin, float speed, float lift, GibSpeeds speeds)
        {
            Vector3 away = centre - origin;
            away.y = Mathf.Max(away.y, 0f);
            if (away.sqrMagnitude < 0.0001f)
                away = Vector3.up;

            float clusterSpeed = speed * Random.Range(1f - speeds.speedJitter, 1f + speeds.speedJitter);
            return (away.normalized + Vector3.up * lift + Random.insideUnitSphere * speeds.scatter) * clusterSpeed;
        }

        /// <param name="weight">How big the cluster is: 1 for a lone limb, more for more.</param>
        /// <remarks>A whole chest with its arms spinning like a lone hand looks weightless.</remarks>
        public static Vector3 ClusterSpin(float spin, float weight) =>
            Random.insideUnitSphere * (spin / Mathf.Sqrt(weight + 0.5f));

        private static void Clusters(int[] parent, IReadOnlyList<SeamLink> seams, List<int> held)
        {
            int count = parent.Length;
            for (int i = 0; i < count; i++)
                parent[i] = i;

            for (int k = 0; k < held.Count; k++)
            {
                int s = held[k];
                parent[Find(parent, seams[s].A)] = Find(parent, seams[s].B);
            }

            for (int i = 0; i < count; i++)
                parent[i] = Find(parent, i);
        }

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
                i = parent[i] = parent[parent[i]];
            return i;
        }

        private static int Distinct(int[] cluster)
        {
            int distinct = 0;
            for (int i = 0; i < cluster.Length; i++)
            {
                bool seen = false;
                for (int j = 0; j < i && !seen; j++)
                    seen = cluster[j] == cluster[i];
                if (!seen)
                    distinct++;
            }

            return distinct;
        }
    }
}
