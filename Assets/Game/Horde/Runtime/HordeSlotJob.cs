using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace Game.Horde
{
    /// <summary>The attack ring of one step: its shape round the hull and how many places it has.</summary>
    internal struct HordeRing
    {
        /// <summary>No ring this step (no tank, or the places are off): every monster steers as the old crowd.</summary>
        public bool Active;
        public float2 Half;
        /// <summary>Distance of the ring from the hull box: the attackers' middles stand there.</summary>
        public float Standoff;
        /// <summary>Distance of the waiting row from the hull box (a body behind the ring).</summary>
        public float WaitStandoff;
        public float Length;
        public int Count;
        /// <summary>Largest body radius that takes a place (bigger kinds — the brute — push in as before).</summary>
        public float MaxRadius;
    }

    /// <summary>
    /// Hands out the places of the attack ring (<see cref="HordeHullRing"/>) to the melee monsters near the hull, one
    /// monster a place. Places a skinned body outside the crowd stands on (<see cref="HordeObstacles"/>) are taken first.
    /// A monster keeps its place while it stays near (riders, the dead and the far give theirs up); then the ones nearest
    /// the hull take the free place nearest to them round the ring, then the rest. Writes <c>Slot</c> (-1: none) per
    /// monster and <c>Owner</c> per place. One thread: a few hundred monsters near the hull at most.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    internal struct HordeSlotJob : IJob
    {
        /// <summary>A monster gives its place up this much further out than it takes one, m.</summary>
        private const float KEEP_MARGIN = 2f;

        [ReadOnly] public NativeArray<float3> Position;
        [ReadOnly] public NativeArray<HordeState> State;
        [ReadOnly] public NativeArray<byte> Kind;
        [ReadOnly] public NativeArray<HordeKind> Kinds;
        [ReadOnly] public NativeArray<float4> Obstacles;
        public NativeArray<int> Slot;
        public NativeArray<int> Owner;
        public HordeRing Ring;
        public HordeTank Tank;
        public HordeSettings Settings;
        public int Count;
        public int ObstacleCount;
        public ProfilerMarker Marker;

        public void Execute()
        {
            Marker.Begin();
            for (int k = 0; k < Owner.Length; k++)
                Owner[k] = -1;

            if (!Ring.Active)
            {
                for (int i = 0; i < Count; i++)
                    Slot[i] = -1;
                Marker.End();
                return;
            }

            quaternion inverse = math.inverse(Tank.Rotation);
            float slotWidth = Ring.Length / Ring.Count;

            // Places a body outside the crowd stands on.
            for (int o = 0; o < ObstacleCount; o++)
            {
                float4 body = Obstacles[o];
                if (body.w <= 0f)
                    continue;
                float2 local = HordeHull.ToLocal(Tank, inverse, body.xyz).xz;
                float2 q = math.abs(local) - Ring.Half;
                float hull = math.length(math.max(q, 0f)) + math.min(math.max(q.x, q.y), 0f);
                if (math.abs(hull - Ring.Standoff) > body.w + Settings.Radius)
                    continue;
                float t = HordeHullRing.Param(local, Ring.Half, Ring.Standoff);
                int reach = (int)math.ceil((body.w + Settings.Radius) / slotWidth);
                int centre = HordeHullRing.NearestSlot(t, Ring.Count, Ring.Length);
                for (int d = -reach; d <= reach; d++)
                {
                    int k = ((centre + d) % Ring.Count + Ring.Count) % Ring.Count;
                    float2 p = HordeHullRing.PointAt(HordeHullRing.SlotParam(k, Ring.Count, Ring.Length), Ring.Half, Ring.Standoff, out _);
                    if (math.distance(p, local) < body.w + Settings.Radius)
                        Owner[k] = -2;
                }
            }

            // Keep: a monster still near keeps the place it had.
            float keep = Settings.SlotRange + KEEP_MARGIN;
            for (int i = 0; i < Count; i++)
            {
                int slot = Slot[i];
                if (slot < 0)
                    continue;
                if (slot >= Ring.Count || !Eligible(i) || Owner[slot] != -1 || HullDistance(i, inverse) > keep)
                {
                    Slot[i] = -1;
                    continue;
                }

                Owner[slot] = i;
            }

            // Take: the front row first, then the rest within reach of the ring.
            Claim(inverse, Settings.SlotRange * 0.5f);
            Claim(inverse, Settings.SlotRange);
            Marker.End();
        }

        private void Claim(quaternion inverse, float range)
        {
            for (int i = 0; i < Count; i++)
            {
                if (Slot[i] >= 0 || !Eligible(i))
                    continue;
                float2 local = HordeHull.ToLocal(Tank, inverse, Position[i]).xz;
                float2 q = math.abs(local) - Ring.Half;
                float hull = math.length(math.max(q, 0f)) + math.min(math.max(q.x, q.y), 0f);
                if (hull > range)
                    continue;

                float t = HordeHullRing.Param(local, Ring.Half, Ring.Standoff);
                int centre = HordeHullRing.NearestSlot(t, Ring.Count, Ring.Length);
                int half = Ring.Count / 2;
                // Nearest first, both ways round (the side it already leans to first).
                float lean = HordeHullRing.Delta(HordeHullRing.SlotParam(centre, Ring.Count, Ring.Length), t, Ring.Length);
                for (int d = 0; d <= half; d++)
                {
                    int first = lean >= 0f ? d : -d;
                    if (TryTake(i, centre + first) || (d > 0 && TryTake(i, centre - first)))
                        break;
                }
            }
        }

        private bool TryTake(int i, int slot)
        {
            int k = (slot % Ring.Count + Ring.Count) % Ring.Count;
            if (Owner[k] != -1)
                return false;
            Owner[k] = i;
            Slot[i] = k;
            return true;
        }

        private bool Eligible(int i)
        {
            HordeState state = State[i];
            if (state != HordeState.Walk && state != HordeState.Attack)
                return false;
            HordeKind kind = Kinds[Kind[i]];
            return kind.Tactic == HordeTactic.Melee && kind.Radius <= Ring.MaxRadius;
        }

        private float HullDistance(int i, quaternion inverse) =>
            HordeHull.Distance(Tank, HordeHull.ToLocal(Tank, inverse, Position[i]));
    }
}
