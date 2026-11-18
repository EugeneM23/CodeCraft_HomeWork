using Game.Arenas;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.Arenas
{
    /// <summary>The arena plan: same seed, same arena; houses, burrows and the gate where the rules put them.</summary>
    public sealed class ArenaPlannerTests
    {
        private const int SEEDS = 30;
        private const float HALF = ArenaPlan.SIZE * 0.5f;

        private ArenaDefinition _definition;

        [TearDown]
        public void TearDown()
        {
            if (_definition != null)
                Object.DestroyImmediate(_definition);
            _definition = null;
        }

        [Test]
        public void SameSeed_GivesSamePlan()
        {
            ArenaDefinition definition = Definition(ArenaLayout.Scattered, new Vector2Int(2, 3));
            for (int seed = 1; seed <= SEEDS; seed++)
            {
                ArenaPlan a = ArenaPlanner.Plan(definition, seed);
                ArenaPlan b = ArenaPlanner.Plan(definition, seed);

                Assert.That(b.Centre, Is.EqualTo(a.Centre), $"seed {seed}");
                Assert.That(b.Gate.Position, Is.EqualTo(a.Gate.Position), $"seed {seed}");
                Assert.That(b.Entry, Is.EqualTo(a.Entry), $"seed {seed}");
                Assert.That(b.Houses.Count, Is.EqualTo(a.Houses.Count), $"seed {seed}");
                for (int i = 0; i < a.Houses.Count; i++)
                {
                    Assert.That(b.Houses[i].Position, Is.EqualTo(a.Houses[i].Position), $"seed {seed}");
                    Assert.That(b.Houses[i].Shape, Is.EqualTo(a.Houses[i].Shape), $"seed {seed}");
                    Assert.That(b.Houses[i].Seed, Is.EqualTo(a.Houses[i].Seed), $"seed {seed}");
                }
                Assert.That(b.Trees.Count, Is.EqualTo(a.Trees.Count), $"seed {seed}");
                for (int i = 0; i < a.Trees.Count; i++)
                    Assert.That(b.Trees[i].Position, Is.EqualTo(a.Trees[i].Position), $"seed {seed}");
                Assert.That(b.Burrows, Is.EqualTo(a.Burrows), $"seed {seed}");
                Assert.That(b.Fences.Count, Is.EqualTo(a.Fences.Count), $"seed {seed}");
            }
        }

        [Test]
        public void OtherSeed_GivesOtherPlan()
        {
            ArenaDefinition definition = Definition(ArenaLayout.Scattered, new Vector2Int(1, 2));
            ArenaPlan first = ArenaPlanner.Plan(definition, 1);
            for (int seed = 2; seed <= SEEDS; seed++)
            {
                ArenaPlan other = ArenaPlanner.Plan(definition, seed);
                bool differs = other.Centre != first.Centre || other.Houses[0].Position != first.Houses[0].Position;
                Assert.That(differs, Is.True, $"seed {seed} gave the same arena as seed 1");
            }
        }

        [Test]
        public void Houses_InRange_InsideSquare_AndApart()
        {
            ArenaDefinition definition = Definition(ArenaLayout.Scattered, new Vector2Int(2, 3));
            for (int seed = 1; seed <= SEEDS; seed++)
            {
                ArenaPlan plan = ArenaPlanner.Plan(definition, seed);
                Assert.That(plan.Houses.Count, Is.InRange(2, 3), $"seed {seed}");

                Rect square = ArenaPlanner.Inset(ArenaPlanner.Square(plan), 3f - 1e-3f);
                for (int i = 0; i < plan.Houses.Count; i++)
                {
                    Rect footprint = plan.Houses[i].Footprint;
                    Assert.That(square.Contains(footprint.min) && square.Contains(footprint.max), Is.True,
                        $"seed {seed}: house {i} is nearer the edge than 3 m");
                    for (int j = i + 1; j < plan.Houses.Count; j++)
                        Assert.That(ArenaPlanner.RectDistance(footprint, plan.Houses[j].Footprint), Is.GreaterThanOrEqualTo(4f - 1e-3f),
                            $"seed {seed}: houses {i} and {j}");
                }
            }
        }

        [Test]
        public void HouseBlocksGate_FirstHouseStandsOnTheWay()
        {
            ArenaDefinition definition = Definition(ArenaLayout.HouseBlocksGate, new Vector2Int(1, 2));
            for (int seed = 1; seed <= SEEDS; seed++)
            {
                ArenaPlan plan = ArenaPlanner.Plan(definition, seed);
                Assert.That(plan.Houses.Count, Is.GreaterThanOrEqualTo(1), $"seed {seed}");
                float distance = ArenaPlanner.SegmentRectDistance(ArenaPlanner.Flat(plan.Entry),
                    ArenaPlanner.Flat(plan.Gate.Position), plan.Houses[0].Footprint);
                Assert.That(distance, Is.EqualTo(0f), $"seed {seed}: the house is off the way to the gate");
            }
        }

        [Test]
        public void CoverForSpitters_FirstHouseInNorthHalf_WithCover()
        {
            ArenaDefinition definition = Definition(ArenaLayout.CoverForSpitters, new Vector2Int(2, 2));
            for (int seed = 1; seed <= SEEDS; seed++)
            {
                ArenaPlan plan = ArenaPlanner.Plan(definition, seed);
                Assert.That(plan.Houses.Count, Is.GreaterThanOrEqualTo(1), $"seed {seed}");
                Assert.That(plan.Houses[0].Position.z, Is.GreaterThan(plan.Centre.z), $"seed {seed}");
                Assert.That(plan.Cover, Is.Not.Empty, $"seed {seed}");
            }
        }

        [Test]
        public void Burrows_RoundTheEdge_AwayFromTheGate()
        {
            ArenaDefinition definition = Definition(ArenaLayout.Scattered, new Vector2Int(1, 3));
            for (int seed = 1; seed <= SEEDS; seed++)
            {
                ArenaPlan plan = ArenaPlanner.Plan(definition, seed);
                Assert.That(plan.Burrows.Count, Is.InRange(4, definition.Burrows), $"seed {seed}");

                Vector2 gate = ArenaPlanner.Flat(plan.Gate.Position);
                foreach (Vector3 burrow in plan.Burrows)
                {
                    float dx = Mathf.Abs(burrow.x - plan.Centre.x);
                    float dz = Mathf.Abs(burrow.z - plan.Centre.z);
                    Assert.That(Mathf.Max(dx, dz), Is.InRange(HALF - 2f, HALF), $"seed {seed}: burrow {burrow} is off the edge");
                    Assert.That(Vector2.Distance(ArenaPlanner.Flat(burrow), gate), Is.GreaterThanOrEqualTo(6f - 1e-3f),
                        $"seed {seed}: burrow {burrow} by the gate");
                }
            }
        }

        [Test]
        public void Gate_OnTheNorthEdge_EntryInTheSouth()
        {
            ArenaDefinition definition = Definition(ArenaLayout.Scattered, new Vector2Int(1, 2));
            for (int seed = 1; seed <= SEEDS; seed++)
            {
                ArenaPlan plan = ArenaPlanner.Plan(definition, seed);
                Assert.That(plan.Gate.Position.z, Is.EqualTo(plan.Centre.z + HALF).Within(1e-3f), $"seed {seed}");
                Assert.That(plan.Gate.Exit.z, Is.GreaterThan(plan.Gate.Position.z), $"seed {seed}");
                Assert.That(plan.Entry.z, Is.LessThan(plan.Centre.z - HALF * 0.5f), $"seed {seed}");
                Assert.That(plan.Entry.z, Is.GreaterThan(plan.Centre.z - HALF), $"seed {seed}");
            }
        }

        [Test]
        public void Points_StandOnTheGround()
        {
            ArenaDefinition definition = Definition(ArenaLayout.Scattered, new Vector2Int(1, 3));
            for (int seed = 1; seed <= SEEDS; seed++)
            {
                ArenaPlan plan = ArenaPlanner.Plan(definition, seed);
                AssertOnGround(plan, plan.Entry, seed);
                foreach (Vector3 burrow in plan.Burrows)
                    AssertOnGround(plan, burrow, seed);
                foreach (TreePlan tree in plan.Trees)
                    AssertOnGround(plan, tree.Position, seed);
            }
        }

        private static void AssertOnGround(ArenaPlan plan, Vector3 point, int seed) =>
            Assert.That(point.y, Is.EqualTo(plan.HeightAt(point.x, point.z)).Within(1e-3f), $"seed {seed}: {point}");

        private ArenaDefinition Definition(ArenaLayout layout, Vector2Int houses)
        {
            _definition = ScriptableObject.CreateInstance<ArenaDefinition>();
            _definition.Configure(new ArenaId("test"), 3, layout, houses, new Vector2Int(3, 6), new Vector2Int(1, 2),
                relief: 0.8f, mud: 0.3f, burrows: 10, ArenaHint.None, elite: false);
            return _definition;
        }
    }
}
