using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// The battle UI's adapter over the core's run: the skill offers come from IRunService.SkillOffered, and the
    /// adapter listens to it only while a skill screen listens to the adapter — without one (the sandbox) the run
    /// takes a skill by itself and nothing pauses.
    /// </summary>
    public sealed class RunServiceHudSourceTests
    {
        // ExperienceForLevel: 10 to level 2, 22 more to level 3
        private const int TO_LEVEL_2 = 10;
        private const int TO_LEVEL_3 = 10 + 22;

        private readonly List<Object> _assets = new();
        private readonly List<int> _offerSizes = new();

        private GameCatalog _catalog;
        private Ticking.GameLoop _loop;
        private RunService _run;
        private RunServiceHudSource _source;

        [SetUp]
        public void SetUp()
        {
            _offerSizes.Clear();

            var arenas = new ArenaConfig[5];
            for (int i = 0; i < arenas.Length; i++)
                arenas[i] = Arena("a" + (i + 1), i == 4 ? ArenaKind.Elite : ArenaKind.Horde);

            _catalog = Make<GameCatalog>();
            _catalog.Configure(
                new[] { Tank(TankIds.Tiger, 4, 6) },
                new ModuleConfig[0],
                new[]
                {
                    Skill(SkillIds.Turret, SkillGroup.Weapon),
                    Skill(SkillIds.AirStrike, SkillGroup.Weapon),
                    Skill(SkillIds.Ricochet, SkillGroup.Gun),
                    Skill(SkillIds.RateOfFire, SkillGroup.Gun),
                    Skill(SkillIds.Armor, SkillGroup.Tank),
                    Skill(SkillIds.Magnet, SkillGroup.Tank)
                },
                new[] { Chapter(ChapterIds.Outskirts, arenas) });

            _loop = new Ticking.GameLoop();
            _run = new RunService(_catalog, new EnemyDeaths(), new System.Random(1), RunTimings.Default, null, _loop);
            _source = new RunServiceHudSource(_run, _catalog, null);
            _run.StartRun(ChapterIds.Outskirts, TankIds.Tiger);
        }

        [TearDown]
        public void TearDown()
        {
            _source?.Dispose();
            _run?.Dispose();
            foreach (Object asset in _assets)
                Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void NoSkillScreen_LevelUp_RunTakesTheSkillItself_AndNothingPauses()
        {
            _run.AddExperience(TO_LEVEL_2);

            Assert.AreEqual(2, _source.Level);
            Assert.AreEqual(1, _source.Skills.Count, "the run took a skill from its offer");
            Assert.AreEqual(0, _run.PendingLevelUps);
            Assert.AreEqual(0, _source.PendingOffer.Count);
            Assert.IsFalse(_loop.IsPaused);
        }

        [Test]
        public void SkillScreen_LevelUp_OffersThreeCards_AndTheRunWaitsPaused()
        {
            _source.SkillOffered += RecordOffer;

            _run.AddExperience(TO_LEVEL_2);

            CollectionAssert.AreEqual(new[] { 3 }, _offerSizes);
            Assert.AreEqual(3, _source.PendingOffer.Count);
            Assert.AreEqual(0, _source.Skills.Count, "nothing is taken before the choice");
            Assert.AreEqual(1, _run.PendingLevelUps);
            Assert.IsTrue(_loop.IsPaused);
        }

        [Test]
        public void SkillScreen_ChooseSkill_TakesThePickedCard_AndTheGameGoesOn()
        {
            _source.SkillOffered += RecordOffer;
            _run.AddExperience(TO_LEVEL_2);
            string picked = _source.PendingOffer[1].Id;

            _source.ChooseSkill(1);

            Assert.AreEqual(1, _source.Skills.Count);
            Assert.AreEqual(picked, _source.Skills[0].Id);
            Assert.AreEqual(1, _run.GetSkillLevel(new SkillId(picked)));
            Assert.AreEqual(0, _source.PendingOffer.Count);
            Assert.IsFalse(_loop.IsPaused);
        }

        [Test]
        public void SkillScreen_TwoLevelUps_TheNextOfferComesOnceFromInsideChooseSkill()
        {
            _source.SkillOffered += RecordOffer;
            _run.AddExperience(TO_LEVEL_3);
            Assert.AreEqual(1, _offerSizes.Count, "one offer at a time");

            _source.ChooseSkill(0);

            Assert.AreEqual(2, _offerSizes.Count, "the second level-up is offered once");
            Assert.AreEqual(3, _source.PendingOffer.Count);
            Assert.IsTrue(_loop.IsPaused, "still waiting for the second choice");

            _source.ChooseSkill(0);

            Assert.AreEqual(2, _offerSizes.Count);
            Assert.AreEqual(0, _source.PendingOffer.Count);
            Assert.AreEqual(0, _run.PendingLevelUps);
            Assert.IsFalse(_loop.IsPaused);
        }

        [Test]
        public void SkillScreen_Gone_TheRunTakesSkillsItselfAgain()
        {
            _source.SkillOffered += RecordOffer;
            _source.SkillOffered -= RecordOffer;

            _run.AddExperience(TO_LEVEL_2);

            CollectionAssert.IsEmpty(_offerSizes);
            Assert.AreEqual(1, _source.Skills.Count);
            Assert.IsFalse(_loop.IsPaused);
        }

        [Test]
        public void Disposed_StopsListeningToTheRun()
        {
            _source.SkillOffered += RecordOffer;
            _source.Dispose();
            _source = null;

            _run.AddExperience(TO_LEVEL_2);

            CollectionAssert.IsEmpty(_offerSizes);
            Assert.AreEqual(0, _run.PendingLevelUps, "nobody listens: the run took the skill");
            Assert.IsFalse(_loop.IsPaused);
        }

        [Test]
        public void ChooseSkill_WithoutAnOffer_DoesNothing()
        {
            _source.SkillOffered += RecordOffer;

            _source.ChooseSkill(0);

            Assert.AreEqual(0, _source.Skills.Count);
            CollectionAssert.IsEmpty(_offerSizes);
        }

        private void RecordOffer(IReadOnlyList<SkillOffer> offers) => _offerSizes.Add(offers.Count);

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

        private ChapterConfig Chapter(ChapterId id, params ArenaConfig[] arenas)
        {
            var chapter = Make<ChapterConfig>();
            chapter.Configure(id, null, 1f, 1f, 1000, 10, null, arenas);
            return chapter;
        }

        private TankConfig Tank(TankId id, int weapons, int upgrades)
        {
            var tank = Make<TankConfig>();
            tank.Configure(id, true, weapons, upgrades);
            return tank;
        }

        private SkillConfig Skill(SkillId id, SkillGroup group)
        {
            var skill = Make<SkillConfig>();
            skill.Configure(id, group, true);
            return skill;
        }
    }
}
