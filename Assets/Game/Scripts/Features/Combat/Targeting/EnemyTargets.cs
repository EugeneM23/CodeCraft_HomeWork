using System;
using Unity.Profiling;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Every enemy the guns, turrets and skills can aim at, over the registered sources (entity enemies, the horde).
    /// GameContext key: EnemyTargets. A source id is its slot here; a freed slot is reused by the next source, so an
    /// <see cref="EnemyRef"/> of an unregistered source can point at a new one — its version check turns it away.
    /// No allocations in queries.
    /// </summary>
    public sealed class EnemyTargets : IEnemyTargets
    {
        /// <summary>Densest: coarse grid side (samples per side) over the search square.</summary>
        private const int DENSE_GRID = 8;

        /// <summary>Densest: fine grid side around the best coarse sample.</summary>
        private const int DENSE_REFINE = 5;

        private static readonly ProfilerMarker DensestMarker = new("Combat.FindDensest");

        private IEnemyTargetSource[] _sources = new IEnemyTargetSource[4];
        private bool[] _physical = new bool[4];

        public int Register(IEnemyTargetSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            for (int i = 0; i < _sources.Length; i++)
                if (ReferenceEquals(_sources[i], source))
                    return i;

            int slot = Array.IndexOf(_sources, null);
            if (slot < 0)
            {
                slot = _sources.Length;
                Array.Resize(ref _sources, slot * 2);
                Array.Resize(ref _physical, slot * 2);
            }

            _sources[slot] = source;
            _physical[slot] = source is IPhysicalEnemyTargetSource;
            return slot;
        }

        public void Unregister(int source)
        {
            if (source < 0 || source >= _sources.Length)
                return;
            _sources[source] = null;
            _physical[source] = false;
        }

        public int AliveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _sources.Length; i++)
                    if (_sources[i] != null)
                        count += _sources[i].AliveCount;
                return count;
            }
        }

        public bool TryFindNearest(Vector3 from, float range, out EnemyRef target, out Vector3 position)
        {
            target = EnemyRef.None;
            position = default;
            float best = range;
            bool found = false;

            for (int s = 0; s < _sources.Length; s++)
            {
                IEnemyTargetSource source = _sources[s];
                if (source == null || source.AliveCount == 0)
                    continue;

                // The range shrinks to the best so far: a later source only answers with something nearer.
                if (!source.TryFindNearest(from, best, out int index, out int version, out Vector3 at))
                    continue;

                float distance = Vector3.Distance(from, at);
                if (found && distance >= best)
                    continue;

                best = distance;
                target = new EnemyRef(s, index, version);
                position = at;
                found = true;
            }

            return found;
        }

        /// <summary>
        /// The nearest live enemy farther than <paramref name="minRange"/> and within <paramref name="maxRange"/>
        /// (the ricochet: outside its own blast). Sources without <see cref="IEnemyTargetRingQuery"/> answer with their
        /// plain nearest, which counts only beyond <paramref name="minRange"/>.
        /// </summary>
        public bool TryFindNearestBeyond(Vector3 from, float minRange, float maxRange, out EnemyRef target,
            out Vector3 position)
        {
            target = EnemyRef.None;
            position = default;
            float best = maxRange;
            bool found = false;

            for (int s = 0; s < _sources.Length; s++)
            {
                IEnemyTargetSource source = _sources[s];
                if (source == null || source.AliveCount == 0)
                    continue;

                int index;
                int version;
                Vector3 at;
                bool hit = source is IEnemyTargetRingQuery ring
                    ? ring.TryFindNearestBeyond(from, minRange, best, out index, out version, out at)
                    : source.TryFindNearest(from, best, out index, out version, out at);
                if (!hit)
                    continue;

                float distance = Vector3.Distance(from, at);
                if (distance <= minRange || (found && distance >= best))
                    continue;

                best = distance;
                target = new EnemyRef(s, index, version);
                position = at;
                found = true;
            }

            return found;
        }

        /// <summary>
        /// Samples a coarse grid over the square round <paramref name="from"/> (points within range only), counts the
        /// enemies within <paramref name="clusterRadius"/> of each, then refines round the best one. Point at the
        /// height of <paramref name="from"/>. About 90 counts per call: for skills on a timer, not for every frame.
        /// </summary>
        public bool TryFindDensest(Vector3 from, float range, float clusterRadius, out Vector3 point)
        {
            point = from;
            if (range <= 0f || clusterRadius <= 0f || AliveCount == 0)
                return false;

            using (DensestMarker.Auto())
            {
                float step = 2f * range / (DENSE_GRID - 1);
                int bestCount = 0;
                Vector3 best = from;

                for (int z = 0; z < DENSE_GRID; z++)
                for (int x = 0; x < DENSE_GRID; x++)
                {
                    Vector3 p = new Vector3(from.x - range + x * step, from.y, from.z - range + z * step);
                    if (!WithinFlat(from, p, range))
                        continue;

                    int count = CountInRadius(p, clusterRadius);
                    if (count > bestCount)
                    {
                        bestCount = count;
                        best = p;
                    }
                }

                if (bestCount == 0)
                    return false;

                // Finer round the best coarse point: half a coarse step to either side.
                float fine = step / (DENSE_REFINE - 1);
                Vector3 center = best;
                int half = DENSE_REFINE / 2;
                for (int z = -half; z <= half; z++)
                for (int x = -half; x <= half; x++)
                {
                    if (x == 0 && z == 0)
                        continue;
                    Vector3 p = new Vector3(center.x + x * fine, from.y, center.z + z * fine);
                    if (!WithinFlat(from, p, range))
                        continue;

                    int count = CountInRadius(p, clusterRadius);
                    if (count > bestCount)
                    {
                        bestCount = count;
                        best = p;
                    }
                }

                point = best;
                return true;
            }
        }

        /// <summary>Live enemies of every source within radius of the point.</summary>
        public int CountInRadius(Vector3 center, float radius)
        {
            int count = 0;
            for (int s = 0; s < _sources.Length; s++)
            {
                IEnemyTargetSource source = _sources[s];
                if (source != null && source.AliveCount > 0)
                    count += source.CountInRadius(center, radius);
            }

            return count;
        }

        public bool IsAlive(in EnemyRef target)
        {
            IEnemyTargetSource source = SourceOf(target);
            return source != null && source.IsAlive(target.Index, target.Version);
        }

        public Vector3 GetPosition(in EnemyRef target)
        {
            IEnemyTargetSource source = SourceOf(target);
            return source != null ? source.GetPosition(target.Index) : default;
        }

        public bool Raycast(Vector3 from, Vector3 to, float radius, out EnemyRef target, out Vector3 point) =>
            Raycast(from, to, radius, true, out target, out point);

        /// <summary>
        /// <see cref="Raycast(Vector3,Vector3,float,out EnemyRef,out Vector3)"/> over the sources without colliders
        /// only (the horde): bullets meet entity enemies through physics.
        /// </summary>
        public bool RaycastNonPhysical(Vector3 from, Vector3 to, float radius, out EnemyRef target, out Vector3 point) =>
            Raycast(from, to, radius, false, out target, out point);

        public void Damage(in EnemyRef target, float damage, Vector3 hitPoint)
        {
            IEnemyTargetSource source = SourceOf(target);
            if (source != null && damage > 0f && source.IsAlive(target.Index, target.Version))
                source.Damage(target.Index, target.Version, damage, hitPoint);
        }

        public void Tear(in EnemyRef target, Vector3 direction, float power = 1f)
        {
            if (SourceOf(target) is IEnemyTearable tearable)
                tearable.Tear(target.Index, target.Version, direction, power);
        }

        public void DamageArea(Vector3 center, float radius, float damage, float minFactor) =>
            DamageArea(center, radius, damage, minFactor, true);

        /// <summary>
        /// Area damage to the sources without colliders only (the horde): a shell blast damages entity enemies through
        /// its own overlap (ExplosionUseCase).
        /// </summary>
        public void DamageAreaNonPhysical(Vector3 center, float radius, float damage, float minFactor) =>
            DamageArea(center, radius, damage, minFactor, false);

        private bool Raycast(Vector3 from, Vector3 to, float radius, bool physical, out EnemyRef target,
            out Vector3 point)
        {
            target = EnemyRef.None;
            point = to;
            float best = float.MaxValue;
            bool found = false;

            for (int s = 0; s < _sources.Length; s++)
            {
                IEnemyTargetSource source = _sources[s];
                if (source == null || (!physical && _physical[s]) || source.AliveCount == 0)
                    continue;
                if (!source.Raycast(from, to, radius, out int index, out int version, out Vector3 at))
                    continue;

                float distance = (at - from).sqrMagnitude;
                if (distance >= best)
                    continue;

                best = distance;
                target = new EnemyRef(s, index, version);
                point = at;
                found = true;
            }

            return found;
        }

        private void DamageArea(Vector3 center, float radius, float damage, float minFactor, bool physical)
        {
            if (radius <= 0f || damage <= 0f)
                return;

            for (int s = 0; s < _sources.Length; s++)
            {
                IEnemyTargetSource source = _sources[s];
                if (source == null || (!physical && _physical[s]))
                    continue;
                source.DamageArea(center, radius, damage, minFactor);
            }
        }

        private IEnemyTargetSource SourceOf(in EnemyRef target) =>
            target.Source >= 0 && target.Source < _sources.Length ? _sources[target.Source] : null;

        private static bool WithinFlat(Vector3 from, Vector3 p, float range)
        {
            float dx = p.x - from.x;
            float dz = p.z - from.z;
            return dx * dx + dz * dz <= range * range + 0.001f;
        }
    }
}
