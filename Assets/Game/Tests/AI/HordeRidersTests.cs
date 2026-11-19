using System.Collections.Generic;
using Game.Horde;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.AIKit.Adapters.Tests
{
    /// <summary>
    /// Zombies leaping onto the tank (<see cref="HordeRiders"/>): a box "tank" with a body on a flat grid, a crawler by
    /// its side. Leap → cling → tear (damage), shaken off by a hard pivot and walking again, killed on the hull with the
    /// corpse put beside it, the cap of riders, no allocations while tearing.
    /// </summary>
    public sealed class HordeRidersTests
    {
        private const float DT = 1f / 30f;
        private static readonly Vector3 HullCenter = new(0f, 0.8f, 0f);
        private static readonly Vector3 HullHalf = new(1.5f, 0.8f, 3f);

        private HordeNavGrid _grid;
        private HordeFlowField _field;
        private HordeSimulation _sim;
        private HordeRiders _riders;
        private GameObject _tank;
        private Rigidbody _body;
        private readonly List<HordeRiderState> _states = new();

        [SetUp]
        public void SetUp()
        {
            _grid = new HordeNavGrid(HordeNavGridSettings.Cover(new float2(-20f, -20f), new float2(20f, 20f), 0.5f));
            using (var heights = new NativeArray<float>(_grid.CellCount, Allocator.Temp))
                _grid.SetHeights(heights);
            _field = new HordeFlowField(_grid);
            HordeSettings settings = HordeSettings.Default;
            settings.Speed = 0f;
            _sim = new HordeSimulation(32, _grid, _field, settings);
            HordeKind crawler = HordeKind.Crawler;
            crawler.Speed = 0f; // they stand: the tests place them
            _sim.SetKind(HordeKinds.CRAWLER, crawler);

            _tank = new GameObject("RiderTestTank");
            _tank.transform.position = new Vector3(0f, 0f, 0f);
            var box = _tank.AddComponent<BoxCollider>();
            box.center = HullCenter;
            box.size = HullHalf * 2f;
            _body = _tank.AddComponent<Rigidbody>();
            _body.useGravity = false;
            Physics.SyncTransforms();

            var options = new HordeRiderOptions();
            HordeRidingSettings s = HordeRidingSettings.Default;
            s.LeapChance = 1000f; // every monster in range leaps at the first scan
            s.LeapGap = 0f;
            options.settings = s;
            _riders = new HordeRiders(options, null, new HordeOptions(), 1f);
            _riders.Bind(null, _tank.transform, _body, HullCenter, HullHalf);
            _states.Clear();
            HordeRiderEvents.StateChanged += OnState;
        }

        [TearDown]
        public void TearDown()
        {
            HordeRiderEvents.StateChanged -= OnState;
            _riders.Dispose();
            _sim.Dispose();
            _field.Dispose();
            _grid.Dispose();
            Object.DestroyImmediate(_tank);
        }

        private void OnState(HordeRiderInfo info) => _states.Add(info.State);

        private HordeTank Tank() => new()
        {
            Present = true,
            Center = (float3)_tank.transform.TransformPoint(HullCenter),
            Rotation = _tank.transform.rotation,
            HalfExtents = HullHalf,
            Velocity = _body.linearVelocity
        };

        /// <summary>A game frame: the step's results, the riders, the next step.</summary>
        private void Frame(int frames = 1)
        {
            for (int i = 0; i < frames; i++)
            {
                _sim.Complete();
                _riders.Tick(_sim, _grid, DT);
                _sim.ClearDeaths();
                _sim.Schedule(DT, Tank());
            }

            _sim.Complete();
        }

        private int SpawnBeside() => _sim.Spawn(new float3(2.5f, 0f, 0.5f), -math.PI * 0.5f);

        [Test]
        public void ACrawlerBesideASlowTank_LeapsOn_ClingsAndTears_AndHurtsTheTank()
        {
            int m = SpawnBeside();
            Frame(45); // 1.5 s

            Assert.AreEqual(1, _riders.Count, "states: " + string.Join(",", _states));
            HordeRiderInfo rider = _riders.GetRider(0);
            Assert.AreEqual(m, rider.Slot);
            Assert.AreEqual(HordeRiderState.Tearing, rider.State);
            Assert.AreEqual(HordeState.Riding, _sim.GetState(m));
            Assert.AreEqual(1.6f, rider.Feet.y, 0.05f, "feet on the hull's top");
            Assert.LessOrEqual(math.abs(rider.Feet.x), HullHalf.x + 0.01f, "on the hull, not beside it");
            CollectionAssert.AreEqual(new[] { HordeRiderState.Leaping, HordeRiderState.Clinging, HordeRiderState.Tearing }, _states);
            Assert.Greater(_riders.TakeDamage(), 0f, "it strikes the tank");
            Assert.AreEqual((float3)rider.Feet, _sim.GetTargetPosition(m), "combat aims at it where it rides");
        }

        [Test]
        public void AHardPivot_ShakesItOff_AndItLandsAndWalksAgain()
        {
            int m = SpawnBeside();
            Frame(45);
            Assert.AreEqual(HordeRiderState.Tearing, _riders.GetRider(0).State);

            _body.angularVelocity = new Vector3(0f, 2.5f, 0f);
            Frame(60);

            Assert.Contains(HordeRiderState.ShakenOff, _states);
            Assert.AreEqual(HordeRiderState.None, _states[_states.Count - 1], "landed");
            Assert.AreEqual(0, _riders.Count);
            Assert.AreEqual(HordeState.Walk, _sim.GetState(m), "back with the crowd");
            float3 p = _sim.GetPosition(m);
            Assert.AreEqual(0f, p.y, 0.01f, "on the ground");
            Assert.Greater(math.max(math.abs(p.x) - HullHalf.x, math.abs(p.z) - HullHalf.z), 0f, "off the hull");
        }

        [Test]
        public void CalmDriving_DoesNotShakeItOff()
        {
            _body.linearVelocity = new Vector3(0f, 0f, 2.5f); // slow enough for a leap
            _body.angularVelocity = new Vector3(0f, 0.4f, 0f);
            SpawnBeside();
            Frame(150);
            Assert.AreEqual(1, _riders.Count);
            Assert.AreEqual(HordeRiderState.Tearing, _riders.GetRider(0).State);
        }

        [Test]
        public void HitsKnockItLoose()
        {
            int m = SpawnBeside();
            Frame(45);
            int version = _sim.Version(m);
            for (int i = 0; i < 3; i++)
            {
                _sim.Damage(m, version, 2f);
                Frame();
            }

            Frame(60);
            Assert.AreEqual(HordeState.Walk, _sim.GetState(m), "fell off and walks");
        }

        [Test]
        public void KilledOnTheTank_ItsCorpseLiesBesideTheHull()
        {
            int m = SpawnBeside();
            Frame(45);
            var died = new List<HordeRiderInfo>();
            void OnDied(HordeRiderInfo info) => died.Add(info);
            HordeRiderEvents.Died += OnDied;
            try
            {
                _sim.Damage(m, _sim.Version(m), 1000f);
                _sim.Schedule(DT, Tank());
                _sim.Complete();
                _riders.Tick(_sim, _grid, DT);

                Assert.AreEqual(1, died.Count);
                Assert.AreEqual(0, _riders.Count);
                Assert.AreEqual(1, _sim.Deaths.Length);
                float3 corpse = _sim.Deaths[0].Position;
                Assert.AreEqual(0f, corpse.y, 0.01f, "on the ground, not in the air");
                Assert.Greater(math.max(math.abs(corpse.x) - HullHalf.x, math.abs(corpse.z) - HullHalf.z), 0f, "beside the hull, not under it");
            }
            finally
            {
                HordeRiderEvents.Died -= OnDied;
            }
        }

        [Test]
        public void NoMoreRidersThanPoints()
        {
            for (int i = 0; i < 12; i++)
                _sim.Spawn(new float3(i % 2 == 0 ? 2.4f : -2.4f, 0f, -2.5f + (i / 2) * 1f), 0f);
            Frame(90);
            Assert.AreEqual(HordeRiders.MAX_RIDERS, _riders.Count);
        }

        [Test]
        public void NoLeapsOntoAFastTank()
        {
            SpawnBeside();
            _body.linearVelocity = new Vector3(0f, 0f, 12f);
            Frame(30);
            Assert.AreEqual(0, _riders.Count);
        }

        [Test]
        public void Tearing_AllocatesNothing()
        {
            SpawnBeside();
            Frame(45);
            Frame(30); // warm
            Assert.That(() =>
            {
                _sim.Complete();
                _riders.Tick(_sim, _grid, DT);
                _riders.Draw(new Bounds(Vector3.zero, Vector3.one * 100f));
                _sim.Schedule(DT, Tank());
                _sim.Complete();
            }, NoAllocation.Constraint);
        }
    }
}
