using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class RunServiceTests
    {
        private static readonly ChapterId Short = new("short");
        private static readonly ChapterId Rich = new("rich");
        private static readonly ChapterId Empty = new("empty");

        private readonly List<Object> _assets = new();
        private readonly List<string> _log = new();
        private readonly HashSet<ChapterId> _completed = new();
        private readonly Dictionary<ChapterId, int> _bestArena = new();

        private GameCatalog _catalog;
        private EnemyDeaths _deaths;
        private FakeArenaBuilder _builder;
        private Ticking.GameLoop _loop;
        private RunService _run;
        private int _leftToHangar;

        [SetUp]
        public void SetUp()
        {
            _log.Clear();
            _completed.Clear();
            _bestArena.Clear();
            _leftToHangar = 0;

            var arenas = new ArenaConfig[5];
            for (int i = 0; i < arenas.Length; i++)
                arenas[i] = Arena("a" + (i + 1), i == 4 ? ArenaKind.Elite : ArenaKind.Horde);

            ChapterConfig outskirts = Chapter(ChapterIds.Outskirts, 1f, 1f, 1000, 10, arenas);
            ChapterConfig rich = Chapter(Rich, 1.6f, 1.5f, 2000, 15, Arena("r1", ArenaKind.Horde), Arena("r2", ArenaKind.Boss));
            ChapterConfig shortOne = Chapter(Short, 1f, 1f, 1000, 10, Arena("s1", ArenaKind.Horde));
            ChapterConfig empty = Chapter(Empty, 1f, 1f, 1000, 10);

            _catalog = Make<GameCatalog>();
            _catalog.Configure(
                new[] { Tank(TankIds.Tiger, 4, 6), Tank(TankIds.Hound, 1, 1) },
                new ModuleConfig[0],
                new[]
                {
                    Skill(SkillIds.Turret, SkillGroup.Weapon),
                    Skill(SkillIds.AirStrike, SkillGroup.Weapon),
                    Skill(SkillIds.Ricochet, SkillGroup.Gun),
                    Skill(SkillIds.RateOfFire, SkillGroup.Gun),
                    Skill(SkillIds.Armor, SkillGroup.Tank),
                    Skill(SkillIds.Magnet, SkillGroup.Tank),
                    Skill(SkillIds.Mortar, SkillGroup.Weapon, false)
                },
                new[] { outskirts, rich, shortOne, empty });

            _deaths = new EnemyDeaths();
            _builder = new FakeArenaBuilder { Log = _log };
            _loop = new Ticking.GameLoop();
            _run = Create(_catalog);
        }

        [TearDown]
        public void TearDown()
        {
            _run?.Dispose();
            foreach (Object asset in _assets)
                Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        // ---------- Arena flow ----------

        [Test]
        public void FullRun_FiveArenas_RaisesEventsInOrder_AndEndsInVictory()
        {
            Listen(_run);
            RunResult ended = default;
            int endedCount = 0;
            _run.RunEnded += r => { ended = r; endedCount++; };

            Assert.IsTrue(_run.StartRun(ChapterIds.Outskirts, TankIds.Tiger));
            for (int n = 1; n <= 5; n++)
            {
                ToFight();
                Assert.AreEqual(RunState.Fight, _run.State);
                Assert.AreEqual(n == 5, _run.IsEliteArena);
                Assert.IsTrue(_run.MarkArenaCleared());
                if (n < 5)
                    LeaveArena();
            }

            Assert.AreEqual(RunState.Cleared, _run.State);
            _run.Tick(1f);
            Assert.AreEqual(RunState.Cleared, _run.State, "the kill is seen for 1.5 s");
            _run.Tick(0.5f);

            var expected = new List<string> { "RunStarted", "Shutter(True)" };
            for (int n = 1; n <= 5; n++)
            {
                expected.Add($"Build(a{n})");
                expected.AddRange(new[] { "State(Preparing)", $"ArenaStarted({n})", "Shutter(False)", "State(Lull)",
                    "State(Fight)", $"FightStarted({n})", "State(Cleared)", $"ArenaCleared({n})", "Loot" });
                if (n < 5)
                    expected.AddRange(new[] { "State(GateOpen)", $"GateOpened({n})", "State(Leaving)", $"ArenaLeft({n})",
                        "Shutter(True)" });
            }

            expected.AddRange(new[] { "Loot", "State(Ended)", "RunEnded(Victory)" });
            CollectionAssert.AreEqual(expected, _log);

            Assert.AreEqual(1, endedCount);
            Assert.AreEqual(RunState.Ended, _run.State);
            Assert.IsFalse(_run.IsRunning);
            Assert.AreEqual(RunOutcome.Victory, ended.outcome);
            Assert.AreEqual(ChapterIds.Outskirts, ended.chapter);
            Assert.AreEqual(5, ended.arenaReached);
            Assert.IsTrue(ended.firstCompletion);
            Assert.AreEqual(12 + 14 + 16 + 18 + 20 + 1000, ended.marks);
            Assert.AreEqual(10, ended.samples);
            Assert.AreEqual(_run.TimeSec, ended.timeSec);
            Assert.Greater(ended.timeSec, 0f);
            Assert.AreEqual(ended, _run.Result);

            Assert.AreEqual(5, _builder.Builds.Count);
            for (int n = 1; n <= 5; n++)
                Assert.AreEqual(_run.Seed + n, _builder.Builds[n - 1].seed);
        }

        [Test]
        public void Preparing_WithoutMarkArenaReady_OpensTheShutterAfter5Seconds_ThenLull2Seconds()
        {
            Listen(_run);
            _run.StartRun(ChapterIds.Outskirts, TankIds.Tiger);
            _log.Clear();

            _run.Tick(4.5f);
            CollectionAssert.IsEmpty(_log);
            _run.Tick(0.5f);
            CollectionAssert.AreEqual(new[] { "Shutter(False)" }, _log);
            Assert.IsFalse(_run.MarkArenaReady(), "already opening");

            _run.Tick(0.25f);
            Assert.AreEqual(RunState.Preparing, _run.State, "the shutter is still opening");
            _run.Tick(0.25f);
            Assert.AreEqual(RunState.Lull, _run.State);

            _run.Tick(1.5f);
            Assert.AreEqual(RunState.Lull, _run.State);
            _run.Tick(0.5f);
            Assert.AreEqual(RunState.Fight, _run.State);
            Assert.AreEqual(4.5f + 0.5f + 0.5f + 2f, _run.TimeSec, 1e-4f);
        }

        [Test]
        public void MarkArenaReady_InsideBuild_OpensTheShutterOnTheNextTick_AfterArenaStarted()
        {
            _builder.OnBuild = () => Assert.IsTrue(_run.MarkArenaReady());
            Listen(_run);

            _run.StartRun(ChapterIds.Outskirts, TankIds.Tiger);
            CollectionAssert.AreEqual(new[] { "RunStarted", "Shutter(True)", "Build(a1)", "State(Preparing)", "ArenaStarted(1)" }, _log);

            _run.Tick(0.01f);
            Assert.AreEqual("Shutter(False)", _log[_log.Count - 1]);
        }

        [Test]
        public void WithoutArenaBuilder_TheArenaIsReadyAtOnce()
        {
            RunService run = Create(_catalog, false);
            run.StartRun(ChapterIds.Outskirts, TankIds.Tiger);
            Assert.IsFalse(run.MarkArenaReady());

            run.Tick(0.01f);
            run.Tick(0.5f);
            Assert.AreEqual(RunState.Lull, run.State);
            run.Dispose();
        }

        [Test]
        public void Leaving_PullsTheOrbsHalfASecond_ThenTheShutterCloses_ThenTheNextArena()
        {
            _run.StartRun(ChapterIds.Outskirts, TankIds.Tiger);
            ToFight();
            _run.MarkArenaCleared();
            _run.MarkGateOpened();
            _log.Clear(); // the builder logged arena 1's Build already
            Listen(_run);

            Assert.IsTrue(_run.MarkArenaLeft());
            CollectionAssert.AreEqual(new[] { "State(Leaving)", "ArenaLeft(1)" }, _log);

            _run.Tick(0.25f);
            Assert.AreEqual(2, _log.Count);
            _run.Tick(0.25f);
            Assert.AreEqual("Shutter(True)", _log[2]);
            _run.Tick(0.25f);
            Assert.AreEqual(RunState.Leaving, _run.State);
            _run.Tick(0.25f);

            CollectionAssert.AreEqual(new[] { "State(Leaving)", "ArenaLeft(1)", "Shutter(True)", "Build(a2)",
                "State(Preparing)", "ArenaStarted(2)" }, _log);
            Assert.AreEqual(2, _run.ArenaNumber);
            Assert.AreEqual(new ArenaId("a2"), _run.Arena);
        }

        [Test]
        public void CallsOutOfState_AreIgnored()
        {
            Assert.IsFalse(_run.MarkArenaReady());
            Assert.IsFalse(_run.MarkArenaCleared());
            Assert.IsFalse(_run.EndRun(RunOutcome.Defeat));
            Assert.IsFalse(_run.ChooseSkill(SkillIds.Turret));
            Assert.IsFalse(_run.StartRun(new ChapterId("nope"), TankIds.Tiger));
            Assert.IsFalse(_run.StartRun(Empty, TankIds.Tiger));
            _run.AddExperience(100);
            Assert.AreEqual(1, _run.Level);
            Assert.AreEqual(RunState.None, _run.State);

            _run.StartRun(ChapterIds.Outskirts, TankIds.Tiger);
            Assert.IsFalse(_run.StartRun(ChapterIds.Outskirts, TankIds.Tiger));
            Assert.IsFalse(_run.MarkArenaCleared());
            Assert.IsFalse(_run.MarkGateOpened());
            Assert.IsFalse(_run.MarkArenaLeft());

            ToFight();
            Assert.IsFalse(_run.MarkArenaReady());
            Assert.IsFalse(_run.MarkGateOpened());
            Assert.IsFalse(_run.MarkArenaLeft());
            Assert.IsFalse(_run.EndRun(RunOutcome.Victory));

            _run.MarkArenaCleared();
            Assert.IsFalse(_run.MarkArenaCleared());
            Assert.IsFalse(_run.MarkArenaLeft());
            Assert.AreEqual(RunState.Cleared, _run.State);
        }

        [Test]
        public void LastArenaCleared_NoGate_AndADefeatInTheVictoryDelay_IsAVictory()
        {
            _run.StartRun(Short, TankIds.Tiger);
            ToFight();
            _run.MarkArenaCleared();
            Assert.IsFalse(_run.MarkGateOpened());

            Assert.IsTrue(_run.EndRun(RunOutcome.Defeat));
            Assert.AreEqual(RunOutcome.Victory, _run.Result.outcome);
        }

        [Test]
        public void EnemyStrength_IsTheChapterMultiplier_Times1_06PerArenaAfterTheFirst()
        {
            _run.StartRun(Rich, TankIds.Tiger);
            Assert.AreEqual(1.6f, _run.EnemyStrength, 1e-5f);
            ToFight();
            _run.MarkArenaCleared();
            LeaveArena();
            Assert.AreEqual(1.6f * 1.06f, _run.EnemyStrength, 1e-5f);
            Assert.IsTrue(_run.IsEliteArena, "a boss arena counts");
        }

        // ---------- Loot ----------

        [Test]
        public void Deaths_PayPerKind_WithTheCrushBonus_AndTheChapterMultiplierOnTheSum()
        {
            _run.StartRun(Rich, TankIds.Tiger);
            int lootChanged = 0;
            _run.LootChanged += () => lootChanged++;

            Die(EnemyKind.Crawler);
            Die(EnemyKind.Crawler, crushed: true);
            Die(EnemyKind.Spitter);
            Die(EnemyKind.Exploder);
            Die(EnemyKind.EliteRegax, elite: true);
            Die(EnemyKind.Boss, elite: true);

            // Base 1 + 2 + 2 + 2 + 40 + 200 = 247 marks, 2 + 5 = 7 samples; × 1.5 = 370.5 → 371, 10.5 → 11
            Assert.AreEqual(371, _run.Marks);
            Assert.AreEqual(11, _run.Samples);
            Assert.AreEqual(6, _run.Kills);
            Assert.AreEqual(1, _run.Crushes);
            Assert.AreEqual(1, _run.ElitesKilled);
            Assert.AreEqual(1, _run.BossesKilled);
            Assert.AreEqual(6, lootChanged);
        }

        [Test]
        public void ArenaCleared_Pays10Plus2N()
        {
            _run.StartRun(ChapterIds.Outskirts, TankIds.Tiger);
            ToFight();
            _run.MarkArenaCleared();
            Assert.AreEqual(12, _run.Marks);
            LeaveArena();
            ToFight();
            _run.MarkArenaCleared();
            Assert.AreEqual(12 + 14, _run.Marks);
        }

        [Test]
        public void Victory_FirstCompletion_AddsTheChapterRewardUnmultiplied()
        {
            RunResult result = WinRich();

            Assert.IsTrue(result.firstCompletion);
            Assert.AreEqual(39 + 2000, result.marks, "(12 + 14) × 1.5 + 2000");
            Assert.AreEqual(15, result.samples);
        }

        [Test]
        public void Victory_OfACompletedChapter_HasNoFirstCompletionReward()
        {
            _completed.Add(Rich);
            RunResult result = WinRich();

            Assert.IsFalse(result.firstCompletion);
            Assert.AreEqual(39, result.marks);
            Assert.AreEqual(0, result.samples);
        }

        [Test]
        public void Defeat_KeepsTheLoot_AndDeathsAfterTheEndAreIgnored()
        {
            _bestArena[ChapterIds.Outskirts] = 3;
            int ended = 0;
            _run.RunEnded += _ => ended++;
            _run.StartRun(ChapterIds.Outskirts, TankIds.Tiger);
            ToFight();
            Die(EnemyKind.EliteRegax);

            Assert.IsTrue(_run.EndRun(RunOutcome.Defeat));
            Die(EnemyKind.Boss);
            Assert.IsFalse(_run.EndRun(RunOutcome.Defeat));

            RunResult result = _run.Result;
            Assert.AreEqual(1, ended);
            Assert.AreEqual(RunOutcome.Defeat, result.outcome);
            Assert.AreEqual(40, result.marks);
            Assert.AreEqual(2, result.samples);
            Assert.AreEqual(1, result.kills);
            Assert.AreEqual(1, result.elitesKilled);
            Assert.AreEqual(1, result.arenaReached);
            Assert.IsFalse(result.firstCompletion);
            Assert.AreEqual(3, result.previousBestArena);
            Assert.IsFalse(result.newRecord);
        }

        [Test]
        public void NewRecord_WhenFurtherThanTheBestBefore()
        {
            _bestArena[ChapterIds.Outskirts] = 1;
            _run.StartRun(ChapterIds.Outskirts, TankIds.Tiger);
            ToFight();
            _run.MarkArenaCleared();
            LeaveArena();
            _run.EndRun(RunOutcome.Defeat);

            Assert.AreEqual(2, _run.Result.arenaReached);
            Assert.AreEqual(1, _run.Result.previousBestArena);
            Assert.IsTrue(_run.Result.newRecord);
        }

        [Test]
        public void CrushStreak_GoesOnWithin2Seconds_IgnoresShots_AndEndsAfter2SecondsWithout()
        {
            _run.StartRun(ChapterIds.Outskirts, TankIds.Tiger);
            Die(EnemyKind.Crawler, crushed: true);
            Assert.AreEqual(1, _run.CrushStreak);
            _run.Tick(1.5f);
            Die(EnemyKind.Crawler, crushed: true);
            Assert.AreEqual(2, _run.CrushStreak);
            Die(EnemyKind.Spitter);
            Assert.AreEqual(2, _run.CrushStreak, "a kill by fire does not break it");
            _run.Tick(1.5f);
            Die(EnemyKind.Crawler, crushed: true);
            Assert.AreEqual(3, _run.CrushStreak);
            _run.Tick(2.1f);
            Assert.AreEqual(0, _run.CrushStreak);
            Die(EnemyKind.Crawler, crushed: true);
            Assert.AreEqual(1, _run.CrushStreak);
        }

        [Test]
        public void LeaveToHangar_EndsAsAbandoned_AndCallsTheHook()
        {
            Assert.IsFalse(_run.LeaveToHangar());
            _run.StartRun(ChapterIds.Outskirts, TankIds.Tiger);
            Die(EnemyKind.Spitter);

            Assert.IsTrue(_run.LeaveToHangar());
            Assert.AreEqual(1, _leftToHangar);
            Assert.AreEqual(RunOutcome.Abandoned, _run.Result.outcome);
            Assert.AreEqual(2, _run.Result.marks);
            Assert.IsFalse(_run.LeaveToHangar());
            Assert.AreEqual(1, _leftToHangar);
        }

        // ---------- Experience and skills ----------

        [Test]
        public void Experience_LevelsUpBy10Plus12PerLevel_WithCarryOver()
        {
            _run.StartRun(ChapterIds.Outskirts, TankIds.Tiger);
            Assert.AreEqual(10, _run.ExperienceToNextLevel);

            _run.AddExperience(9);
            Assert.AreEqual(1, _run.Level);
            Assert.AreEqual(9, _run.Experience);

            _run.AddExperience(4);
            Assert.AreEqual(2, _run.Level);
            Assert.AreEqual(3, _run.Experience);
            Assert.AreEqual(22, _run.ExperienceToNextLevel);
            Assert.AreEqual(1, _run.PendingLevelUps);
        }

        [Test]
        public void TwoLevelUpsAtOnce_OfferOneAfterTheOther_UnderOnePause()
        {
            var levelUps = new List<int>();
            _run.LevelUp += levelUps.Add;
            _run.StartRun(ChapterIds.Outskirts, TankIds.Tiger);

            _run.AddExperience(10 + 22 + 5);
            Assert.AreEqual(3, _run.Level);
            Assert.AreEqual(5, _run.Experience);
            Assert.AreEqual(2, _run.PendingLevelUps);
            CollectionAssert.AreEqual(new[] { 2 }, levelUps);
            Assert.AreEqual(3, _run.SkillOffer.Count);
            Assert.IsTrue(_loop.IsPausedBy(_run));

            Assert.IsTrue(_run.ChooseSkill(_run.SkillOffer[0]));
            Assert.AreEqual(1, _run.PendingLevelUps);
            CollectionAssert.AreEqual(new[] { 2, 3 }, levelUps);
            Assert.IsTrue(_loop.IsPausedBy(_run));

            Assert.IsTrue(_run.ChooseSkill(_run.SkillOffer[1]));
            Assert.AreEqual(0, _run.PendingLevelUps);
            Assert.AreEqual(0, _run.SkillOffer.Count);
            Assert.IsFalse(_loop.IsPausedBy(_run));
            Assert.IsFalse(_loop.IsPaused);
        }

        [Test]
        public void Offer_HasDistinctPoolSkillsOnly()
        {
            _run.StartRun(ChapterIds.Outskirts, TankIds.Tiger);
            for (int i = 0; i < 20; i++)
            {
                _run.AddExperience(_run.ExperienceToNextLevel);
                IReadOnlyList<SkillId> offer = _run.SkillOffer;
                Assert.AreEqual(3, offer.Count);
                CollectionAssert.AllItemsAreUnique(offer);
                CollectionAssert.DoesNotContain(offer, SkillIds.Mortar);
                _run.ChooseSkill(offer[i % 3]);
            }
        }

        [Test]
        public void Offer_StopsAtLevel5_ThenALevelUpWithNothingToOfferIsUsedUpSilently()
        {
            GameCatalog one = Make<GameCatalog>();
            one.Configure(new TankConfig[0], new ModuleConfig[0], new[] { Skill(SkillIds.Magnet, SkillGroup.Tank) },
                new[] { Chapter(new ChapterId("one"), 1f, 1f, 0, 0, Arena("o1", ArenaKind.Horde)) });
            RunService run = Create(one);
            var levelUps = new List<int>();
            run.LevelUp += levelUps.Add;
            run.StartRun(new ChapterId("one"), TankIds.Tiger);

            for (int level = 1; level <= SkillConfig.MAX_LEVEL; level++)
            {
                run.AddExperience(run.ExperienceToNextLevel);
                CollectionAssert.AreEqual(new[] { SkillIds.Magnet }, run.SkillOffer);
                Assert.IsTrue(run.ChooseSkill(SkillIds.Magnet));
                Assert.AreEqual(level, run.GetSkillLevel(SkillIds.Magnet));
            }

            run.AddExperience(run.ExperienceToNextLevel);
            Assert.AreEqual(7, run.Level);
            Assert.AreEqual(0, run.PendingLevelUps);
            Assert.AreEqual(0, run.SkillOffer.Count);
            Assert.AreEqual(5, levelUps.Count);
            Assert.IsFalse(_loop.IsPaused);
            run.Dispose();
        }

        [Test]
        public void Offer_RespectsTheTanksWeaponAndUpgradeSlots()
        {
            _run.StartRun(ChapterIds.Outskirts, TankIds.Hound); // 1 weapon, 1 upgrade
            for (int i = 0; i < 12; i++)
            {
                _run.AddExperience(_run.ExperienceToNextLevel);
                if (_run.SkillOffer.Count == 0)
                    continue;

                _run.ChooseSkill(_run.SkillOffer[0]);
            }

            Assert.AreEqual(2, _run.Skills.Count);
            int weapons = 0;
            foreach (SkillId skill in _run.Skills)
            {
                if (_catalog.FindSkill(skill).Group == SkillGroup.Weapon)
                    weapons++;
                Assert.AreEqual(5, _run.GetSkillLevel(skill));
            }

            Assert.AreEqual(1, weapons);
            Assert.AreEqual(0, _run.SkillOffer.Count);
        }

        [Test]
        public void ChooseSkill_OnlyFromTheOffer_TakesThenLevelsUp()
        {
            var changes = new List<string>();
            _run.SkillChanged += (s, l) => changes.Add($"{s}:{l}");
            _run.StartRun(ChapterIds.Outskirts, TankIds.Tiger);
            _run.AddExperience(_run.ExperienceToNextLevel);

            SkillId notOffered = SkillIds.Mortar;
            Assert.IsFalse(_run.ChooseSkill(notOffered));
            SkillId picked = _run.SkillOffer[0];
            Assert.IsTrue(_run.ChooseSkill(picked));
            Assert.IsFalse(_run.ChooseSkill(picked), "the offer is gone");

            CollectionAssert.AreEqual(new[] { picked }, _run.Skills);
            Assert.AreEqual(1, _run.GetSkillLevel(picked));

            // Level it up when it is offered again
            for (int i = 0; i < 30 && _run.GetSkillLevel(picked) < 2; i++)
            {
                _run.AddExperience(_run.ExperienceToNextLevel);
                _run.ChooseSkill(Contains(_run.SkillOffer, picked) ? picked : _run.SkillOffer[0]);
            }

            Assert.AreEqual(2, _run.GetSkillLevel(picked));
            Assert.AreEqual($"{picked}:1", changes[0]);
            Assert.Contains($"{picked}:2", changes);
            Assert.AreEqual(1, CountOf(_run.Skills, picked), "taken once, in the order taken");
        }

        [Test]
        public void EndRun_LetsTheOffersPauseGo()
        {
            _run.StartRun(ChapterIds.Outskirts, TankIds.Tiger);
            _run.AddExperience(10);
            Assert.IsTrue(_loop.IsPausedBy(_run));

            _run.EndRun(RunOutcome.Defeat);
            Assert.IsFalse(_loop.IsPaused);
            Assert.AreEqual(0, _run.SkillOffer.Count);
            Assert.AreEqual(2, _run.Result.levelReached);
        }

        // ---------- Helpers ----------

        private RunService Create(GameCatalog catalog, bool withBuilder = true) =>
            new(catalog, _deaths, new System.Random(1), RunTimings.Default, withBuilder ? _builder : null, _loop,
                c => _completed.Contains(c),
                c => _bestArena.TryGetValue(c, out int best) ? best : 0,
                () => _leftToHangar++);

        private RunResult WinRich()
        {
            _run.StartRun(Rich, TankIds.Tiger);
            ToFight();
            _run.MarkArenaCleared();
            LeaveArena();
            ToFight();
            _run.MarkArenaCleared();
            _run.Tick(1.5f);
            Assert.AreEqual(RunState.Ended, _run.State);
            return _run.Result;
        }

        // Preparing → Fight: ready, the shutter opens (0.5 s), the lull (2 s)
        private void ToFight()
        {
            Assert.IsTrue(_run.MarkArenaReady());
            _run.Tick(0.1f);
            _run.Tick(0.5f);
            _run.Tick(2f);
        }

        // Cleared → the next arena's Preparing
        private void LeaveArena()
        {
            Assert.IsTrue(_run.MarkGateOpened());
            Assert.IsTrue(_run.MarkArenaLeft());
            _run.Tick(0.5f);
            _run.Tick(0.5f);
            Assert.AreEqual(RunState.Preparing, _run.State);
        }

        private void Die(EnemyKind kind, bool crushed = false, bool elite = false)
        {
            var death = new EnemyDeath(kind, Vector3.zero, crushed, elite);
            _deaths.Report(death);
        }

        private void Listen(RunService run)
        {
            run.RunStarted += () => _log.Add("RunStarted");
            run.ArenaStarted += n => _log.Add($"ArenaStarted({n})");
            run.FightStarted += n => _log.Add($"FightStarted({n})");
            run.ArenaCleared += n => _log.Add($"ArenaCleared({n})");
            run.GateOpened += n => _log.Add($"GateOpened({n})");
            run.ArenaLeft += n => _log.Add($"ArenaLeft({n})");
            run.ShutterChanged += closed => _log.Add($"Shutter({closed})");
            run.LootChanged += () => _log.Add("Loot");
            run.StateChanged += s => _log.Add($"State({s})");
            run.RunEnded += r => _log.Add($"RunEnded({r.outcome})");
        }

        private static bool Contains(IReadOnlyList<SkillId> list, SkillId skill) => CountOf(list, skill) > 0;

        private static int CountOf(IReadOnlyList<SkillId> list, SkillId skill)
        {
            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == skill)
                    count++;
            }

            return count;
        }

        private T Make<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }

        private ArenaConfig Arena(string id, ArenaKind kind)
        {
            var arena = Make<ArenaConfig>();
            arena.Configure(new ArenaId(id), kind);
            return arena;
        }

        private ChapterConfig Chapter(ChapterId id, float enemy, float reward, int firstMarks, int firstSamples,
            params ArenaConfig[] arenas)
        {
            var chapter = Make<ChapterConfig>();
            chapter.Configure(id, null, enemy, reward, firstMarks, firstSamples, null, arenas);
            return chapter;
        }

        private TankConfig Tank(TankId id, int weapons, int upgrades)
        {
            var tank = Make<TankConfig>();
            tank.Configure(id, true, weapons, upgrades);
            return tank;
        }

        private SkillConfig Skill(SkillId id, SkillGroup group, bool inPool = true)
        {
            var skill = Make<SkillConfig>();
            skill.Configure(id, group, inPool);
            return skill;
        }
    }
}
