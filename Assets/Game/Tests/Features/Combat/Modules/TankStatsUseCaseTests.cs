using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class TankStatsUseCaseTests
    {
        private const float EPS = 1e-3f;
        private const float G = ModuleEffectDefaults.LEVEL_GROWTH;

        private static TankCombatStats Stats(params ModuleLevel[] loadout) =>
            TankStatsUseCase.Compute(null, loadout, new TankBaseSettings(), null);

        private static ModuleLevel M(ModuleId id, int level = 1) => new(id, level);

        private static List<ModuleLevel> DefaultLoadout()
        {
            List<ModuleLevel> loadout = new();
            TankStatsUseCase.ResolveLoadout(null, null, loadout);
            return loadout;
        }

        [Test]
        public void DefaultLoadout_WithoutASave_IsTheSixStandardModulesAtLevelOne()
        {
            List<ModuleLevel> loadout = DefaultLoadout();

            Assert.That(loadout.Count, Is.EqualTo(6));
            Assert.That(loadout[0].Id, Is.EqualTo(ModuleIds.Gun88He));
            Assert.That(loadout[1].Id, Is.EqualTo(ModuleIds.MachineGun));
            Assert.That(loadout[2].Id, Is.EqualTo(ModuleIds.ArmorStandard));
            Assert.That(loadout[3].Id, Is.EqualTo(ModuleIds.EngineStandard));
            Assert.That(loadout[4].Id, Is.EqualTo(ModuleIds.TracksStandard));
            Assert.That(loadout[5].Id, Is.EqualTo(ModuleIds.SpecialSmoke));
            foreach (ModuleLevel module in loadout)
                Assert.That(module.Level, Is.EqualTo(1));
        }

        [Test]
        public void DefaultLoadout_GivesTheTigerOfGdd4()
        {
            TankCombatStats s = TankStatsUseCase.Compute(null, DefaultLoadout(), new TankBaseSettings(), null);

            Assert.That(s.Health, Is.EqualTo(1000f).Within(EPS));
            Assert.That(s.Speed, Is.EqualTo(6f).Within(EPS));
            Assert.That(s.BoostSpeed, Is.EqualTo(10.8f).Within(EPS));
            Assert.That(s.TurretSpeed, Is.EqualTo(90f).Within(EPS));
            Assert.That(s.CannonDamage, Is.EqualTo(150f).Within(EPS));
            Assert.That(s.CannonRadius, Is.EqualTo(5f).Within(EPS));
            Assert.That(s.CannonReload, Is.EqualTo(2f).Within(EPS));
            Assert.That(s.CannonPierce, Is.EqualTo(0));
            Assert.That(s.MgDamage, Is.EqualTo(10f).Within(EPS));
            Assert.That(s.MgInterval, Is.EqualTo(0.16f).Within(EPS));
            Assert.That(s.MgSpread, Is.EqualTo(2f).Within(EPS));
            Assert.That(s.MgRange, Is.EqualTo(18f).Within(EPS));
            Assert.That(s.CrushSpeed, Is.EqualTo(1.5f).Within(EPS));
            Assert.That(s.CrushWidthFactor, Is.EqualTo(1f).Within(EPS));
            Assert.That(s.SpecialDuration, Is.EqualTo(12f).Within(EPS));
            Assert.That(s.SpecialCooldown, Is.EqualTo(30f).Within(EPS));
        }

        [Test]
        public void NoTank_TakesHealthSpeedAndTurretFromTheBaseSettings()
        {
            TankBaseSettings settings = new() { Health = 1200f, Speed = 5f, BoostSpeed = 9f, TurretSpeed = 60f };

            TankCombatStats s = TankStatsUseCase.Compute(null, new ModuleLevel[0], settings, null);

            Assert.That(s.Health, Is.EqualTo(1200f).Within(EPS));
            Assert.That(s.Speed, Is.EqualTo(5f).Within(EPS));
            Assert.That(s.BoostSpeed, Is.EqualTo(9f).Within(EPS));
            Assert.That(s.TurretSpeed, Is.EqualTo(60f).Within(EPS));
        }

        [TestCase(1, 150f, 5f)]
        [TestCase(2, 150f * G, 5f * G)]
        [TestCase(3, 150f * G * G, 5f * G * G)]
        public void HeGun_LevelsRaiseDamageAndRadius(int level, float damage, float radius)
        {
            TankCombatStats s = Stats(M(ModuleIds.Gun88He, level));

            Assert.That(s.CannonDamage, Is.EqualTo(damage).Within(EPS));
            Assert.That(s.CannonRadius, Is.EqualTo(radius).Within(EPS));
            Assert.That(s.CannonReload, Is.EqualTo(2f).Within(EPS));
            Assert.That(s.CannonPierce, Is.EqualTo(0));
        }

        [TestCase(1, 250f, 1.6f)]
        [TestCase(2, 250f * G, 1.6f / G)]
        [TestCase(3, 250f * G * G, 1.6f / (G * G))]
        public void ArmorPiercingGun_LevelsRaiseDamageAndShortenReload(int level, float damage, float reload)
        {
            TankCombatStats s = Stats(M(ModuleIds.GunArmorPiercing, level));

            Assert.That(s.CannonDamage, Is.EqualTo(damage).Within(EPS));
            Assert.That(s.CannonReload, Is.EqualTo(reload).Within(EPS));
            Assert.That(s.CannonRadius, Is.EqualTo(2f).Within(EPS));
            Assert.That(s.CannonPierce, Is.EqualTo(3));
        }

        [TestCase(1, 10f, 0.16f)]
        [TestCase(2, 10f * G, 0.16f / G)]
        [TestCase(3, 10f * G * G, 0.16f / (G * G))]
        public void MachineGun_LevelsRaiseDamageAndRate(int level, float damage, float interval)
        {
            TankCombatStats s = Stats(M(ModuleIds.MachineGun, level));

            Assert.That(s.MgDamage, Is.EqualTo(damage).Within(EPS));
            Assert.That(s.MgInterval, Is.EqualTo(interval).Within(1e-5f));
            Assert.That(s.MgSpread, Is.EqualTo(2f).Within(EPS));
            Assert.That(s.MgRange, Is.EqualTo(18f).Within(EPS));
        }

        [TestCase(1, 1000f)]
        [TestCase(2, 1150f)]
        [TestCase(3, 1300f)]
        public void StandardArmor_GivesFifteenPercentHealthALevelAboveTheFirst(int level, float health)
        {
            Assert.That(Stats(M(ModuleIds.ArmorStandard, level)).Health, Is.EqualTo(health).Within(EPS));
        }

        [TestCase(1, 0.4f, 0.16f)]
        [TestCase(2, 0.4f * G, 0.16f / G)]
        [TestCase(3, 0.4f * G * G, 0.16f / (G * G))]
        public void Screens_AddHealthAndTakeSpeed_LevelsGrowTheBonusAndShrinkThePenalty(int level, float bonus,
            float penalty)
        {
            TankCombatStats s = Stats(M(ModuleIds.ArmorSkirts, level));

            Assert.That(s.Health, Is.EqualTo(1000f * (1f + bonus)).Within(EPS));
            Assert.That(s.Speed, Is.EqualTo(6f * (1f - penalty)).Within(EPS));
            Assert.That(s.BoostSpeed, Is.EqualTo(10.8f * (1f - penalty)).Within(EPS));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void StandardEngine_LevelsRaiseSpeedAndBoost(int level)
        {
            float f = level == 1 ? 1f : level == 2 ? G : G * G;
            TankCombatStats s = Stats(M(ModuleIds.EngineStandard, level));

            Assert.That(s.Speed, Is.EqualTo(6f * f).Within(EPS));
            Assert.That(s.BoostSpeed, Is.EqualTo(10.8f * f).Within(EPS));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void StandardTracks_LevelsWidenTheCrushAndLowerItsThreshold(int level)
        {
            float f = level == 1 ? 1f : level == 2 ? G : G * G;
            TankCombatStats s = Stats(M(ModuleIds.TracksStandard, level));

            Assert.That(s.CrushWidthFactor, Is.EqualTo(f).Within(EPS));
            Assert.That(s.CrushSpeed, Is.EqualTo(1.5f / f).Within(EPS));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void SmokeLaunchers_LevelsLengthenTheSmokeAndShortenTheReload(int level)
        {
            float f = level == 1 ? 1f : level == 2 ? G : G * G;
            TankCombatStats s = Stats(M(ModuleIds.SpecialSmoke, level));

            Assert.That(s.SpecialDuration, Is.EqualTo(12f * f).Within(EPS));
            Assert.That(s.SpecialCooldown, Is.EqualTo(30f / f).Within(EPS));
        }

        [Test]
        public void NoSpecialModule_HasNoSpecialNumbers()
        {
            TankCombatStats s = Stats(M(ModuleIds.Gun88He));

            Assert.That(s.SpecialDuration, Is.EqualTo(0f));
            Assert.That(s.SpecialCooldown, Is.EqualTo(0f));
        }

        [Test]
        public void ScreensAndEngine_Multiply_InAnyOrder()
        {
            TankCombatStats a = Stats(M(ModuleIds.ArmorSkirts), M(ModuleIds.EngineStandard, 2));
            TankCombatStats b = Stats(M(ModuleIds.EngineStandard, 2), M(ModuleIds.ArmorSkirts));

            Assert.That(a.Speed, Is.EqualTo(6f * G * 0.92f).Within(EPS));
            Assert.That(b.Speed, Is.EqualTo(a.Speed).Within(EPS));
            Assert.That(a.Health, Is.EqualTo(1400f).Within(EPS));
        }

        [Test]
        public void ALevelOutOfRange_IsClampedToOneToThree()
        {
            Assert.That(Stats(M(ModuleIds.ArmorStandard, 0)).Health, Is.EqualTo(1000f).Within(EPS));
            Assert.That(Stats(M(ModuleIds.ArmorStandard, 7)).Health, Is.EqualTo(1300f).Within(EPS));
        }

        [Test]
        public void AnUnknownModule_ChangesNothing()
        {
            TankCombatStats bare = Stats();
            TankCombatStats s = Stats(M(new ModuleId("no_such_module"), 2));

            Assert.That(s.Health, Is.EqualTo(bare.Health));
            Assert.That(s.Speed, Is.EqualTo(bare.Speed));
            Assert.That(s.CannonDamage, Is.EqualTo(bare.CannonDamage));
        }

        [Test]
        public void AConfiguredModule_OverridesItsCodeDefault()
        {
            ModuleEffectConfig armor = new(ModuleIds.ArmorStandard, ModuleSlot.Armor,
                new ModuleStatEffect(ModuleStat.Health, ModuleStatOp.Multiply, new[] { 2f, 3f, 4f }));

            TankCombatStats s = TankStatsUseCase.Compute(null, new[] { M(ModuleIds.ArmorStandard, 2) },
                new TankBaseSettings(), new[] { armor });

            Assert.That(s.Health, Is.EqualTo(3000f).Within(EPS));
        }

        [Test]
        public void AModuleMissingFromTheConfig_TakesItsCodeDefault()
        {
            ModuleEffectConfig armor = new(ModuleIds.ArmorStandard, ModuleSlot.Armor);

            TankCombatStats s = TankStatsUseCase.Compute(null, new[] { M(ModuleIds.EngineStandard, 2) },
                new TankBaseSettings(), new[] { armor });

            Assert.That(s.Speed, Is.EqualTo(6f * G).Within(EPS));
        }
    }
}
