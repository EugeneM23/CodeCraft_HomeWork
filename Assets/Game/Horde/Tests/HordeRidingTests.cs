using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Horde.Tests
{
    /// <summary>Riders (<see cref="HordeState.Riding"/>) in the simulation and the riding arithmetic (<see cref="HordeRiding"/>).</summary>
    public class HordeRidingTests
    {
        private const float DT = 0.05f;

        private static HordeTank Tank(float3 center, float3 velocity) => new()
        {
            Present = true,
            Center = center,
            Rotation = quaternion.identity,
            HalfExtents = new float3(1.5f, 0.8f, 3f),
            Velocity = velocity
        };

        private static void Step(HordeSimulation sim, in HordeTank tank, int steps = 1)
        {
            for (int i = 0; i < steps; i++)
            {
                sim.Schedule(DT, tank);
                sim.Complete();
            }
        }

        [Test]
        public void Rider_StaysWhereItIsPlaced_IsNotCrushedOrPushed_AndIsNotDrawnByTheStep()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 20);
            using var field = new HordeFlowField(grid);
            using var sim = new HordeSimulation(8, grid, field, HordeSettings.Default);
            var center = new float3(10f, 0.8f, 10f);
            int m = sim.Spawn(new float3(10f, 0f, 14f), 0f);
            Assert.IsTrue(sim.BeginRide(m));
            Assert.AreEqual(HordeState.Riding, sim.GetState(m));

            // On the deck, inside the hull box of a fast tank: a walker there would be crushed.
            var deck = new float3(10.3f, 1.6f, 11f);
            sim.SetRidePose(m, deck, 1f);
            HordeTank tank = Tank(center, new float3(0f, 0f, 8f));
            Step(sim, tank, 10);

            Assert.AreEqual(HordeState.Riding, sim.GetState(m), "not crushed");
            Assert.AreEqual(0, sim.Deaths.Length);
            Assert.AreEqual(deck, sim.GetPosition(m), "not steered or pushed out of the hull");
            Assert.AreEqual(deck, sim.GetTargetPosition(m), "the targets see it where it rides");
            Assert.AreEqual(0f, sim.StepDamage, "the step does not bite for it (its owner strikes)");
            Assert.AreEqual(0, sim.VisibleCount, "drawn by its owner, not the crowd");
            Assert.IsTrue(sim.IsTargetAlive(m, sim.Version(m)));
            Assert.AreEqual(1, sim.AliveCount);
        }

        [Test]
        public void Rider_TakesHitsAndDiesWhereItRides()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 20);
            using var field = new HordeFlowField(grid);
            using var sim = new HordeSimulation(8, grid, field, HordeSettings.Default);
            int m = sim.Spawn(new float3(5f, 0f, 5f), 0f);
            sim.BeginRide(m);
            var deck = new float3(10f, 1.6f, 11f);
            sim.SetRidePose(m, deck, 0f);
            int version = sim.Version(m);

            sim.Damage(m, version, 30f);
            Step(sim, Tank(new float3(10f, 0.8f, 10f), float3.zero));
            Assert.AreEqual(HordeState.Riding, sim.GetState(m), "hurt, still riding");
            Assert.Less(sim.GetHealth(m), HordeSettings.Default.Health);

            sim.Damage(m, version, 1000f);
            Step(sim, Tank(new float3(10f, 0.8f, 10f), float3.zero));
            Assert.AreEqual(1, sim.Deaths.Length);
            Assert.AreEqual(m, sim.Deaths[0].Index);
            Assert.AreEqual(HordeDeathCause.Damaged, sim.Deaths[0].Cause);
            Assert.AreEqual(deck, sim.Deaths[0].Position, "dies where it rode (its owner moves the corpse)");
            Assert.AreEqual(HordeState.Free, sim.GetState(m));
            Assert.IsFalse(sim.IsTargetAlive(m, version));
        }

        [Test]
        public void Rider_BlastsHurtButDoNotShoveIt()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 20);
            using var field = new HordeFlowField(grid);
            using var sim = new HordeSimulation(8, grid, field, HordeSettings.Default);
            int m = sim.Spawn(new float3(5f, 0f, 5f), 0f);
            sim.BeginRide(m);
            var deck = new float3(10f, 1.6f, 11f);
            sim.SetRidePose(m, deck, 0f);

            sim.AddAreaDamage(deck + new float3(1f, 0f, 0f), 3f, 10f);
            sim.AddKnockback(deck + new float3(1f, 0f, 0f), 3f, 2f);
            Step(sim, default);

            Assert.Less(sim.GetHealth(m), HordeSettings.Default.Health, "the blast hurt it");
            Assert.AreEqual(deck, sim.GetPosition(m), "but did not shove it off the hull");
        }

        [Test]
        public void EndRide_PutsItBackOnTheGround_AndItWalksAgain()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 20);
            using var field = new HordeFlowField(grid);
            using var sim = new HordeSimulation(8, grid, field, HordeSettings.Default);
            int m = sim.Spawn(new float3(5f, 0f, 5f), 0f);
            sim.BeginRide(m);
            Assert.IsFalse(sim.BeginRide(m), "already riding");

            Assert.IsTrue(sim.EndRide(m, new float3(3f, 0f, 3f), 0f));
            Assert.AreEqual(HordeState.Walk, sim.GetState(m));
            Assert.IsFalse(sim.EndRide(m, float3.zero, 0f), "no longer a rider");
            Step(sim, Tank(new float3(15f, 0.8f, 15f), float3.zero), 10);
            Assert.Greater(math.distance(sim.GetPosition(m), new float3(3f, 0f, 3f)), 0.5f, "walks toward the tank");
        }

        [Test]
        public void BeginRide_RefusesEmergingAndFreeSlots()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 20);
            using var field = new HordeFlowField(grid);
            using var sim = new HordeSimulation(8, grid, field, HordeSettings.Default);
            int emerging = sim.Spawn(new float3(5f, 0f, 5f), 0f, emerge: true);
            Assert.IsFalse(sim.BeginRide(emerging));
            Assert.IsFalse(sim.BeginRide(7), "a free slot");
            Assert.IsFalse(sim.BeginRide(-1));
        }

        [Test]
        public void SetRidePose_AllocatesNothing()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 20);
            using var field = new HordeFlowField(grid);
            using var sim = new HordeSimulation(8, grid, field, HordeSettings.Default);
            int m = sim.Spawn(new float3(5f, 0f, 5f), 0f);
            sim.BeginRide(m);
            var at = new float3(1f, 2f, 3f);
            Assert.That(() => sim.SetRidePose(m, at, 0.5f), NoAllocation.Constraint);
        }

        // ------------------------------------------------------------------ arithmetic

        [Test]
        public void LeapChance_FullWhenSlow_ScaledWhenFast_NoneAboveTheLimit()
        {
            HordeRidingSettings s = HordeRidingSettings.Default;
            Assert.AreEqual(s.LeapChance, HordeRiding.LeapChance(s, 0f));
            Assert.AreEqual(s.LeapChance * s.FastChanceScale, HordeRiding.LeapChance(s, s.SlowSpeed + 1f), 1e-6f);
            Assert.AreEqual(0f, HordeRiding.LeapChance(s, s.MaxLeapTankSpeed));
            Assert.AreEqual(0f, HordeRiding.LeapChance(s, 0f, s.TurnThreshold + 0.5f), "not onto a tank that turns hard");
        }

        [Test]
        public void LeapPoint_StartsAndEndsOnItsPoints_AndArcsAboveTheLine()
        {
            var from = new float3(0f, 0f, 0f);
            var to = new float3(2f, 1.6f, 0f);
            Assert.AreEqual(from, HordeRiding.LeapPoint(from, to, 1.2f, 0f));
            Assert.AreEqual(to, HordeRiding.LeapPoint(from, to, 1.2f, 1f));
            float3 mid = HordeRiding.LeapPoint(from, to, 1.2f, 0.5f);
            Assert.AreEqual(0.8f + 1.2f, mid.y, 1e-5f, "the arc's top over the line's middle");
            Assert.AreEqual(1f, mid.x, 1e-5f);
        }

        [Test]
        public void LeapTime_IsClamped()
        {
            HordeRidingSettings s = HordeRidingSettings.Default;
            Assert.AreEqual(s.LeapMinTime, HordeRiding.LeapTime(s, 0.1f));
            Assert.AreEqual(s.LeapMaxTime, HordeRiding.LeapTime(s, 100f));
        }

        [Test]
        public void Grip_HoldsInCalmDriving_WearsDownInSharpTurns_AndAHitKnocksItLoose()
        {
            HordeRidingSettings s = HordeRidingSettings.Default;
            Assert.AreEqual(0f, HordeRiding.Shake(s, 0.5f, 5f, 3f), "a gentle turn at cruising speed shakes nothing");

            float calm = 1f;
            for (int i = 0; i < 600; i++)
                calm = HordeRiding.Grip(s, calm, HordeRiding.Shake(s, 0.5f, 5f, 3f), 0, 1f / 60f);
            Assert.AreEqual(1f, calm, "ten seconds of calm driving: still a full grip");

            float turning = 1f;
            float seconds = 0f;
            while (turning > 0f && seconds < 10f)
            {
                turning = HordeRiding.Grip(s, turning, HordeRiding.Shake(s, 2f, 4f, 3f), 0, 1f / 60f);
                seconds += 1f / 60f;
            }

            Assert.LessOrEqual(turning, 0f, "a hard pivot throws it off");
            Assert.Less(seconds, 2f, "within a couple of seconds");

            Assert.Greater(HordeRiding.Shake(s, 0f, 0f, 60f), 5f, "a ram (60 m/s² jolt) shakes hard");

            float hit = HordeRiding.Grip(s, 1f, 0f, 1, 1f / 60f);
            Assert.AreEqual(1f - s.HitShake, hit, 0.01f);
            Assert.LessOrEqual(HordeRiding.Grip(s, hit, 0f, 2, 1f / 60f), 0f, "three hits and it lets go");
        }

        [Test]
        public void StrikeInterval_VariesWithinAQuarter()
        {
            HordeRidingSettings s = HordeRidingSettings.Default;
            for (int slot = 0; slot < 20; slot++)
            for (int k = 0; k < 5; k++)
            {
                float t = HordeRiding.StrikeInterval(s, slot, k);
                Assert.GreaterOrEqual(t, s.StrikeInterval * 0.75f - 1e-5f);
                Assert.LessOrEqual(t, s.StrikeInterval * 1.25f + 1e-5f);
            }
        }
    }
}
