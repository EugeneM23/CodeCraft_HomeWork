using System.Collections.Generic;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>A target source over a plain list of points; a slot's version grows when it is killed or reused.</summary>
    public class FakeEnemyTargetSource : IEnemyTargetSource, IEnemyTargetRingQuery
    {
        public readonly List<Vector3> Points = new();
        public readonly List<bool> Live = new();
        public readonly List<int> Versions = new();
        public readonly List<float> Dealt = new();
        public int AreaCalls;

        public int Add(Vector3 point)
        {
            Points.Add(point);
            Live.Add(true);
            Versions.Add(1);
            Dealt.Add(0f);
            return Points.Count - 1;
        }

        public void Kill(int index)
        {
            Live[index] = false;
            Versions[index]++;
        }

        public void Respawn(int index, Vector3 point)
        {
            Points[index] = point;
            Live[index] = true;
            Versions[index]++;
        }

        public int AliveCount
        {
            get
            {
                int count = 0;
                foreach (bool live in Live)
                    if (live)
                        count++;
                return count;
            }
        }

        public bool TryFindNearest(Vector3 from, float range, out int index, out int version, out Vector3 position)
        {
            index = -1;
            version = 0;
            position = default;
            float best = range;
            for (int i = 0; i < Points.Count; i++)
            {
                float d = Vector3.Distance(from, Points[i]);
                if (!Live[i] || d > best)
                    continue;
                best = d;
                index = i;
            }

            if (index < 0)
                return false;
            version = Versions[index];
            position = Points[index];
            return true;
        }

        public bool TryFindNearestBeyond(Vector3 from, float minRange, float maxRange, out int index, out int version,
            out Vector3 position)
        {
            index = -1;
            version = 0;
            position = default;
            float best = maxRange;
            for (int i = 0; i < Points.Count; i++)
            {
                float d = Vector3.Distance(from, Points[i]);
                if (!Live[i] || d <= minRange || d > best)
                    continue;
                best = d;
                index = i;
            }

            if (index < 0)
                return false;
            version = Versions[index];
            position = Points[index];
            return true;
        }

        public bool IsAlive(int index, int version) => Live[index] && Versions[index] == version;

        public Vector3 GetPosition(int index) => Points[index];

        public bool Raycast(Vector3 from, Vector3 to, float radius, out int index, out int version, out Vector3 point)
        {
            index = -1;
            version = 0;
            point = to;
            float best = float.MaxValue;
            for (int i = 0; i < Points.Count; i++)
            {
                if (!Live[i] || !TargetGeometry.SegmentSphere(from, to, Points[i], radius + 0.5f, out float t) ||
                    t >= best)
                    continue;
                best = t;
                index = i;
            }

            if (index < 0)
                return false;
            version = Versions[index];
            point = Vector3.Lerp(from, to, best);
            return true;
        }

        public void Damage(int index, int version, float damage, Vector3 hitPoint) => Dealt[index] += damage;

        public void DamageArea(Vector3 center, float radius, float damage, float minFactor) => AreaCalls++;

        public int CountInRadius(Vector3 center, float radius)
        {
            int count = 0;
            for (int i = 0; i < Points.Count; i++)
                if (Live[i] && Vector3.Distance(center, Points[i]) <= radius)
                    count++;
            return count;
        }
    }
}
