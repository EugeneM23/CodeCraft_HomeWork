using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Horde.Tests
{
    /// <summary>
    /// The attack ring round the hull (<see cref="HordeHullRing"/>) and the crowd on it: the places, the waiting row,
    /// bodies that do not pass through each other, and the skinned bodies the crowd keeps out of.
    /// </summary>
    public class HordeHullRingTests
    {
        private const float DT = 0.05f;
        private static readonly float2 Half = new(1.75f, 3.25f);

        private static HordeTank Tank(float3 center) => new()
        {
            Present = true,
            Center = center,
            Rotation = quaternion.identity,
            HalfExtents = new float3(Half.x, 1f, Half.y)
        };

        private static void Step(HordeSimulation sim, in HordeTank tank, int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                sim.Schedule(DT, tank);
                sim.Complete();
            }
        }

        private static float HullGap(float2 local, float2 half)
        {
            float2 q = math.abs(local) - half;
            return math.length(math.max(q, 0f)) + math.min(math.max(q.x, q.y), 0f);
        }

        [Test]
        public void Ring_EveryPointStandsTheStandoffOffTheHull_AndParamFindsItBack()
        {
            const float standoff = 0.9f;
            float length = HordeHullRing.Length(Half, standoff);
            for (int i = 0; i < 200; i++)
            {
                float t = length * i / 200f;
                float2 p = HordeHullRing.PointAt(t, Half, standoff, out float2 normal);
                Assert.AreEqual(standoff, HullGap(p, Half), 1e-3f, $"t {t}: on the ring");
                Assert.AreEqual(1f, math.length(normal), 1e-3f);
                float back = HordeHullRing.Param(p, Half, standoff);
                Assert.AreEqual(0f, HordeHullRing.Delta(t, back, length), 1e-3f, $"t {t}: param of its own point");
            }
        }

        [Test]
        public void Ring_PlacesAreABodyApart()
        {
            const float standoff = 0.9f, spacing = 1.155f;
            int count = HordeHullRing.SlotCount(Half, standoff, spacing);
            float length = HordeHullRing.Length(Half, standoff);
            Assert.That(count, Is.InRange(20, 24), "a Tiger-sized hull has room for some 22 crawlers");
            for (int k = 0; k < count; k++)
            {
                float2 a = HordeHullRing.PointAt(HordeHullRing.SlotParam(k, count, length), Half, standoff, out _);
                float2 b = HordeHullRing.PointAt(HordeHullRing.SlotParam((k + 1) % count, count, length), Half, standoff, out _);
                Assert.Greater(math.distance(a, b), spacing * 0.85f, $"places {k} and {k + 1}");
            }
        }

        [Test]
        public void Approach_WalksRoundTheHull_BehindTheRing_NotThroughIt()
        {
            const float standoff = 0.9f, lane = standoff + 1.155f, direct = 1f;
            float length = HordeHullRing.Length(Half, standoff);
            var p = new float2(0f, -Half.y - 3f);                       // behind the tank
            float target = HordeHullRing.Param(new float2(0f, Half.y + 1f), Half, standoff); // the front place
            for (int i = 0; i < 400; i++)
            {
                float2 aim = HordeHullRing.Approach(p, target, Half, standoff, lane, direct);
                float2 to = aim - p;
                float d = math.length(to);
                if (d < 1e-3f)
                    break;
                p += to / d * math.min(d, 0.1f);
                float gap = HullGap(p, Half);
                Assert.Greater(gap, 0.6f, $"step {i}: the body (radius 0.525 in the game) stays clear of the hull");
                if (math.abs(HordeHullRing.Delta(HordeHullRing.Param(p, Half, standoff), target, length)) > direct + 0.5f)
                    Assert.Greater(gap, lane - 0.5f, $"step {i}: on the way round it keeps behind the ones at their places");
            }

            Assert.AreEqual(0f, HordeHullRing.Delta(HordeHullRing.Param(p, Half, standoff), target, length), 0.05f, "at its place");
        }

        [Test]
        public void Crowd_SpreadsRoundTheHull_OneOnAPlace_AndAttacks()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(40, 40);
            using var field = new HordeFlowField(grid);
            HordeSettings settings = HordeSettings.Default;
            using var sim = new HordeSimulation(64, grid, field, settings);
            HordeTank tank = Tank(new float3(20f, 0f, 20f));

            // Sixteen from one side, bunched: the old crowd heaped up on the near face.
            for (int i = 0; i < 16; i++)
                sim.Spawn(new float3(12f + (i % 5) * 0.7f, 0f, 18f + (i / 5) * 0.7f), 0f);
            Step(sim, tank, 300);

            int slots = sim.RingSlots;
            Assert.Greater(slots, 16, "room for all of them");
            var used = new bool[slots];
            int attacking = 0;
            float2 sum = float2.zero;
            for (int i = 0; i < 16; i++)
            {
                int slot = sim.GetRingSlot(i);
                Assert.GreaterOrEqual(slot, 0, $"monster {i} has a place");
                Assert.IsFalse(used[slot], $"place {slot} has one monster");
                used[slot] = true;
                if (sim.GetState(i) == HordeState.Attack)
                    attacking++;
                sum += sim.GetPosition(i).xz - tank.Center.xz;
            }

            Assert.AreEqual(16, attacking, "every one at the hull attacks");
            Assert.Less(math.length(sum / 16f), 1.5f, "spread round the hull, not heaped on the side they came from");
            AssertNoOverlap(sim, 16, settings.Radius * 2f * 0.9f);
        }

        [Test]
        public void Crowd_MoreThanThePlaces_TheRestWaitABodyFurtherOut_AndStepInWhenAPlaceFrees()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(40, 40);
            using var field = new HordeFlowField(grid);
            HordeSettings settings = HordeSettings.Default;
            using var sim = new HordeSimulation(64, grid, field, settings);
            HordeTank tank = Tank(new float3(20f, 0f, 20f));
            for (int i = 0; i < 40; i++)
            {
                math.sincos(i * 0.157f, out float s, out float c);
                sim.Spawn(new float3(20f + c * 9f, 0f, 20f + s * 9f), 0f);
            }

            Step(sim, tank, 300);
            int slots = sim.RingSlots;
            int placed = 0, waiting = -1;
            for (int i = 0; i < 40; i++)
            {
                if (sim.GetRingSlot(i) >= 0)
                    placed++;
                else
                    waiting = i;
            }

            Assert.AreEqual(slots, placed, "every place is taken");
            Assert.GreaterOrEqual(waiting, 0);
            float gap = HullGap(sim.GetPosition(waiting).xz - tank.Center.xz, Half);
            Assert.Greater(gap, settings.Radius + settings.AttackReach, "a waiting one stands out of the bite's reach");
            Assert.AreNotEqual(HordeState.Attack, sim.GetState(waiting));

            // A placed one dies: somebody waiting takes its place.
            int victim = -1;
            for (int i = 0; i < 40 && victim < 0; i++)
                if (sim.GetRingSlot(i) >= 0)
                    victim = i;
            int freed = sim.GetRingSlot(victim);
            sim.Kill(victim);
            Step(sim, tank, 2);
            int taker = -1;
            for (int i = 0; i < 40; i++)
                if (i != victim && sim.GetRingSlot(i) == freed)
                    taker = i;
            Assert.GreaterOrEqual(taker, 0, "the freed place is taken again");
            AssertNoOverlap(sim, 40, settings.Radius * 2f * 0.85f);
        }

        [Test]
        public void Crowd_SpawnedInOneHeap_EndsWithNoTwoBodiesInOnePlace()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(40, 40);
            using var field = new HordeFlowField(grid);
            HordeSettings settings = HordeSettings.Default;
            using var sim = new HordeSimulation(64, grid, field, settings);
            for (int i = 0; i < 30; i++)
            {
                // A heap 0.6 m across (a burrow, a blast's knockback): not one line, where every push points one way.
                math.sincos(i * 2.4f, out float s, out float c);
                float r = 0.3f * math.sqrt(i / 30f);
                sim.Spawn(new float3(20f + c * r, 0f, 20f + s * r), 0f);
            }

            Step(sim, default, 120);
            AssertNoOverlap(sim, 30, settings.Radius * 2f * 0.85f);
        }

        [Test]
        public void Obstacle_TheCrowdStepsOutOfIt_AndLeavesItsPlaceFree()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(40, 40);
            using var field = new HordeFlowField(grid);
            HordeSettings settings = HordeSettings.Default;
            using var sim = new HordeSimulation(64, grid, field, settings);
            HordeTank tank = Tank(new float3(20f, 0f, 20f));
            float standoff = settings.Radius + settings.AttackReach * 0.5f;
            var body = new float3(20f - Half.x - standoff, 0f, 20f);   // on the ring, at the left side's middle
            int handle = HordeObstacles.Register(body, 0.5f);
            try
            {
                for (int i = 0; i < 16; i++)
                    sim.Spawn(new float3(10f, 0f, 14f + i * 0.8f), 0f);
                Step(sim, tank, 300);

                for (int i = 0; i < 16; i++)
                {
                    float d = math.distance(sim.GetPosition(i).xz, body.xz);
                    Assert.Greater(d, (settings.Radius + 0.5f) * 0.85f, $"monster {i} is not inside the skinned body");
                }
            }
            finally
            {
                HordeObstacles.Unregister(handle);
            }
        }

        [Test]
        public void HullSlotsOff_TheOldCrowd_NoPlaces()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(40, 40);
            using var field = new HordeFlowField(grid);
            HordeSettings settings = HordeSettings.Default;
            settings.HullSlots = false;
            using var sim = new HordeSimulation(16, grid, field, settings);
            HordeTank tank = Tank(new float3(20f, 0f, 20f));
            int a = sim.Spawn(new float3(14f, 0f, 20f), 0f);
            Step(sim, tank, 100);
            Assert.AreEqual(0, sim.RingSlots);
            Assert.AreEqual(-1, sim.GetRingSlot(a));
            Assert.AreEqual(HordeState.Attack, sim.GetState(a), "it still reaches the hull and attacks");
        }

        private static void AssertNoOverlap(HordeSimulation sim, int count, float min)
        {
            for (int i = 0; i < count; i++)
            {
                if (!HordeStates.IsLive(sim.GetState(i)))
                    continue;
                for (int j = i + 1; j < count; j++)
                {
                    if (!HordeStates.IsLive(sim.GetState(j)))
                        continue;
                    float d = math.distance(sim.GetPosition(i).xz, sim.GetPosition(j).xz);
                    Assert.Greater(d, min, $"monsters {i} and {j} stand {d:0.00} m apart");
                }
            }
        }
    }
}
