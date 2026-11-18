using System;
using System.Collections.Generic;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.AIKit.Adapters.Tests
{
    /// <summary>
    /// The arena director following a run and an arena service (fakes) in the contract's order: the arena's Built
    /// (inside Build) → the run's ArenaStarted → FightStarted. The level grid is rebuilt once per arena.
    /// </summary>
    public sealed class ArenaDirectorRunTests
    {
        private sealed class FakeHorde : IArenaHorde
        {
            public int Alive;
            public int Resets;
            public int Spawns;
            public int AliveCount => Alive;
            public float EmergeSeconds => 1f;

            public bool Spawn(EnemyKind kind, Vector3 at, float healthScale, float damageScale)
            {
                Spawns++;
                Alive++;
                return true;
            }

            public void BurrowOpened(Vector3 at)
            {
            }

            public void WaveStarted()
            {
            }

            public void ResetForArena()
            {
                Resets++;
                Alive = 0;
            }
        }

        private sealed class FakeArena : IArenaService
        {
            private readonly Vector3[] _burrows;

            public FakeArena(Vector3 centre)
            {
                Bounds = new Bounds(centre + new Vector3(0f, 10f, 0f), new Vector3(40f, 20f, 40f));
                _burrows = new Vector3[8];
                for (int i = 0; i < _burrows.Length; i++)
                {
                    float a = i * Mathf.PI * 2f / _burrows.Length;
                    _burrows[i] = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 19f;
                }
            }

            public event Action<ArenaPlan> Built;
            public event Action<Vector3> BurrowOpened { add { } remove { } }
            public event Action GateOpened { add { } remove { } }
            public event Action GatePassed { add { } remove { } }

            public bool IsBuilt { get; private set; }
            public ArenaPlan Plan { get; private set; }
            public ArenaDefinition Definition => null;
            public Bounds Bounds { get; }
            public Vector3 Entry => Bounds.center;
            public IReadOnlyList<Vector3> BurrowPoints => _burrows;
            public IReadOnlyList<Vector3> CoverPoints => Array.Empty<Vector3>();
            public bool IsGateOpen => false;
            public Vector3 GatePosition => Bounds.center;

            public bool Build(ArenaId arena, int seed)
            {
                Plan = new ArenaPlan { Seed = seed, Centre = Bounds.center };
                IsBuilt = true;
                Built?.Invoke(Plan);
                return true;
            }

            public bool TryPickBurrow(Vector3 tank, out int burrow)
            {
                burrow = 0;
                return true;
            }

            public Vector3 OpenBurrow(int burrow) => _burrows[burrow];

            public bool TryOpenBurrow(Vector3 tank, out Vector3 point)
            {
                point = _burrows[0];
                return true;
            }

            public void OpenGate()
            {
            }
        }

        private sealed class FakeRun : IRunService
        {
            public event Action RunStarted { add { } remove { } }
            public event Action<int> ArenaStarted;
            public event Action<int> FightStarted;
            public event Action<int> ArenaCleared { add { } remove { } }
            public event Action<int> GateOpened { add { } remove { } }
            public event Action<int> ArenaLeft { add { } remove { } }
            public event Action<bool> ShutterChanged { add { } remove { } }
            public event Action<int> LevelUp { add { } remove { } }
            public event Action<SkillId, int> SkillChanged { add { } remove { } }
            public event Action LootChanged { add { } remove { } }
            public event Action<RunState> StateChanged { add { } remove { } }
            public event Action<RunResult> RunEnded { add { } remove { } }

            public int Cleared;

            public RunState State { get; private set; } = RunState.Preparing;
            public bool IsRunning => true;
            public ChapterId Chapter => default;
            public int Seed => 7;
            public int ArenaNumber { get; private set; }
            public int ArenaCount => 5;
            public ArenaId Arena => default;
            public bool IsEliteArena => false;
            public float EnemyStrength => 1f;
            public float TimeSec => 0f;
            public int Level => 1;
            public int Experience => 0;
            public int ExperienceToNextLevel => 10;
            public int PendingLevelUps => 0;
            public IReadOnlyList<SkillId> SkillOffer => Array.Empty<SkillId>();
            public IReadOnlyList<SkillId> Skills => Array.Empty<SkillId>();
            public int GetSkillLevel(SkillId skill) => 0;
            public int Marks => 0;
            public int Samples => 0;
            public int Kills => 0;
            public int Crushes => 0;
            public int CrushStreak => 0;
            public int ElitesKilled => 0;
            public RunResult Result => default;

            public void StartArena(int number)
            {
                ArenaNumber = number;
                State = RunState.Preparing;
                ArenaStarted?.Invoke(number);
            }

            public void StartFight()
            {
                State = RunState.Fight;
                FightStarted?.Invoke(ArenaNumber);
            }

            public bool StartRun(ChapterId chapter, TankId tank) => true;
            public bool MarkArenaReady() => true;

            public bool MarkArenaCleared()
            {
                Cleared++;
                return true;
            }

            public bool MarkGateOpened() => true;
            public bool MarkArenaLeft() => true;

            public void AddExperience(int amount)
            {
            }

            public bool ChooseSkill(SkillId skill) => true;
            public bool EndRun(RunOutcome outcome) => true;
            public bool LeaveToHangar() => true;
        }

        private Entity _level;
        private FakeHorde _horde;
        private LevelNav _nav;
        private ArenaDirector _director;
        private GameObject _tank;

        [SetUp]
        public void SetUp()
        {
            _tank = new GameObject("Tank");
            var vehicle = new PlayerTank();
            vehicle.Bind(_tank.transform);
            _level = new Entity("Level");
            _level.AddPlayerVehicle(vehicle);
            _horde = new FakeHorde();
            _level.AddLevelHorde(_horde);
            _nav = new LevelNav(new LevelNavOptions { mapMin = new Vector2(-10f, -10f), mapMax = new Vector2(10f, 10f) });
            _level.AddLevelNavigation(_nav);
            _director = new ArenaDirector { UseGameContext = false };
            _director.Init(_level);
        }

        [TearDown]
        public void TearDown()
        {
            _director.Dispose(_level);
            _nav.Dispose();
            Object.DestroyImmediate(_tank);
        }

        [Test]
        public void ContractOrder_BuiltArenaStartedFightStarted_RebuildsTheGridOncePerArena()
        {
            var run = new FakeRun();
            var arena = new FakeArena(new Vector3(300f, 0f, -200f));
            _director.Bind(run, arena);

            for (int n = 1; n <= 2; n++)
            {
                int before = _nav.Generation;
                arena.Build(default, run.Seed + n); // the core calls Build behind the shutter...
                run.StartArena(n); // ...then ArenaStarted
                run.StartFight();

                Assert.AreEqual(before + 1, _nav.Generation, "arena " + n + ": one rebuild, at Built");
                Assert.IsTrue(_director.IsRunning);
                Assert.AreEqual(n, _director.ArenaNumber);
            }

            Assert.AreEqual(arena.Bounds.center.x, _nav.Grid.Settings.Origin.x + _nav.Grid.Size.x * _nav.Grid.CellSize * 0.5f, 1f,
                "the grid moved over the arena");
        }

        [Test]
        public void ArenaBuiltBeforeTheDirectorHeardOfIt_IsRebuiltAtTheFightOnce()
        {
            var run = new FakeRun();
            var arena = new FakeArena(new Vector3(100f, 0f, 100f));
            arena.Build(default, 8); // built before the director was bound
            _director.Bind(run, arena);
            int before = _nav.Generation;

            run.StartArena(1);
            run.StartFight();
            Assert.AreEqual(before + 1, _nav.Generation, "the missed arena is applied at the fight");

            run.StartFight(); // a second FightStarted for the same plan
            Assert.AreEqual(before + 1, _nav.Generation, "not again");
        }

        [Test]
        public void NoArenaService_TheFallbackArenaGetsTheGridRebuiltOverIt()
        {
            _tank.transform.position = new Vector3(200f, 0f, 50f);
            var run = new FakeRun();
            _director.Bind(run, null);
            int before = _nav.Generation;

            run.StartArena(1);
            run.StartFight();

            Assert.AreEqual(before + 1, _nav.Generation, "one rebuild, at the fight");
            Bounds layout = _director.Burrows.Bounds;
            Assert.AreEqual(200f, layout.center.x, 1e-3f, "the stand-in arena is round the tank");
            float gridMinX = _nav.Grid.Settings.Origin.x;
            float gridMaxX = gridMinX + _nav.Grid.Size.x * _nav.Grid.CellSize;
            Assert.LessOrEqual(gridMinX, layout.min.x, "the grid covers it");
            Assert.GreaterOrEqual(gridMaxX, layout.max.x);
        }
    }
}
