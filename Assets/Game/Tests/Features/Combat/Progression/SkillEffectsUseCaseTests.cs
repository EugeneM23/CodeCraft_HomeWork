using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class SkillEffectsUseCaseTests
    {
        private static CombatModifiers Apply(SkillId skill, int level, SkillEffectTable table = null)
        {
            var modifiers = new CombatModifiers();
            Assert.IsTrue(SkillEffectsUseCase.Apply(modifiers, skill, level, table ?? new SkillEffectTable()));
            return modifiers;
        }

        [TestCase(1, 1, 1f)]
        [TestCase(2, 1, 1.2f)]
        [TestCase(3, 2, 1.2f)]
        [TestCase(4, 2, 1.44f)]
        [TestCase(5, 3, 1.44f)]
        public void Turret_CountAndDamagePerLevel(int level, int turrets, float damage)
        {
            CombatModifiers m = Apply(SkillIds.Turret, level);
            Assert.AreEqual(turrets, m.TurretCount);
            Assert.AreEqual(damage, m.TurretDamageFactor, 1e-4f);
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        public void AirStrikeAndRicochet_FollowTheLevel(int level)
        {
            Assert.AreEqual(level, Apply(SkillIds.AirStrike, level).AirStrikeLevel);
            Assert.AreEqual(level, Apply(SkillIds.Ricochet, level).RicochetBounces);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void FactorsPerLevel(int level)
        {
            Assert.AreEqual(1f - (0.12f + 0.08f * (level - 1)), Apply(SkillIds.RateOfFire, level).CannonReloadFactor, 1e-4f);
            Assert.AreEqual(1f + 0.15f * level, Apply(SkillIds.Armor, level).MaxHealthFactor, 1e-4f);
            Assert.AreEqual(1.5f + 0.3f * (level - 1), Apply(SkillIds.Magnet, level).PickupRadiusFactor, 1e-4f);
        }

        [Test]
        public void LevelZero_PutsTheSkillBackToNeutral()
        {
            var table = new SkillEffectTable();
            var m = new CombatModifiers();
            SkillEffectsUseCase.Apply(m, SkillIds.Turret, 5, table);
            SkillEffectsUseCase.Apply(m, SkillIds.Magnet, 5, table);

            SkillEffectsUseCase.Apply(m, SkillIds.Turret, 0, table);
            SkillEffectsUseCase.Apply(m, SkillIds.Magnet, 0, table);

            Assert.AreEqual(0, m.TurretCount);
            Assert.AreEqual(1f, m.TurretDamageFactor);
            Assert.AreEqual(1f, m.PickupRadiusFactor);
        }

        [Test]
        public void UnknownSkill_ChangesNothing()
        {
            var m = new CombatModifiers();
            Assert.IsFalse(SkillEffectsUseCase.Apply(m, SkillIds.Luck, 3, new SkillEffectTable()));
            Assert.AreEqual(1f, m.CannonReloadFactor);
        }

        [Test]
        public void Config_OverridesTheDefaults_AndAShortRowIsIgnored()
        {
            var magnet = ScriptableObject.CreateInstance<SkillEffectConfig>();
            magnet.Configure(SkillIds.Magnet, new[] { 2f, 3f, 4f, 5f, 6f });
            var armor = ScriptableObject.CreateInstance<SkillEffectConfig>();
            armor.Configure(SkillIds.Armor, new[] { 9f });
            try
            {
                var table = new SkillEffectTable(new[] { magnet, armor, null });
                Assert.AreEqual(4f, Apply(SkillIds.Magnet, 3, table).PickupRadiusFactor);
                Assert.AreEqual(1.45f, Apply(SkillIds.Armor, 3, table).MaxHealthFactor, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(magnet);
                Object.DestroyImmediate(armor);
            }
        }

        [Test]
        public void Behaviour_AppliesCurrentLevelsAtInit_ThenFollowsSkillChanged()
        {
            var run = new LocalRunService(seed: 7);
            run.AddExperience(10);
            run.ChooseSkill(run.SkillOffer[0]);

            IEntity tank = new Entity();
            var modifiers = new CombatModifiers();
            tank.AddCombatModifiers(modifiers);
            int changed = 0;
            modifiers.Changed += () => changed++;

            var behaviour = new SkillEffectsBehaviour(run, new SkillEffectTable());
            behaviour.Init(tank);
            Assert.AreEqual(1, changed);
            AssertMatchesRun(modifiers, run);

            run.AddExperience(run.ExperienceToNextLevel);
            run.ChooseSkill(run.SkillOffer[0]);
            Assert.AreEqual(2, changed);
            AssertMatchesRun(modifiers, run);

            behaviour.Dispose(tank);
            run.AddExperience(run.ExperienceToNextLevel);
            run.ChooseSkill(run.SkillOffer[0]);
            Assert.AreEqual(2, changed, "no longer listening after Dispose");
        }

        [Test]
        public void Behaviour_NewRun_PutsTheModifiersBackToNeutral()
        {
            var run = new LocalRunService(seed: 7);
            IEntity tank = new Entity();
            var modifiers = new CombatModifiers();
            tank.AddCombatModifiers(modifiers);
            var behaviour = new SkillEffectsBehaviour(run, new SkillEffectTable());
            behaviour.Init(tank);

            for (int i = 0; i < 4; i++)
            {
                run.AddExperience(run.ExperienceToNextLevel);
                run.ChooseSkill(run.SkillOffer[0]);
            }

            Assert.That(run.Skills.Count, Is.GreaterThan(0));
            AssertMatchesRun(modifiers, run);

            int changed = 0;
            modifiers.Changed += () => changed++;
            run.StartRun(ChapterIds.Outskirts, TankIds.Tiger);

            Assert.AreEqual(0, run.Skills.Count, "the run cleared its skills without SkillChanged");
            Assert.That(changed, Is.GreaterThan(0));
            AssertNeutral(modifiers);

            // Skills of the new run apply as before.
            run.AddExperience(run.ExperienceToNextLevel);
            run.ChooseSkill(run.SkillOffer[0]);
            AssertMatchesRun(modifiers, run);

            behaviour.Dispose(tank);
        }

        private static void AssertNeutral(CombatModifiers actual)
        {
            var neutral = new CombatModifiers();
            Assert.AreEqual(neutral.TurretCount, actual.TurretCount);
            Assert.AreEqual(neutral.TurretDamageFactor, actual.TurretDamageFactor, 1e-4f);
            Assert.AreEqual(neutral.AirStrikeLevel, actual.AirStrikeLevel);
            Assert.AreEqual(neutral.RicochetBounces, actual.RicochetBounces);
            Assert.AreEqual(neutral.CannonReloadFactor, actual.CannonReloadFactor, 1e-4f);
            Assert.AreEqual(neutral.MaxHealthFactor, actual.MaxHealthFactor, 1e-4f);
            Assert.AreEqual(neutral.PickupRadiusFactor, actual.PickupRadiusFactor, 1e-4f);
        }

        private static void AssertMatchesRun(CombatModifiers actual, IRunService run)
        {
            var table = new SkillEffectTable();
            var expected = new CombatModifiers();
            for (int i = 0; i < run.Skills.Count; i++)
                SkillEffectsUseCase.Apply(expected, run.Skills[i], run.GetSkillLevel(run.Skills[i]), table);

            Assert.AreEqual(expected.TurretCount, actual.TurretCount);
            Assert.AreEqual(expected.AirStrikeLevel, actual.AirStrikeLevel);
            Assert.AreEqual(expected.RicochetBounces, actual.RicochetBounces);
            Assert.AreEqual(expected.CannonReloadFactor, actual.CannonReloadFactor, 1e-4f);
            Assert.AreEqual(expected.MaxHealthFactor, actual.MaxHealthFactor, 1e-4f);
            Assert.AreEqual(expected.PickupRadiusFactor, actual.PickupRadiusFactor, 1e-4f);
        }
    }
}
