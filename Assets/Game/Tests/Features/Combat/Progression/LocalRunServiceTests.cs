using System.Collections.Generic;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class LocalRunServiceTests
    {
        [TestCase(1, 10)]
        [TestCase(2, 22)]
        [TestCase(5, 58)]
        public void Curve_Is10Plus12PerLevel(int level, int needed)
        {
            Assert.AreEqual(needed, LocalRunService.ExperienceFor(level));
        }

        [Test]
        public void Experience_BelowTheLevel_OffersNothing()
        {
            var run = new LocalRunService(seed: 1);
            run.AddExperience(9);

            Assert.AreEqual(1, run.Level);
            Assert.AreEqual(9, run.Experience);
            Assert.AreEqual(0, run.PendingLevelUps);
            Assert.AreEqual(0, run.SkillOffer.Count);
        }

        [Test]
        public void LevelUp_OffersThreeDistinctSkills()
        {
            var run = new LocalRunService(seed: 1);
            var levels = new List<int>();
            run.LevelUp += levels.Add;

            run.AddExperience(12);

            Assert.AreEqual(2, run.Level);
            Assert.AreEqual(2, run.Experience);
            Assert.AreEqual(22, run.ExperienceToNextLevel);
            Assert.AreEqual(1, run.PendingLevelUps);
            Assert.AreEqual(3, run.SkillOffer.Count);
            CollectionAssert.AllItemsAreUnique(run.SkillOffer);
            CollectionAssert.AreEqual(new[] { 2 }, levels);
        }

        [Test]
        public void SeveralLevelsAtOnce_AreQueued_EachOfferedInTurn()
        {
            var run = new LocalRunService(seed: 3);
            var levels = new List<int>();
            run.LevelUp += levels.Add;

            run.AddExperience(10 + 22 + 34);

            Assert.AreEqual(4, run.Level);
            Assert.AreEqual(3, run.PendingLevelUps);

            run.ChooseSkill(run.SkillOffer[0]);
            Assert.AreEqual(2, run.PendingLevelUps);
            Assert.AreEqual(3, run.SkillOffer.Count);

            run.ChooseSkill(run.SkillOffer[0]);
            run.ChooseSkill(run.SkillOffer[0]);
            Assert.AreEqual(0, run.PendingLevelUps);
            Assert.AreEqual(0, run.SkillOffer.Count);
            CollectionAssert.AreEqual(new[] { 2, 3, 4 }, levels);
        }

        [Test]
        public void Choose_RaisesSkillChanged_AndLevelsTheSkillUp()
        {
            var run = new LocalRunService(seed: 5);
            var changes = new List<(SkillId, int)>();
            run.SkillChanged += (skill, level) => changes.Add((skill, level));

            run.AddExperience(10);
            SkillId skill = run.SkillOffer[1];
            Assert.IsTrue(run.ChooseSkill(skill));

            Assert.AreEqual(1, run.GetSkillLevel(skill));
            CollectionAssert.AreEqual(new[] { skill }, run.Skills);
            Assert.AreEqual((skill, 1), changes[0]);
        }

        [Test]
        public void Choose_WithoutAnOffer_OrOffTheOffer_IsRefused()
        {
            var run = new LocalRunService(seed: 5);
            Assert.IsFalse(run.ChooseSkill(SkillIds.Turret));

            run.AddExperience(10);
            SkillId missing = default;
            foreach (SkillId skill in SkillEffectTable.MilestoneSkills)
                if (!Contains(run.SkillOffer, skill))
                    missing = skill;

            Assert.IsFalse(run.ChooseSkill(missing));
            Assert.AreEqual(1, run.PendingLevelUps);
        }

        [Test]
        public void MaxedSkill_IsNoLongerOffered_AndAllMaxed_LevelUpsPass()
        {
            SkillId[] pool = { SkillIds.Turret };
            var run = new LocalRunService(pool, new[] { SkillGroup.Weapon }, null, 9);

            for (int i = 0; i < SkillConfig.MAX_LEVEL; i++)
            {
                run.AddExperience(run.ExperienceToNextLevel);
                Assert.AreEqual(1, run.SkillOffer.Count);
                run.ChooseSkill(SkillIds.Turret);
            }

            Assert.AreEqual(5, run.GetSkillLevel(SkillIds.Turret));
            run.AddExperience(run.ExperienceToNextLevel);
            Assert.AreEqual(0, run.SkillOffer.Count);
            Assert.AreEqual(0, run.PendingLevelUps);
        }

        [Test]
        public void FourWeaponSlots_AFifthWeaponIsNeverOffered()
        {
            SkillId[] weapons = { SkillIds.Turret, SkillIds.SideFlamers, SkillIds.Mortar, SkillIds.Rockets, SkillIds.RearMines };
            var groups = new[] { SkillGroup.Weapon, SkillGroup.Weapon, SkillGroup.Weapon, SkillGroup.Weapon, SkillGroup.Weapon };
            var run = new LocalRunService(weapons, groups, null, 11);

            // Take new weapons while any is offered, until four are taken
            while (run.Skills.Count < LocalRunService.WEAPON_SLOTS)
            {
                run.AddExperience(run.ExperienceToNextLevel);
                SkillId pick = run.SkillOffer[0];
                for (int i = 0; i < run.SkillOffer.Count; i++)
                    if (run.GetSkillLevel(run.SkillOffer[i]) == 0)
                        pick = run.SkillOffer[i];
                run.ChooseSkill(pick);
            }

            for (int n = 0; n < 12; n++)
            {
                run.AddExperience(run.ExperienceToNextLevel);
                for (int i = 0; i < run.SkillOffer.Count; i++)
                    Assert.Greater(run.GetSkillLevel(run.SkillOffer[i]), 0, "only the taken weapons level up");
                if (run.SkillOffer.Count > 0)
                    run.ChooseSkill(run.SkillOffer[0]);
            }

            Assert.AreEqual(LocalRunService.WEAPON_SLOTS, run.Skills.Count);
        }

        [Test]
        public void SixUpgradeSlots_SharedByGunAndTank()
        {
            SkillId[] pool =
            {
                SkillIds.Ricochet, SkillIds.DoubleShot, SkillIds.Piercing, SkillIds.BigCaliber,
                SkillIds.Armor, SkillIds.Magnet, SkillIds.Regeneration, SkillIds.Luck
            };
            var groups = new SkillGroup[pool.Length];
            for (int i = 0; i < pool.Length; i++)
                groups[i] = LocalRunService.DefaultGroupOf(pool[i]);
            var run = new LocalRunService(pool, groups, null, 13);

            for (int n = 0; n < 40; n++)
            {
                run.AddExperience(run.ExperienceToNextLevel);
                if (run.SkillOffer.Count == 0)
                    break;

                SkillId pick = run.SkillOffer[0];
                for (int i = 0; i < run.SkillOffer.Count; i++)
                    if (run.GetSkillLevel(run.SkillOffer[i]) == 0)
                        pick = run.SkillOffer[i];
                run.ChooseSkill(pick);
                Assert.LessOrEqual(run.Skills.Count, LocalRunService.UPGRADE_SLOTS);
            }

            Assert.AreEqual(LocalRunService.UPGRADE_SLOTS, run.Skills.Count);
        }

        [Test]
        public void EndRun_RaisesRunEndedOnce_AndStopsExperience()
        {
            var run = new LocalRunService(seed: 2);
            run.AddExperience(10);
            int ended = 0;
            RunResult result = default;
            run.RunEnded += r =>
            {
                ended++;
                result = r;
            };

            Assert.IsTrue(run.EndRun(RunOutcome.Defeat));
            Assert.IsFalse(run.EndRun(RunOutcome.Defeat));
            run.AddExperience(100);

            Assert.AreEqual(1, ended);
            Assert.AreEqual(RunOutcome.Defeat, result.outcome);
            Assert.AreEqual(2, result.levelReached);
            Assert.AreEqual(RunState.Ended, run.State);
            Assert.AreEqual(0, run.SkillOffer.Count);
            Assert.AreEqual(2, run.Level);
        }

        [Test]
        public void Deaths_AreCounted()
        {
            var deaths = new EnemyDeaths();
            var run = new LocalRunService(deaths, 1);

            deaths.Report(new EnemyDeath(EnemyKind.Crawler, Vector3.zero, true, false));
            deaths.Report(new EnemyDeath(EnemyKind.EliteRegax, Vector3.zero, false, true));
            run.Dispose();
            deaths.Report(new EnemyDeath(EnemyKind.Crawler, Vector3.zero, false, false));

            Assert.AreEqual(2, run.Kills);
            Assert.AreEqual(1, run.Crushes);
            Assert.AreEqual(1, run.ElitesKilled);
        }

        [Test]
        public void TankDeath_EndsTheRunAsDefeat_Once()
        {
            var run = new LocalRunService(seed: 4);
            int ended = 0;
            run.RunEnded += _ => ended++;

            IEntity tank = new Entity();
            var health = new Health(10, 10);
            tank.AddHealth(health);
            var behaviour = new TankDefeatBehaviour(run);
            behaviour.Init(tank);

            health.Reduce(10);
            health.SetCurrent(5);
            health.SetCurrent(0);

            Assert.AreEqual(1, ended);
            Assert.AreEqual(RunOutcome.Defeat, run.Result.outcome);
        }

        [Test]
        public void TankDeath_InANewRun_EndsThatRunToo()
        {
            var run = new LocalRunService(seed: 4);
            int ended = 0;
            run.RunEnded += _ => ended++;

            IEntity tank = new Entity();
            var health = new Health(10, 10);
            tank.AddHealth(health);
            var behaviour = new TankDefeatBehaviour(run);
            behaviour.Init(tank);

            health.Reduce(10);
            Assert.AreEqual(1, ended);

            run.StartRun(ChapterIds.Outskirts, TankIds.Tiger);
            health.SetCurrent(10);
            health.Reduce(10);

            Assert.AreEqual(2, ended, "the defeat is armed again by RunStarted");
            Assert.AreEqual(RunState.Ended, run.State);

            behaviour.Dispose(tank);
            run.StartRun(ChapterIds.Outskirts, TankIds.Tiger);
            health.SetCurrent(10);
            health.Reduce(10);
            Assert.AreEqual(2, ended, "no longer listening after Dispose");
        }

        private static bool Contains(IReadOnlyList<SkillId> list, SkillId skill)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == skill)
                    return true;
            return false;
        }
    }
}
