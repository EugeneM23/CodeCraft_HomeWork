using Game.Horde;
using Unity.Mathematics;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// The horde as combat's <see cref="IEnemyTargetSource"/>: index = monster slot, version = the slot's generation
    /// (bumped when its monster dies or the horde is cleared). Reads the snapshot the simulation takes when a step is
    /// completed (positions of the last completed step), so every query is valid at any time in the frame, also while
    /// the next step runs on the workers. Damage is queued and lands in the next step: a monster killed by it is still
    /// <see cref="IsAlive"/> until then, and its death (corpse, events) comes with that step. Positions are aim points,
    /// the middle of the body. Linear scans over the used slots, no allocations.
    /// </summary>
    public sealed class HordeTargetSource : IEnemyTargetSource
    {
        /// <summary>Middle of the body over the feet at render scale 1 (the Regax model), m.</summary>
        public const float BODY_HEIGHT = 1.3f;

        private HordeSimulation _simulation;

        public HordeTargetSource(HordeSimulation simulation = null) => _simulation = simulation;

        /// <summary>The simulation answered for (null: an empty horde).</summary>
        public HordeSimulation Simulation
        {
            get => _simulation;
            set => _simulation = value;
        }

        public int AliveCount => _simulation?.AliveCount ?? 0;

        public bool TryFindNearest(Vector3 from, float range, out int index, out int version, out Vector3 position)
        {
            index = -1;
            version = 0;
            position = default;
            if (_simulation == null)
                return false;

            float bestSq = range * range;
            int n = _simulation.HighWater;
            for (int i = 0; i < n; i++)
            {
                if (!_simulation.IsSlotLive(i))
                    continue;
                float3 c = Center(i);
                float d2 = math.distancesq(c, (float3)from);
                if (d2 > bestSq)
                    continue;
                bestSq = d2;
                index = i;
                position = c;
            }

            if (index < 0)
                return false;
            version = _simulation.Version(index);
            return true;
        }

        public bool IsAlive(int index, int version) => _simulation != null && _simulation.IsTargetAlive(index, version);

        public Vector3 GetPosition(int index) => _simulation != null && index >= 0 && index < _simulation.Capacity ? Center(index) : Vector3.zero;

        public bool Raycast(Vector3 from, Vector3 to, float radius, out int index, out int version, out Vector3 point)
        {
            index = -1;
            version = 0;
            point = default;
            if (_simulation == null)
                return false;

            float3 a = from;
            float3 ab = (float3)to - a;
            float lengthSq = math.lengthsq(ab);
            float bestT = float.MaxValue;
            int n = _simulation.HighWater;
            for (int i = 0; i < n; i++)
            {
                if (!_simulation.IsSlotLive(i))
                    continue;
                float3 c = Center(i);
                float reach = BodyRadius(i) + radius;
                float t = lengthSq > 1e-8f ? math.saturate(math.dot(c - a, ab) / lengthSq) : 0f;
                float3 closest = a + ab * t;
                float d2 = math.distancesq(closest, c);
                if (d2 > reach * reach)
                    continue;
                // Where the swept ball first touches the body: back along the ray from the closest point.
                float back = lengthSq > 1e-8f ? math.sqrt(reach * reach - d2) / math.sqrt(lengthSq) : 0f;
                float enter = math.max(0f, t - back);
                if (enter >= bestT)
                    continue;
                bestT = enter;
                index = i;
            }

            if (index < 0)
                return false;
            version = _simulation.Version(index);
            point = a + ab * bestT;
            return true;
        }

        public void Damage(int index, int version, float damage, Vector3 hitPoint) =>
            _simulation?.Damage(index, version, damage);

        public void DamageArea(Vector3 center, float radius, float damage, float minFactor) =>
            _simulation?.AddAreaDamage(center, radius, damage, minFactor);

        public int CountInRadius(Vector3 center, float radius)
        {
            if (_simulation == null)
                return 0;
            float rSq = radius * radius;
            int count = 0;
            int n = _simulation.HighWater;
            for (int i = 0; i < n; i++)
                if (_simulation.IsSlotLive(i) && math.distancesq(Center(i), (float3)center) <= rSq)
                    count++;
            return count;
        }

        private float3 Center(int i)
        {
            float scale = _simulation.GetKind(_simulation.GetKindOf(i)).RenderScale;
            return _simulation.GetTargetPosition(i) + new float3(0f, BODY_HEIGHT * scale, 0f);
        }

        private float BodyRadius(int i)
        {
            HordeKind kind = _simulation.GetKind(_simulation.GetKindOf(i));
            return math.max(kind.Radius, BODY_HEIGHT * 0.8f * kind.RenderScale);
        }
    }
}
