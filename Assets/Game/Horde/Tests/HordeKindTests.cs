using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Horde.Tests
{
    /// <summary>Kinds (Crawler, Spitter), emerging, acid, direct hits and the crush threshold of <see cref="HordeSimulation"/>.</summary>
    public class HordeKindTests
    {
        private const float DT = 0.05f;
        private const int CRAWLER = 0;
        private const int SPITTER = 1;
        private const int BRUTE = 2;
        private static readonly float3 Half = new(1.5f, 0.8f, 3f);

        private static HordeTank Tank(float3 center, float3 velocity = default) => new()
        {
            Present = true,
            Center = center,
            Rotation = quaternion.identity,
            HalfExtents = Half,
            Velocity = velocity
        };

        private static HordeSimulation Make(HordeNavGrid grid, HordeFlowField field, int capacity = 16)
        {
            var sim = new HordeSimulation(capacity, grid, field, HordeSettings.Default);
            sim.SetKind(CRAWLER, HordeKind.Crawler);
            sim.SetKind(SPITTER, HordeKind.Spitter);
            sim.SetKind(BRUTE, HordeKind.Brute);
            return sim;
        }

        private static void Step(HordeSimulation sim, in HordeTank tank, float seconds)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += DT)
            {
                sim.Schedule(DT, tank);
                sim.Complete();
            }
        }

        private static HordeFlowField FieldTo(HordeNavGrid grid, float3 target)
        {
            var field = new HordeFlowField(grid);
            field.Schedule(target);
            field.Complete();
            return field;
        }

        [Test]
        public void Kinds_TheirHealthSpeedAndBiteApply_WithTheSpawnScales()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(60, 60);
            var center = new float3(30f, 0.8f, 30f);
            using HordeFlowField field = FieldTo(grid, center);
            using HordeSimulation sim = Make(grid, field);

            int crawler = sim.Spawn(new float3(30f, 0f, 55f), 0f, CRAWLER, 1.5f);
            int spitter = sim.Spawn(new float3(5f, 0f, 30f), 0f, SPITTER, 1.5f);
            Assert.AreEqual(30f, sim.GetHealth(crawler), 1e-4f, "20 × 1.5");
            Assert.AreEqual(75f, sim.GetHealth(spitter), 1e-4f, "50 × 1.5");
            Assert.AreEqual(SPITTER, sim.GetKindOf(spitter));

            Step(sim, Tank(center), 2f);
            float crawled = 55f - sim.GetPosition(crawler).z;
            float walked = sim.GetPosition(spitter).x - 5f;
            Assert.That(crawled, Is.InRange(HordeKind.CRAWLER_SPEED * 2f * 0.7f, HordeKind.CRAWLER_SPEED * 2f * 1.25f), "the crawler runs at its run clip's root speed (2.73 m/s)");
            Assert.That(walked, Is.InRange(4f, 8.5f), "the spitter walks at about 3.5 m/s");

            // A crawler at the hull bites 6/s × its damage scale.
            using HordeSimulation biting = Make(grid, field);
            biting.Spawn(new float3(30f, 0f, 33.2f), 0f, CRAWLER, 1f, 2f);
            Step(biting, Tank(center), DT);
            Assert.AreEqual(6f * 2f * DT, biting.StepDamage, 1e-3f);
            Assert.AreEqual(1, biting.BiterCount);
        }

        [Test]
        public void Spitter_WalksInWithTheCrowd_AndStopsInRange()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(80, 80);
            var center = new float3(40f, 0.8f, 40f);
            using HordeFlowField field = FieldTo(grid, center);
            using HordeSimulation sim = Make(grid, field);
            int far = sim.Spawn(new float3(40f, 0f, 78f), 0f, SPITTER);

            Step(sim, Tank(center), 15f);

            float d = math.distance(sim.GetPosition(far).xz, center.xz);
            Assert.That(d, Is.InRange(9f, 20f), "came in to shooting range and stopped");
        }

        [Test]
        public void Spitter_BacksOffWhenTheTankIsTooClose()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(80, 80);
            var center = new float3(40f, 0.8f, 40f);
            using HordeFlowField field = FieldTo(grid, center);
            using HordeSimulation sim = Make(grid, field);
            int near = sim.Spawn(new float3(46f, 0f, 40f), 0f, SPITTER);

            Step(sim, Tank(center), 1f);
            Assert.Greater(sim.GetPosition(near).x, 47.5f, "walking away");
            Step(sim, Tank(center), 7f);
            float d = math.distance(sim.GetPosition(near).xz, center.xz);
            Assert.That(d, Is.InRange(9f, 20f));
        }

        [Test]
        public void Acid_HitsTheHullBox_WithTheSpittersDamage()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(60, 60);
            var center = new float3(30f, 0.8f, 30f);
            using HordeFlowField field = FieldTo(grid, center);
            using HordeSimulation sim = Make(grid, field);
            sim.Spawn(new float3(30f, 0f, 48f), 0f, SPITTER, 1f, 2f);

            float first = 0f;
            bool fired = false;
            for (int i = 0; i < 160 && first == 0f; i++)
            {
                Step(sim, Tank(center), DT);
                fired |= Has(sim.ShotEvents, HordeShotEventType.Fired);
                first = sim.StepShotDamage;
            }

            Assert.IsTrue(fired, "a shot was fired");
            Assert.AreEqual(30f, first, 1e-3f, "15 × 2 into the tank's damage");
            Assert.AreEqual(first, sim.StepDamage, 1e-3f, "the shots go into the same damage as the bites");
        }

        [Test]
        public void Acid_MissesWhenTheTankMovesAway_AndEndsOnTheGround()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(60, 60);
            var center = new float3(30f, 0.8f, 30f);
            using HordeFlowField field = FieldTo(grid, center);
            using HordeSimulation sim = Make(grid, field);
            sim.Spawn(new float3(30f, 0f, 48f), 0f, SPITTER);

            bool fired = false;
            for (int i = 0; i < 160 && !fired; i++)
            {
                Step(sim, Tank(center), DT);
                fired = Has(sim.ShotEvents, HordeShotEventType.Fired);
            }

            Assert.IsTrue(fired);
            HordeTank moved = Tank(center + new float3(6f, 0f, 0f));
            bool ground = false;
            float damage = 0f;
            for (int i = 0; i < 50; i++)
            {
                Step(sim, moved, DT);
                damage += sim.StepShotDamage;
                ground |= Has(sim.ShotEvents, HordeShotEventType.HitGround);
            }

            Assert.AreEqual(0f, damage, "outside the hull box: no damage");
            Assert.IsTrue(ground, "the shot ended on the ground");
        }

        [Test]
        public void Spitter_DoesNotShootWithoutLineOfSight()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(60, 60);
            HordeTestGrids.Block(grid, new int2(0, 39), new int2(59, 40), 6f); // a wall across the whole grid
            var center = new float3(30f, 0.8f, 30f);
            using HordeFlowField field = FieldTo(grid, center);
            using HordeSimulation sim = Make(grid, field);
            sim.Spawn(new float3(30f, 0f, 48f), 0f, SPITTER);

            for (int i = 0; i < 100; i++)
            {
                Step(sim, Tank(center), DT);
                Assert.IsFalse(Has(sim.ShotEvents, HordeShotEventType.Fired), "no shot through the wall");
            }
        }

        [Test]
        public void Emerging_StandsForASecond_RisingOutOfTheGround_ThenWalks()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(60, 60);
            var center = new float3(40f, 0.8f, 40f);
            using HordeFlowField field = FieldTo(grid, center);
            using HordeSimulation sim = Make(grid, field);
            var at = new float3(10f, 0f, 10f);
            int m = sim.Spawn(at, 0f, CRAWLER, 1f, 1f, true);

            Step(sim, Tank(center), DT);
            Assert.Less(sim.Visible[0].PositionHeading.y, -0.5f, "drawn under the ground at first");
            Step(sim, Tank(center), 0.85f);
            Assert.AreEqual(HordeState.Emerging, sim.GetState(m));
            Assert.AreEqual(0f, math.distance(sim.GetPosition(m), at), 1e-4f, "does not move while emerging");

            Step(sim, Tank(center), 0.2f);
            Assert.AreEqual(HordeState.Walk, sim.GetState(m));
            Step(sim, Tank(center), 0.5f);
            Assert.Greater(math.distance(sim.GetPosition(m), at), 0.5f, "walks after it");
        }

        [Test]
        public void Emerging_CanBeCrushedAndHurt()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(40, 40);
            using var field = new HordeFlowField(grid);
            using HordeSimulation sim = Make(grid, field);
            var center = new float3(20f, 0.8f, 20f);
            int under = sim.Spawn(new float3(20.2f, 0f, 20.5f), 0f, CRAWLER, 1f, 1f, true);
            int hurt = sim.Spawn(new float3(5f, 0f, 5f), 0f, CRAWLER, 1f, 1f, true);
            sim.AddAreaDamage(new float3(5f, 0.5f, 5f), 2f, 100f);

            Step(sim, Tank(center, new float3(0f, 0f, 4f)), DT);

            Assert.AreEqual(2, sim.Deaths.Length);
            Assert.AreEqual(HordeState.Free, sim.GetState(under));
            Assert.AreEqual(HordeState.Free, sim.GetState(hurt));
        }

        [Test]
        public void Crush_StartsAtTheCrushSpeed()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(40, 40);
            using var field = new HordeFlowField(grid);
            var center = new float3(20f, 0.8f, 20f);
            float threshold = HordeSettings.Default.CrushSpeed;

            using HordeSimulation slow = Make(grid, field);
            slow.Spawn(new float3(20.2f, 0f, 20.5f), 0f, CRAWLER);
            Step(slow, Tank(center, new float3(0f, 0f, threshold - 0.01f)), DT);
            Assert.AreEqual(0, slow.Deaths.Length, "just under the threshold: pushed, not crushed");

            using HordeSimulation fast = Make(grid, field);
            fast.Spawn(new float3(20.2f, 0f, 20.5f), 0f, CRAWLER);
            Step(fast, Tank(center, new float3(threshold, 0f, 0f)), DT);
            Assert.AreEqual(1, fast.Deaths.Length, "at the threshold: crushed");
            Assert.AreEqual(HordeDeathCause.Crushed, fast.Deaths[0].Cause);
            Assert.AreEqual(CRAWLER, fast.Deaths[0].Kind);
        }

        [Test]
        public void Brute_HitsTheHullWithBlows()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(40, 40);
            using var field = new HordeFlowField(grid);
            using HordeSimulation sim = Make(grid, field);
            var center = new float3(20f, 0.8f, 20f);
            sim.Spawn(new float3(20f, 0f, 23.9f), 0f, BRUTE);

            Step(sim, Tank(center), DT);
            Assert.AreEqual(40f, sim.StepDamage, 1e-3f, "the first blow lands at contact");
            float total = sim.StepDamage;
            for (int i = 0; i < 25; i++)
            {
                Step(sim, Tank(center), DT);
                total += sim.StepDamage;
            }

            Assert.AreEqual(80f, total, 1e-3f, "then one every 1.2 s");
        }

        [Test]
        public void Brute_IsCrushedOnlyAtAQuarterOfItsHealth()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(40, 40);
            using var field = new HordeFlowField(grid);
            var center = new float3(20f, 0.8f, 20f);
            HordeTank fast = Tank(center, new float3(0f, 0f, 5f));

            using HordeSimulation healthy = Make(grid, field);
            int a = healthy.Spawn(new float3(20.2f, 0f, 20.5f), 0f, BRUTE);
            Assert.AreEqual(1800f, healthy.GetMaxHealth(a));
            Step(healthy, fast, DT);
            Assert.AreEqual(0, healthy.Deaths.Length, "the hull pushes it, it is not crushed");

            using HordeSimulation beaten = Make(grid, field);
            int b = beaten.Spawn(new float3(20.2f, 0f, 20.5f), 0f, BRUTE);
            beaten.Damage(b, beaten.Version(b), 1400f); // 400 left: under a quarter of 1800
            Step(beaten, fast, DT);
            Assert.AreEqual(1, beaten.Deaths.Length);
            Assert.AreEqual(HordeDeathCause.Crushed, beaten.Deaths[0].Cause);
            Assert.AreEqual(BRUTE, beaten.Deaths[0].Kind);
        }

        [Test]
        public void Visible_IsSortedByKind()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(40, 40);
            using var field = new HordeFlowField(grid);
            using HordeSimulation sim = Make(grid, field);
            sim.Spawn(new float3(5f, 0f, 5f), 0f, SPITTER);
            sim.Spawn(new float3(10f, 0f, 5f), 0f, CRAWLER);
            sim.Spawn(new float3(15f, 0f, 5f), 0f, CRAWLER);

            Step(sim, default, DT);

            sim.GetVisibleRange(CRAWLER, out int cs, out int cc);
            sim.GetVisibleRange(SPITTER, out int ss, out int sc);
            Assert.AreEqual(2, cc);
            Assert.AreEqual(1, sc);
            Assert.AreEqual(0, cs);
            Assert.AreEqual(2, ss);
            Assert.AreEqual(SPITTER, (int)sim.Visible[ss].Animation.w);
        }

        [Test]
        public void Damage_LandsInTheNextStep_AndTheVersionGoesStale()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(40, 40);
            using var field = new HordeFlowField(grid);
            using HordeSimulation sim = Make(grid, field);
            int m = sim.Spawn(new float3(5f, 0f, 5f), 0f, CRAWLER);
            int version = sim.Version(m);
            Assert.IsTrue(sim.IsTargetAlive(m, version));

            sim.Damage(m, version, 5f);
            Step(sim, default, DT);
            Assert.AreEqual(15f, sim.GetHealth(m), 1e-4f);

            sim.Damage(m, version, 100f);
            Assert.IsTrue(sim.IsTargetAlive(m, version), "alive until the step");
            Step(sim, default, DT);
            Assert.AreEqual(1, sim.Deaths.Length);
            Assert.AreEqual(HordeDeathCause.Damaged, sim.Deaths[0].Cause);
            Assert.IsFalse(sim.IsTargetAlive(m, version));

            int again = sim.Spawn(new float3(6f, 0f, 6f), 0f, CRAWLER);
            Assert.AreEqual(m, again, "the slot comes back");
            Assert.IsFalse(sim.IsTargetAlive(m, version), "with a new version");
            sim.Damage(m, version, 100f);
            sim.ClearDeaths();
            Step(sim, default, DT);
            Assert.AreEqual(0, sim.Deaths.Length, "a stale hit does nothing");
        }

        [Test]
        public void Clear_RemovesTheShotsInFlight()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(60, 60);
            var center = new float3(30f, 0.8f, 30f);
            using HordeFlowField field = FieldTo(grid, center);
            using HordeSimulation sim = Make(grid, field);
            sim.Spawn(new float3(30f, 0f, 48f), 0f, SPITTER);
            for (int i = 0; i < 160 && sim.ProjectileCount == 0; i++)
                Step(sim, Tank(center), DT);
            Assert.Greater(sim.ProjectileCount, 0);

            sim.Clear();
            Assert.AreEqual(0, sim.ProjectileCount);
            Assert.AreEqual(0, sim.AliveCount);
            Step(sim, Tank(center), DT);
            Assert.AreEqual(0, sim.ProjectileCount);
        }

        [Test]
        public void Step_WithKindsShotsAndHits_AllocatesNoManagedMemory()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(60, 60);
            var center = new float3(30f, 0.8f, 30f);
            using HordeFlowField field = FieldTo(grid, center);
            using HordeSimulation sim = Make(grid, field, 512);
            var random = new Random(3);
            for (int i = 0; i < 400; i++)
                sim.Spawn(new float3(random.NextFloat(1f, 59f), 0f, random.NextFloat(1f, 59f)), 0f, i % 10 == 0 ? SPITTER : CRAWLER,
                    1f, 1f, i % 3 == 0);
            HordeTank tank = Tank(center, new float3(0f, 0f, 3f));
            Step(sim, tank, 3f); // warm-up: Burst, job reflection data, first shots

            Assert.That(() =>
            {
                for (int i = 0; i < 3; i++)
                {
                    sim.Damage(i, sim.Version(i), 1f);
                    sim.AddAreaDamage(new float3(5f, 0.8f, 5f), 2f, 10f, 0.25f);
                    sim.Schedule(DT, tank);
                    sim.Complete();
                    sim.ClearDeaths();
                }
            }, NoAllocation.Constraint);
        }

        private static bool Has(NativeArray<HordeShotEvent> events, HordeShotEventType type)
        {
            for (int i = 0; i < events.Length; i++)
                if (events[i].Type == type)
                    return true;
            return false;
        }
    }
}
