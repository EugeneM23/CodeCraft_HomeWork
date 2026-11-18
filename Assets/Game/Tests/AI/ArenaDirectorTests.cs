using System.Collections.Generic;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.AIKit.Adapters.Tests
{
    /// <summary>The arena director without a run: lull, limit and refill, burrows, strength, events, the elite, reinforcements.</summary>
    public sealed class ArenaDirectorTests
    {
        private const float DT = 0.1f;

        private sealed class FakeHorde : IArenaHorde
        {
            public readonly List<(EnemyKind kind, Vector3 at, float health, float damage, float time)> Spawns = new(1024);
            public readonly List<Vector3> Opened = new(256);
            public int Alive;
            public int Waves;
            public int Resets;
            public float Clock;

            public int AliveCount => Alive;
            public float EmergeSeconds => 1f;

            public bool Spawn(EnemyKind kind, Vector3 at, float healthScale, float damageScale)
            {
                Spawns.Add((kind, at, healthScale, damageScale, Clock));
                Alive++;
                return true;
            }

            public void BurrowOpened(Vector3 at) => Opened.Add(at);

            public void SetCover(IReadOnlyList<Vector3> points)
            {
            }

            public void WaveStarted() => Waves++;

            public void ResetForArena()
            {
                Resets++;
                Alive = 0;
            }
        }

        private sealed class FakeElites : IArenaEliteSpawner
        {
            public int Spawned;
            public float Strength;

            public bool SpawnElite(Vector3 at, Vector3 facing, float strength)
            {
                Spawned++;
                Strength = strength;
                return true;
            }
        }

        private Entity _level;
        private FakeHorde _horde;
        private FakeElites _elites;
        private ArenaDirector _director;
        private GameObject _tank;
        private readonly List<int> _wavesStarted = new();
        private int _cleared;
        private readonly List<Object> _assets = new();

        [SetUp]
        public void SetUp()
        {
            _tank = new GameObject("Tank");
            _tank.transform.position = new Vector3(-15f, 0f, -15f); // near a corner of the 40 m arena round the origin
            var vehicle = new PlayerTank();
            vehicle.Bind(_tank.transform);
            _level = new Entity("Level");
            _level.AddPlayerVehicle(vehicle);
            _horde = new FakeHorde();
            _level.AddLevelHorde(_horde);
            _elites = new FakeElites();
            _director = new ArenaDirector { EliteSpawner = _elites, UseGameContext = false };
            _director.Init(_level);
            AIEvents.WaveStarted += OnWave;
            AIEvents.ArenaCleared += OnCleared;
        }

        [TearDown]
        public void TearDown()
        {
            AIEvents.WaveStarted -= OnWave;
            AIEvents.ArenaCleared -= OnCleared;
            _director.Dispose(_level);
            Object.DestroyImmediate(_tank);
            foreach (Object asset in _assets)
                Object.DestroyImmediate(asset);
            _assets.Clear();
            _wavesStarted.Clear();
            _cleared = 0;
        }

        private void OnWave(int wave) => _wavesStarted.Add(wave);

        private void OnCleared() => _cleared++;

        private ArenaWaves Waves(int limit, params ArenaWave[] waves)
        {
            var asset = ScriptableObject.CreateInstance<ArenaWaves>();
            asset.waves = waves;
            asset.liveLimit = limit;
            _assets.Add(asset);
            return asset;
        }

        private static ArenaWave Wave(float delay, float interval, params ArenaWaveEntry[] entries) =>
            new() { delay = delay, spawnInterval = interval, entries = entries };

        private static EdgeBurrowsLayout Layout(int burrows = 16) => EdgeBurrowsLayout.Around(Vector3.zero, 40f, burrows);

        private void Run(float seconds)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += DT)
            {
                _director.Tick(_level, DT);
                _horde.Clock += DT;
            }
        }

        [Test]
        public void Lull_TwoSecondsWithoutMonsters_ThenTheFirstWave()
        {
            _director.Begin(1, Waves(40, Wave(0f, 0.2f, new ArenaWaveEntry(EnemyKind.Crawler, 5))), Layout());
            Assert.AreEqual(1, _horde.Resets, "the stage is cleared");

            Run(1.9f);
            Assert.AreEqual(0, _horde.Spawns.Count, "the lull");
            Assert.AreEqual(0, _wavesStarted.Count);

            Run(0.5f);
            Assert.Greater(_horde.Spawns.Count, 0);
            CollectionAssert.AreEqual(new[] { 0 }, _wavesStarted);
            Assert.AreEqual(1, _horde.Waves, "the wave's sound");
        }

        [Test]
        public void Limit_IsKept_AndTheRefillWaitsForSeventyPercent()
        {
            _director.Begin(1, Waves(40, Wave(0f, 0f, new ArenaWaveEntry(EnemyKind.Crawler, 80))), Layout(48));
            Run(6f);
            Assert.AreEqual(40, _horde.Alive, "up to the limit");
            Assert.AreEqual(40, _director.Queued);

            _horde.Alive = 29; // above 70 % (28)
            Run(2f);
            Assert.AreEqual(29, _horde.Alive, "no refill above 70 %");

            _horde.Alive = 27;
            Run(4f);
            Assert.AreEqual(40, _horde.Alive, "refilled to the limit");
        }

        [Test]
        public void Burrows_FarFromTheTankFirst_NeverNearer15m_OneEmergingAtATime()
        {
            EdgeBurrowsLayout layout = Layout();
            _director.Begin(1, Waves(40, Wave(0f, 0f, new ArenaWaveEntry(EnemyKind.Crawler, 30))), layout);
            Run(5f);

            Vector3 tank = _tank.transform.position;
            float farthest = 0f;
            foreach (Vector3 p in layout.BurrowPoints)
                farthest = Mathf.Max(farthest, Flat(p - tank));
            Assert.AreEqual(farthest, Flat(_horde.Spawns[0].at - tank), 1e-3f, "the first comes out of the farthest burrow");

            for (int i = 0; i < _horde.Spawns.Count; i++)
            {
                Assert.GreaterOrEqual(Flat(_horde.Spawns[i].at - tank), ArenaDirector.MIN_BURROW_DISTANCE);
                for (int j = 0; j < i; j++)
                    if (_horde.Spawns[j].at == _horde.Spawns[i].at)
                        Assert.GreaterOrEqual(_horde.Spawns[i].time - _horde.Spawns[j].time, 1f - 1e-3f,
                            "one burrow lets the next out after the last has emerged");
            }

            Assert.AreEqual(layout.OpenedCount, _horde.Opened.Count, "each opened burrow is heard once");
        }

        [Test]
        public void Strength_ChapterTimesSixPercentPerArena()
        {
            ArenaWaves waves = Waves(40, Wave(0f, 0f, new ArenaWaveEntry(EnemyKind.Spitter, 1)));
            waves.chapterMultiplier = 1.6f;
            _director.Begin(3, waves, Layout());
            Run(2.5f);

            float expected = 1.6f * 1.12f;
            Assert.AreEqual(expected, _director.Strength, 1e-4f);
            Assert.AreEqual(EnemyKind.Spitter, _horde.Spawns[0].kind);
            Assert.AreEqual(expected, _horde.Spawns[0].health, 1e-4f);
            Assert.AreEqual(expected, _horde.Spawns[0].damage, 1e-4f);
        }

        [Test]
        public void Events_EachWaveStartsOnce_AndTheArenaClearsOnce()
        {
            _director.Begin(1, Waves(40,
                Wave(0f, 0f, new ArenaWaveEntry(EnemyKind.Crawler, 3)),
                Wave(3f, 0f, new ArenaWaveEntry(EnemyKind.Crawler, 3))), Layout());
            Run(4f);
            CollectionAssert.AreEqual(new[] { 0 }, _wavesStarted);
            _horde.Alive = 0;
            Run(1f);
            Assert.AreEqual(0, _cleared, "the second wave is still to come");

            Run(3f);
            CollectionAssert.AreEqual(new[] { 0, 1 }, _wavesStarted);
            Assert.AreEqual(0, _cleared, "its monsters live");
            Assert.IsTrue(_level.HasArenaReinforcements(), "the reinforcements door is open while the arena runs");

            _horde.Alive = 0;
            Run(1f);
            Assert.AreEqual(1, _cleared);
            Assert.IsFalse(_level.HasArenaReinforcements(), "and closed after");
            Run(2f);
            Assert.AreEqual(1, _cleared, "once");
            CollectionAssert.AreEqual(new[] { 0, 1 }, _wavesStarted);
        }

        [Test]
        public void Elite_CountsAsAlive_UntilItsDeathArrives()
        {
            _director.Begin(5, Waves(40, Wave(0f, 0f, new ArenaWaveEntry(EnemyKind.EliteRegax, 1))), Layout());
            Run(3f);
            Assert.AreEqual(1, _elites.Spawned);
            Assert.AreEqual(1.24f, _elites.Strength, 1e-4f);
            Assert.AreEqual(1, _director.ElitesAlive);
            Assert.AreEqual(1, _director.Live);

            AIEvents.RaiseEnemyKilled(new EnemyKilledInfo(AIEnemyKind.Crawler, Vector3.zero, false, false));
            Run(1f);
            Assert.AreEqual(0, _cleared, "a crawler's death is not the elite's");

            AIEvents.RaiseEnemyKilled(new EnemyKilledInfo(AIEnemyKind.EliteRegax, Vector3.zero, false, true));
            Run(0.2f);
            Assert.AreEqual(0, _director.ElitesAlive);
            Assert.AreEqual(1, _cleared);
        }

        [Test]
        public void Reinforcements_ComeOutOfTheNearestBurrows_OverTheLimit()
        {
            EdgeBurrowsLayout layout = Layout();
            _director.Begin(5, Waves(2, Wave(0f, 0f, new ArenaWaveEntry(EnemyKind.Crawler, 2))), layout);
            Run(3f);
            Assert.AreEqual(2, _horde.Alive, "the limit is 2");

            var near = new Vector3(20f, 0f, 20f);
            IArenaReinforcements door = _level.GetArenaReinforcements();
            door.Summon(AIEnemyKind.Crawler, 5, near);
            Run(3f);

            Assert.AreEqual(7, _horde.Alive, "summoned over the limit");
            float nearest = float.MaxValue;
            foreach (Vector3 p in layout.BurrowPoints)
                nearest = Mathf.Min(nearest, Flat(p - near));
            Assert.AreEqual(nearest, Flat(_horde.Spawns[2].at - near), 1e-3f, "the first from the burrow nearest the point");
            for (int i = 2; i < _horde.Spawns.Count; i++)
                Assert.Less(Flat(_horde.Spawns[i].at - near), 26f, "all from the burrows near it");
        }

        [Test]
        public void Tick_AllocatesNothing()
        {
            _director.Begin(1, Waves(40, Wave(0f, 0.05f, new ArenaWaveEntry(EnemyKind.Crawler, 100))), Layout(40));
            Run(3f); // past the lull, the wave started
            Assert.That(() =>
            {
                for (int i = 0; i < 20; i++)
                {
                    _director.Tick(_level, DT);
                    _horde.Clock += DT;
                    if (_horde.Alive > 20)
                        _horde.Alive -= 3;
                }
            }, NoAllocation.Constraint);
        }

        private static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }
    }
}
