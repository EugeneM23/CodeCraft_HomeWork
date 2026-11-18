using Game.Horde;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.AIKit.Adapters.Tests
{
    /// <summary>The horde as combat's target source: nearest, ray, versions, area damage, no allocations.</summary>
    public sealed class HordeTargetsTests
    {
        private HordeNavGrid _grid;
        private HordeFlowField _field;
        private HordeSimulation _sim;
        private HordeTargets _source;

        [SetUp]
        public void SetUp()
        {
            _grid = new HordeNavGrid(HordeNavGridSettings.Cover(float2.zero, new float2(40f, 40f), 1f));
            using (var heights = new NativeArray<float>(_grid.CellCount, Allocator.Temp))
                _grid.SetHeights(heights);
            _field = new HordeFlowField(_grid);
            HordeSettings settings = HordeSettings.Default;
            settings.Speed = 0f; // they stand: the tests place them
            _sim = new HordeSimulation(64, _grid, _field, settings);
            HordeKind crawler = HordeKind.Crawler;
            crawler.Speed = 0f;
            _sim.SetKind(HordeKinds.CRAWLER, crawler);
            _source = new HordeTargets(_sim);
        }

        [TearDown]
        public void TearDown()
        {
            _sim.Dispose();
            _field.Dispose();
            _grid.Dispose();
        }

        private int Spawn(float x, float z) => _sim.Spawn(new float3(x, 0f, z), 0f, HordeKinds.CRAWLER);

        private void Step()
        {
            _sim.Schedule(0.02f, default);
            _sim.Complete();
        }

        [Test]
        public void Nearest_FindsTheClosestLiveOne_WithinRange()
        {
            Spawn(10f, 10f);
            int near = Spawn(14f, 10f);
            Spawn(30f, 30f);

            Assert.IsTrue(_source.TryFindNearest(new Vector3(16f, 0.5f, 10f), 10f, out int index, out int version, out Vector3 at));
            Assert.AreEqual(near, index);
            Assert.IsTrue(_source.IsAlive(index, version));
            Assert.AreEqual(14f, at.x, 1e-4f);
            Assert.Greater(at.y, 0.3f, "the aim point is the middle of the body");
            Assert.IsFalse(_source.TryFindNearest(new Vector3(0f, 0f, 39f), 5f, out _, out _, out _), "none within range");
            Assert.AreEqual(3, _source.AliveCount);
            Assert.AreEqual(2, _source.CountInRadius(new Vector3(12f, 0.5f, 10f), 3f));
        }

        [Test]
        public void Raycast_HitsTheFirstBodyOnTheWay_AndMissesBeside()
        {
            int first = Spawn(10f, 10f);
            Spawn(20f, 10f);

            Assert.IsTrue(_source.Raycast(new Vector3(0f, 0.5f, 10f), new Vector3(30f, 0.5f, 10f), 0.1f,
                out int index, out int version, out Vector3 point));
            Assert.AreEqual(first, index);
            Assert.IsTrue(_source.IsAlive(index, version));
            Assert.Less(point.x, 10f, "the point is where the ray meets the body");
            Assert.Greater(point.x, 8.5f);

            Assert.IsFalse(_source.Raycast(new Vector3(0f, 0.5f, 14f), new Vector3(30f, 0.5f, 14f), 0.1f, out _, out _, out _),
                "4 m beside them");
        }

        [Test]
        public void Version_GoesStaleWhenTheMonsterDies()
        {
            int m = Spawn(10f, 10f);
            _source.TryFindNearest(new Vector3(10f, 0f, 10f), 5f, out int index, out int version, out _);
            Assert.AreEqual(m, index);

            _source.Damage(index, version, 1000f, Vector3.zero);
            Assert.IsTrue(_source.IsAlive(index, version), "the damage lands in the next step");
            Step();
            Assert.IsFalse(_source.IsAlive(index, version));
            Assert.AreEqual(0, _source.AliveCount);
            Assert.IsFalse(_source.TryFindNearest(new Vector3(10f, 0f, 10f), 5f, out _, out _, out _));
        }

        [Test]
        public void DamageArea_FallsOffToTheEdge_AndKills()
        {
            int center = Spawn(10f, 10f);
            int edge = Spawn(13.5f, 10f);
            int outside = Spawn(20f, 10f);

            // 30 at the centre, falling to 7.5 at the edge (4 m): the one at the centre dies (20 hp), the one 3.5 m out takes ~10.
            _source.DamageArea(new Vector3(10f, 0.3f, 10f), 4f, 30f, 0.25f);
            Step();

            Assert.AreEqual(1, _sim.Deaths.Length);
            Assert.AreEqual(center, _sim.Deaths[0].Index);
            Assert.AreEqual(HordeState.Free, _sim.GetState(center));
            float edgeHealth = _sim.GetHealth(edge);
            Assert.Less(edgeHealth, 20f, "the edge took some");
            Assert.Greater(edgeHealth, 5f, "and much less than the centre");
            Assert.AreEqual(20f, _sim.GetHealth(outside), "outside took nothing");
        }

        [Test]
        public void Queries_AllocateNothing_AlsoWhileAStepRuns()
        {
            for (int i = 0; i < 30; i++)
                Spawn(2f + i, 10f + i % 5);
            Step();
            _sim.Schedule(0.02f, default);
            try
            {
                Assert.That(() =>
                {
                    for (int i = 0; i < 10; i++)
                    {
                        _source.TryFindNearest(new Vector3(15f, 0f, 12f), 30f, out int index, out int version, out _);
                        _source.IsAlive(index, version);
                        _source.GetPosition(index);
                        _source.Raycast(new Vector3(0f, 0.5f, 12f), new Vector3(40f, 0.5f, 12f), 0.2f, out _, out _, out _);
                        _source.CountInRadius(new Vector3(15f, 0f, 12f), 5f);
                        _source.Damage(index, version, 1f, Vector3.zero);
                        _source.DamageArea(new Vector3(15f, 0f, 12f), 3f, 1f, 0.5f);
                    }
                }, NoAllocation.Constraint);
            }
            finally
            {
                _sim.Complete();
            }
        }
    }
}
