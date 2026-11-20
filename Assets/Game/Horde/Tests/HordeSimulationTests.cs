using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Horde.Tests
{
    public class HordeSimulationTests
    {
        private const float DT = 0.05f;

        private static HordeTank Tank(float3 center, float3 halfExtents, float3 velocity) => new()
        {
            Present = true,
            Center = center,
            Rotation = quaternion.identity,
            HalfExtents = halfExtents,
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
        public void Tear_KeepsTheWayTheShellFlew_ForThatDeathOnly()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 20);
            using var field = new HordeFlowField(grid);
            using var sim = new HordeSimulation(8, grid, field, HordeSettings.Default);
            int torn = sim.Spawn(new float3(5f, 0f, 5f), 0f);
            int shot = sim.Spawn(new float3(15f, 0f, 15f), 0f);
            Step(sim, default);

            var way = new float3(0.6f, -0.2f, 0.77f);
            sim.Tear(torn, sim.Version(torn), way);
            sim.Damage(torn, sim.Version(torn), 1e6f);
            sim.Damage(shot, sim.Version(shot), 1e6f);
            Step(sim, default);

            Assert.AreEqual(2, sim.Deaths.Length);
            Assert.IsTrue(sim.IsTorn(torn));
            Assert.AreEqual(way, sim.TornDirection(torn), "its goo and pieces go the shell's way");
            Assert.IsFalse(sim.IsTorn(shot));
            Assert.AreEqual(float3.zero, sim.TornDirection(shot), "a plain death has no way: its goo goes up");
        }

        [Test]
        public void Tear_KeepsTheShellsPower_ForThatDeathOnly()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 20);
            using var field = new HordeFlowField(grid);
            using var sim = new HordeSimulation(8, grid, field, HordeSettings.Default);
            int strong = sim.Spawn(new float3(5f, 0f, 5f), 0f);
            int plain = sim.Spawn(new float3(15f, 0f, 15f), 0f);
            Step(sim, default);

            sim.Tear(strong, sim.Version(strong), new float3(0f, 0f, 1f), 2.5f);
            sim.Tear(plain, sim.Version(plain), new float3(0f, 0f, 1f));
            sim.Damage(strong, sim.Version(strong), 1e6f);
            sim.Damage(plain, sim.Version(plain), 1e6f);
            Step(sim, default);

            Assert.AreEqual(2.5f, sim.TornPower(strong), "«Мощный фугас» 5: its goo and pieces fly ×2.5");
            Assert.AreEqual(1f, sim.TornPower(plain), "a tear without power: as before");

            int fresh = sim.Spawn(new float3(5f, 0f, 5f), 0f);
            Assert.AreEqual(1f, sim.TornPower(fresh), "a new monster in the slot starts whole");
        }

        [Test]
        public void Crawlers_AShareOfThemCrawl_AtTheCrawlClipsSpeed_OtherKindsNever()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(60, 60);
            using var field = new HordeFlowField(grid);
            HordeSettings settings = HordeSettings.Default;
            settings.SpeedJitter = 0f;
            settings.RunClipSpeed = 6f;
            settings.CrawlClipSpeed = 2f;
            settings.CrawlShare = 1f;
            settings.AttackDps = 0f;
            using var sim = new HordeSimulation(8, grid, field, settings);
            int crawler = sim.Spawn(new float3(30f, 0f, 55f), 0f);
            int spitter = sim.Spawn(new float3(5f, 0f, 30f), 0f, 1);
            Assert.IsTrue(sim.IsCrawling(crawler), "share 1: every crawler crawls");
            Assert.IsFalse(sim.IsCrawling(spitter), "the other kinds walk upright");

            settings.CrawlShare = 0f;
            sim.Settings = settings;
            int runner = sim.Spawn(new float3(55f, 0f, 30f), 0f);
            Assert.IsFalse(sim.IsCrawling(runner), "share 0: none crawl");

            var center = new float3(30f, 0.8f, 30f);
            var tank = new HordeTank { Present = true, Center = center, Rotation = quaternion.identity, HalfExtents = new float3(1.5f, 1f, 3f) };
            Step(sim, tank, 30); // 1.5 s
            float crawled = 55f - sim.GetPosition(crawler).z;
            float ran = 55f - sim.GetPosition(runner).x;
            Assert.That(crawled / ran, Is.EqualTo(2f / 6f).Within(0.08f), "a crawler goes at the crawl clip's speed against the run's");
        }

        [Test]
        public void CrawlShare_PicksAboutThatShare()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(40, 40);
            using var field = new HordeFlowField(grid);
            HordeSettings settings = HordeSettings.Default;
            settings.CrawlShare = 0.3f;
            using var sim = new HordeSimulation(1000, grid, field, settings);
            int crawling = 0;
            for (int i = 0; i < 1000; i++)
                if (sim.IsCrawling(sim.Spawn(new float3(5f + i % 30, 0f, 5f + i / 30 % 30), 0f)))
                    crawling++;
            Assert.That(crawling, Is.InRange(240, 360), "about 30 %");
        }

        [Test]
        public void Separation_PushesOverlappingMonstersApart()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 20);
            using var field = new HordeFlowField(grid);
            using var sim = new HordeSimulation(16, grid, field, HordeSettings.Default);
            int a = sim.Spawn(new float3(10f, 0f, 10f), 0f);
            int b = sim.Spawn(new float3(10.1f, 0f, 10f), 0f);
            int c = sim.Spawn(new float3(10f, 0f, 10f), 0f);

            Step(sim, default, 40);

            float ab = math.distance(sim.GetPosition(a), sim.GetPosition(b));
            float ac = math.distance(sim.GetPosition(a), sim.GetPosition(c));
            float bc = math.distance(sim.GetPosition(b), sim.GetPosition(c));
            Assert.Greater(math.min(ab, math.min(ac, bc)), 0.6f, "every pair ends at least 0.6 m apart");
            Assert.Less(math.max(ab, math.max(ac, bc)), 3f, "pushed apart, not scattered");
        }

        [Test]
        public void Monsters_FollowTheFlowFieldAroundAWall_AndNeverStepIntoIt()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 20);
            HordeTestGrids.Block(grid, new int2(10, 0), new int2(10, 14));
            using var field = new HordeFlowField(grid);
            float3 target = HordeTestGrids.Center(new int2(15, 2));
            field.Schedule(target);
            field.Complete();

            HordeSettings settings = HordeSettings.Default;
            settings.AttackDps = 0f;
            using var sim = new HordeSimulation(8, grid, field, settings);
            int m = sim.Spawn(HordeTestGrids.Center(new int2(5, 2)), 0f);
            HordeTank tank = Tank(target + new float3(0f, 0.5f, 0f), new float3(0.3f, 0.5f, 0.3f), float3.zero);

            float maxZ = 0f;
            for (int i = 0; i < 400; i++)
            {
                Step(sim, tank);
                float3 p = sim.GetPosition(m);
                Assert.IsTrue(grid.TryCell(p, out int2 cell), "stays on the grid");
                Assert.AreEqual(0f, grid.HeightOf(cell), "never stands on the wall (step " + i + ", " + p + ")");
                maxZ = math.max(maxZ, p.z);
            }

            float3 end = sim.GetPosition(m);
            Assert.Less(math.distance(end.xz, target.xz), 1.5f, "reaches the tank");
            Assert.Greater(maxZ, 14f, "went round the end of the wall");
        }

        [Test]
        public void Crush_KillsOnlyInsideTheHull_AndOnlyAboveTheCrushSpeed()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 20);
            using var field = new HordeFlowField(grid);
            using var sim = new HordeSimulation(8, grid, field, HordeSettings.Default);
            var center = new float3(10f, 0.8f, 10f);
            var half = new float3(1.5f, 0.8f, 3f);

            int under = sim.Spawn(new float3(10f, 0f, 10.5f), 0f);
            int outside = sim.Spawn(new float3(10f, 0f, 16f), 0f);

            // Slow tank: the one under the hull is pushed out and claws, nobody dies.
            Step(sim, Tank(center, half, new float3(0f, 0f, 0.5f)));
            Assert.AreEqual(0, sim.Deaths.Length, "a slow tank crushes nobody");
            Assert.AreEqual(HordeState.Attack, sim.GetState(under), "the one at the hull attacks");
            Assert.Greater(sim.StepDamage, 0f, "and its damage is summed");
            Assert.AreEqual(HordeState.Walk, sim.GetState(outside));

            // Fast tank over a monster under the hull.
            int victim = sim.Spawn(new float3(10.2f, 0f, 9f), 0f);
            Step(sim, Tank(center, half, new float3(0f, 0f, 6f)));
            Assert.AreEqual(0, sim.Deaths.Length, "run into, it is knocked down first: no pop yet");
            Assert.AreEqual(HordeState.Knocked, sim.GetState(victim));
            Step(sim, Tank(center, half, new float3(0f, 0f, 6f))); // the hull over it: popped
            Assert.AreEqual(1, sim.Deaths.Length, "exactly one crushed");
            Assert.AreEqual(victim, sim.Deaths[0].Index);
            Assert.AreEqual(HordeDeathCause.Crushed, sim.Deaths[0].Cause);
            Assert.AreEqual(HordeState.Free, sim.GetState(victim), "its slot is free");
            Assert.AreEqual(HordeState.Walk, sim.GetState(outside), "the one in the open lives");
            Assert.AreEqual(2, sim.AliveCount);
        }

        /// <summary>Drives a hull box along +Z at <paramref name="speed"/> for <paramref name="steps"/> steps of 1/60 s.</summary>
        private static void Drive(HordeSimulation sim, ref float3 center, float3 half, float speed, int steps)
        {
            const float dt = 1f / 60f;
            for (int i = 0; i < steps; i++)
            {
                sim.Schedule(dt, Tank(center, half, new float3(0f, 0f, speed)));
                sim.Complete();
                center.z += speed * dt;
            }
        }

        [Test]
        public void Knocked_FallsWhereItIs_DoesNotWalkOrBite_AndThenDiesThereAsACorpse()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(30, 30);
            using var field = new HordeFlowField(grid);
            HordeSettings settings = HordeSettings.Default;
            settings.KnockLie = 1f;
            using var sim = new HordeSimulation(8, grid, field, settings);
            var center = new float3(15f, 0.8f, 10f);
            var half = new float3(1.5f, 0.8f, 3f);
            int victim = sim.Spawn(new float3(15f, 0f, 13.4f), 0f); // just ahead of the front face

            // The tank drives into it for one step, then stops a little short of it.
            Step(sim, Tank(center, half, new float3(0f, 0f, 6f)));
            Assert.AreEqual(HordeState.Knocked, sim.GetState(victim), "run into: knocked down");
            Assert.AreEqual(0f, sim.GetAnimTime(victim), 0.06f, "its fall plays from the start");
            float3 lying = sim.GetPosition(victim);

            var away = new float3(15f, 0.8f, 6f);
            Step(sim, Tank(away, half, float3.zero), 10);
            Assert.AreEqual(HordeState.Knocked, sim.GetState(victim));
            Assert.Less(math.distance(lying, sim.GetPosition(victim)), 0.01f, "down: it does not walk");
            Assert.AreEqual(0f, sim.StepDamage, "nor bite");

            Step(sim, Tank(away, half, float3.zero), 12); // past KnockLie
            Assert.AreEqual(HordeState.Free, sim.GetState(victim));
            Assert.AreEqual(0, sim.AliveCount, "it died where it lay");
        }

        [Test]
        public void Knocked_ShotWhileDown_DiesOfTheShot()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(30, 30);
            using var field = new HordeFlowField(grid);
            using var sim = new HordeSimulation(8, grid, field, HordeSettings.Default);
            var center = new float3(15f, 0.8f, 10f);
            var half = new float3(1.5f, 0.8f, 3f);
            int victim = sim.Spawn(new float3(15f, 0f, 13.4f), 0f);
            Step(sim, Tank(center, half, new float3(0f, 0f, 6f)));
            Assert.AreEqual(HordeState.Knocked, sim.GetState(victim));

            sim.Damage(victim, sim.Version(victim), 1000f);
            Step(sim, Tank(new float3(15f, 0.8f, 6f), half, float3.zero));
            Assert.AreEqual(1, sim.Deaths.Length);
            Assert.AreEqual(HordeDeathCause.Damaged, sim.Deaths[0].Cause, "a shot is a shot (a shell tears it as ever)");
        }

        [Test]
        public void Crush_ATankAtNormalSpeed_CrushesWhatStandsInItsPath_AtAHighFrameRate()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 30);
            using var field = new HordeFlowField(grid);
            HordeSettings settings = HordeSettings.Default;
            settings.Speed = 0f; // it stands: only the hull moves
            using var sim = new HordeSimulation(8, grid, field, settings);
            var center = new float3(10f, 0.8f, 6f);
            var half = new float3(1.5f, 0.8f, 3f);
            int victim = sim.Spawn(new float3(10.3f, 0f, 13f), 0f); // 4 m ahead of the front face

            Drive(sim, ref center, half, 6f, 60);

            Assert.AreEqual(HordeState.Free, sim.GetState(victim), "crushed, not shoved ahead of the hull");
            Assert.AreEqual(1, sim.Deaths.Length);
            Assert.AreEqual(HordeDeathCause.Crushed, sim.Deaths[0].Cause);
        }

        [Test]
        public void Crush_ASlowTank_PushesWhatStandsInItsPath()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 30);
            using var field = new HordeFlowField(grid);
            HordeSettings settings = HordeSettings.Default;
            settings.Speed = 0f;
            using var sim = new HordeSimulation(8, grid, field, settings);
            var center = new float3(10f, 0.8f, 6f);
            var half = new float3(1.5f, 0.8f, 3f);
            int victim = sim.Spawn(new float3(10.3f, 0f, 9.5f), 0f); // 0.5 m ahead of the front face

            Drive(sim, ref center, half, 1f, 120);

            Assert.AreEqual(0, sim.Deaths.Length, "below the crush speed nobody dies");
            Assert.AreEqual(HordeState.Attack, sim.GetState(victim), "it claws the hull");
            float3 p = sim.GetPosition(victim);
            Assert.Greater(p.z, center.z + half.z, "pushed ahead of the front face, not run over");
        }

        [Test]
        public void Crush_TheHullDoesNotCrushBackwards()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 30);
            using var field = new HordeFlowField(grid);
            HordeSettings settings = HordeSettings.Default;
            settings.Speed = 0f;
            using var sim = new HordeSimulation(8, grid, field, settings);
            var center = new float3(10f, 0.8f, 15f);
            var half = new float3(1.5f, 0.8f, 3f);
            int behind = sim.Spawn(new float3(10f, 0f, 11.5f), 0f); // 0.5 m behind the rear face

            Drive(sim, ref center, half, 6f, 10);

            Assert.AreEqual(0, sim.Deaths.Length);
            Assert.AreNotEqual(HordeState.Free, sim.GetState(behind));
        }

        [Test]
        public void PushOut_DoesNotPutAMonsterOnARoof()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 20);
            // A house right of the hull: the shallow way out (along X) leads onto it.
            HordeTestGrids.Block(grid, new int2(12, 0), new int2(19, 19), 3f);
            using var field = new HordeFlowField(grid);
            HordeSettings settings = HordeSettings.Default;
            settings.Speed = 0f;
            using var sim = new HordeSimulation(8, grid, field, settings);
            var center = new float3(10.5f, 0.8f, 10f);
            var half = new float3(1.2f, 0.8f, 3f);
            int m = sim.Spawn(new float3(11.5f, 0f, 11f), 0f); // under the hull, near its right side

            Step(sim, Tank(center, half, float3.zero));

            float3 p = sim.GetPosition(m);
            Assert.IsTrue(grid.TryCell(p, out int2 cell));
            Assert.AreEqual(0f, grid.HeightOf(cell), "stays on the street (" + p + ")");
            Assert.AreEqual(HordeState.Attack, sim.GetState(m));
        }

        [Test]
        public void Separation_ReachesANeighbourTwoMetreCellsAway()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 20);
            using var field = new HordeFlowField(grid);
            using var sim = new HordeSimulation(8, grid, field, HordeSettings.Default);
            // 1.31 m apart (inside the 1.4 m separation radius), on either side of a whole 1 m cell.
            int a = sim.Spawn(new float3(8.99f, 0f, 10f), 0f);
            int b = sim.Spawn(new float3(10.3f, 0f, 10f), 0f);

            Step(sim, default);

            Assert.Less(sim.GetPosition(a).x, 8.99f, "pushed away from b");
            Assert.Greater(sim.GetPosition(b).x, 10.3f, "pushed away from a");
        }

        [Test]
        public void AreaDamage_KillsInsideTheRadius_Only()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 20);
            using var field = new HordeFlowField(grid);
            using var sim = new HordeSimulation(8, grid, field, HordeSettings.Default);
            int near = sim.Spawn(new float3(12f, 0f, 10f), 0f);
            int far = sim.Spawn(new float3(18f, 0f, 10f), 0f);

            sim.AddAreaDamage(new float3(10f, 0.8f, 10f), 5f, 1000f);
            Step(sim, default);

            Assert.AreEqual(1, sim.Deaths.Length);
            Assert.AreEqual(near, sim.Deaths[0].Index);
            Assert.AreEqual(HordeDeathCause.Explosion, sim.Deaths[0].Cause);
            Assert.AreEqual(HordeState.Walk, sim.GetState(far));
            Assert.AreEqual(HordeSettings.Default.Health, sim.GetHealth(far));

            sim.ClearDeaths();
            Step(sim, default);
            Assert.AreEqual(0, sim.Deaths.Length, "a blast hits once");
        }

        [Test]
        public void FreeList_ReusesTheSlotsOfTheDead()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(10, 10);
            using var field = new HordeFlowField(grid);
            using var sim = new HordeSimulation(3, grid, field, HordeSettings.Default);
            Assert.AreEqual(0, sim.Spawn(new float3(1f, 0f, 1f), 0f));
            Assert.AreEqual(1, sim.Spawn(new float3(3f, 0f, 1f), 0f));
            Assert.AreEqual(2, sim.Spawn(new float3(5f, 0f, 1f), 0f));
            Assert.AreEqual(-1, sim.Spawn(new float3(7f, 0f, 1f), 0f), "full");

            Assert.IsTrue(sim.Kill(1));
            Assert.IsFalse(sim.Kill(1), "a free slot cannot die again");
            Assert.AreEqual(2, sim.AliveCount);
            Assert.AreEqual(1, sim.Deaths.Length);
            Assert.AreEqual(HordeDeathCause.Killed, sim.Deaths[0].Cause);

            Assert.AreEqual(1, sim.Spawn(new float3(7f, 0f, 1f), 0f), "the freed slot comes back");
            Assert.AreEqual(3, sim.AliveCount);
        }

        [Test]
        public void Hit_OnASlotReusedBeforeTheStep_DoesNotLandOnTheNewMonster()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(10, 10);
            using var field = new HordeFlowField(grid);
            using var sim = new HordeSimulation(4, grid, field, HordeSettings.Default);
            int old = sim.Spawn(new float3(2f, 0f, 2f), 0f);
            int oldVersion = sim.Version(old);
            sim.Damage(old, oldVersion, 30f);

            Assert.IsTrue(sim.Kill(old));
            int fresh = sim.Spawn(new float3(5f, 0f, 5f), 0f);
            Assert.AreEqual(old, fresh, "the free list hands the same slot out again");

            Step(sim, default);
            Assert.AreEqual(HordeSettings.Default.Health, sim.GetHealth(fresh), "the old monster's hit is dropped");

            sim.Damage(fresh, sim.Version(fresh), 30f);
            Step(sim, default);
            Assert.AreEqual(HordeSettings.Default.Health - 30f, sim.GetHealth(fresh), 1e-4f, "its own hit lands");
        }

        [Test]
        public void AliveOrUnreported_KeepsTheDeadUntilTheirDeathsAreTaken()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(10, 10);
            using var field = new HordeFlowField(grid);
            using var sim = new HordeSimulation(4, grid, field, HordeSettings.Default);
            int m = sim.Spawn(new float3(2f, 0f, 2f), 0f);
            sim.Damage(m, sim.Version(m), 1000f);
            Step(sim, default);

            Assert.AreEqual(0, sim.AliveCount, "the slot is free after Complete");
            Assert.AreEqual(1, sim.AliveOrUnreported, "but its death is not handed out yet");
            sim.ClearDeaths();
            Assert.AreEqual(0, sim.AliveOrUnreported);
        }

        [Test]
        public void VersionBase_AHandleOfTheReplacedSimulation_NeverMatchesTheNewOne()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(10, 10);
            using var field = new HordeFlowField(grid);
            int slot, version, next;
            using (var first = new HordeSimulation(4, grid, field, HordeSettings.Default))
            {
                slot = first.Spawn(new float3(2f, 0f, 2f), 0f);
                version = first.Version(slot);
                first.Clear();
                next = first.MaxVersion() + 1;
            }

            using var second = new HordeSimulation(4, grid, field, HordeSettings.Default, 1, next);
            Assert.AreEqual(slot, second.Spawn(new float3(2f, 0f, 2f), 0f));
            Assert.IsFalse(second.IsTargetAlive(slot, version), "the old handle is stale");
            Assert.IsTrue(second.IsTargetAlive(slot, second.Version(slot)));
            Assert.Greater(second.Version(slot), version);
        }

        [Test]
        public void AreaDamage_HasABudgetOfItsOwn_ShovesDoNotCrowdItOut()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 20);
            using var field = new HordeFlowField(grid);
            using var sim = new HordeSimulation(4, grid, field, HordeSettings.Default);
            int m = sim.Spawn(new float3(10f, 0f, 10f), 0f);
            for (int i = 0; i < 100; i++)
                sim.AddKnockback(new float3(0f, 0f, 0f), 1f, 1f); // far away: shoves nobody
            Assert.Greater(sim.DroppedAreas, 0, "the shoves past their budget are counted");
            int dropped = sim.DroppedAreas;

            sim.AddAreaDamage(new float3(10f, 0.8f, 10f), 3f, 1000f);
            Assert.AreEqual(dropped, sim.DroppedAreas, "the blast is not dropped");
            Step(sim, default);
            Assert.AreEqual(1, sim.Deaths.Length);
            Assert.AreEqual(m, sim.Deaths[0].Index);

            sim.ClearDeaths();
            for (int i = 0; i < 70; i++)
                sim.AddAreaDamage(new float3(0f, 0f, 0f), 1f, 1f);
            Assert.AreEqual(dropped + 6, sim.DroppedAreas, "64 blasts a step, the rest counted");
            Step(sim, default);
            sim.AddAreaDamage(new float3(0f, 0f, 0f), 1f, 1f);
            Assert.AreEqual(dropped + 6, sim.DroppedAreas, "the budget is per step");
        }

        [Test]
        public void RenderPrep_CullsByTheFrustum()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 20);
            using var field = new HordeFlowField(grid);
            using var sim = new HordeSimulation(8, grid, field, HordeSettings.Default);
            sim.Spawn(new float3(5f, 0f, 5f), 0f);
            sim.Spawn(new float3(15f, 0f, 5f), 0f);

            Step(sim, default);
            Assert.AreEqual(2, sim.Visible.Length, "no planes: everything is drawn");

            // Only x < 10 is inside.
            var planes = new UnityEngine.Plane[6];
            for (int i = 0; i < 6; i++)
                planes[i] = new UnityEngine.Plane(UnityEngine.Vector3.up, 100f);
            planes[0] = new UnityEngine.Plane(UnityEngine.Vector3.left, 10f);
            sim.SetCullingPlanes(planes);
            Step(sim, default);
            Assert.AreEqual(1, sim.Visible.Length);
            Assert.Less(sim.Visible[0].PositionHeading.x, 10f);
        }

        [Test]
        public void Step_AllocatesNoManagedMemory()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(40, 40);
            using var field = new HordeFlowField(grid);
            field.Schedule(new float3(20f, 0f, 20f));
            field.Complete();
            using var sim = new HordeSimulation(1024, grid, field, HordeSettings.Default);
            var random = new Random(7);
            for (int i = 0; i < 1000; i++)
                sim.Spawn(new float3(random.NextFloat(1f, 39f), 0f, random.NextFloat(1f, 39f)), 0f);
            HordeTank tank = Tank(new float3(20f, 0.8f, 20f), new float3(1.5f, 0.8f, 3f), new float3(0f, 0f, 3f));
            Step(sim, tank, 5); // warm-up: Burst, job reflection data

            Assert.That(() =>
            {
                for (int i = 0; i < 3; i++)
                {
                    sim.AddAreaDamage(new float3(5f, 0.8f, 5f), 2f, 10f);
                    sim.Schedule(DT, tank);
                    sim.Complete();
                    sim.ClearDeaths();
                }
            }, NoAllocation.Constraint);
        }
    }
}
