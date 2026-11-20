using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// The «Мощный фугас» skill: its level becomes CombatModifiers.HeFactor, an HE shell reads it from the tank that fired
    /// it, and the hit's crater, debris speed, skeleton damage and gore throw scale with it; power 1 changes nothing.
    /// </summary>
    public sealed class ShellPowerUseCaseTests
    {
        private static IEntity Tank(float heFactor)
        {
            IEntity tank = new Entity();
            tank.AddCombatModifiers(new CombatModifiers { HeFactor = heFactor });
            return tank;
        }

        private static IEntity Shell(ShellKind? kind, IEntity owner)
        {
            IEntity shell = new Entity();
            shell.AddDamage(new ReactiveInt(150));
            shell.AddExplosionRadius(5f);
            shell.AddExplosionMinDamageFactor(0.25f);
            shell.AddMoveDirection(new ReactiveVariable<Vector3>(Vector3.forward));
            if (kind.HasValue)
                shell.AddShellAmmoKind(new Variable<ShellKind>(kind.Value));
            if (owner != null)
            {
                IEntity weapon = new Entity();
                weapon.AddWeaponOwner(owner);
                shell.AddBulletWeapon(new Variable<IEntity>(weapon));
            }

            return shell;
        }

        // ---------- The skill ----------

        [TestCase(0, 1f)]
        [TestCase(1, 1.3f)]
        [TestCase(2, 1.6f)]
        [TestCase(3, 1.9f)]
        [TestCase(4, 2.2f)]
        [TestCase(5, 2.5f)]
        public void Skill_AddsThirtyPercentALevel(int level, float factor)
        {
            var modifiers = new CombatModifiers();
            Assert.IsTrue(SkillEffectsUseCase.Apply(modifiers, SkillIds.HighExplosive, level, new SkillEffectTable()));
            Assert.AreEqual(factor, modifiers.HeFactor, 1e-4f);
        }

        [Test]
        public void Skill_IsOneOfTheSkillsCombatApplies_AndResetsToOne()
        {
            CollectionAssert.Contains(SkillEffectTable.MilestoneSkills, SkillIds.HighExplosive);
            var modifiers = new CombatModifiers { HeFactor = 2.5f };
            modifiers.Reset();
            Assert.AreEqual(1f, modifiers.HeFactor);
        }

        // ---------- The shell ----------

        [Test]
        public void HeShell_ReadsTheShootersFactor()
        {
            Assert.AreEqual(2.2f, ShellPowerUseCase.Of(Shell(ShellKind.HighExplosive, Tank(2.2f))), 1e-4f);
        }

        [Test]
        public void AnythingElse_HasPowerOne()
        {
            Assert.AreEqual(1f, ShellPowerUseCase.Of(Shell(ShellKind.ArmourPiercing, Tank(2.2f))), "an AP shell");
            Assert.AreEqual(1f, ShellPowerUseCase.Of(Shell(null, Tank(2.2f))), "an untyped explosive (a rocket, a bomb)");
            Assert.AreEqual(1f, ShellPowerUseCase.Of(Shell(ShellKind.HighExplosive, null)), "no shooter known");
            Assert.AreEqual(1f, ShellPowerUseCase.Of(Shell(ShellKind.HighExplosive, new Entity())), "a shooter without skills");
            Assert.AreEqual(1f, ShellPowerUseCase.Of(null));
        }

        // ---------- The bite ----------

        [Test]
        public void Bite_AtPowerOne_IsTheBlastWithoutTheSkill()
        {
            ExplosionUseCase.Bite(5f, true, 1f, out float crater, out float speed, out float skeleton);
            Assert.AreEqual(1.6f, crater, 1e-4f, "5 m × 0.32");
            Assert.AreEqual(9f, speed, 1e-4f);
            Assert.AreEqual(2f, skeleton, 1e-4f);
        }

        [Test]
        public void Bite_ScalesTheVolume_TheDebrisSpeed_AndTheSkeletonDamage()
        {
            ExplosionUseCase.Bite(5f, true, 1f, out float crater1, out float speed1, out float skeleton1);
            ExplosionUseCase.Bite(5f, true, 2.5f, out float crater5, out float speed5, out float skeleton5);

            Assert.AreEqual(ShellPowerUseCase.TOP_HOLE, crater5 / crater1, 1e-3f, "level 5: the hole ×2.3 as wide");
            Assert.AreEqual(2.5f, speed5 / speed1, 1e-4f, "debris ×2.5");
            Assert.AreEqual(2.5f, skeleton5 / skeleton1, 1e-4f, "skeleton damage ×2.5");
        }

        [Test]
        public void Bite_OfAnythingButHe_IgnoresThePower()
        {
            ExplosionUseCase.Bite(5f, false, 2.5f, out float crater, out float speed, out float skeleton);
            Assert.AreEqual(1.6f, crater, 1e-4f);
            Assert.AreEqual(9f, speed, 1e-4f);
            Assert.AreEqual(1f, skeleton, 1e-4f);
        }

        // ---------- The gore ----------

        [Test]
        public void KillingHit_CarriesThePower_AndTheBodyFliesThatMuchFaster()
        {
            var speeds = new GibSpeeds();
            KillingHit plain = GibRules.ReadHit(new TakeDamageArgs(Shell(ShellKind.HighExplosive, Tank(1f))), Vector3.zero);
            KillingHit strong = GibRules.ReadHit(new TakeDamageArgs(Shell(ShellKind.HighExplosive, Tank(2.5f))), Vector3.zero);

            Assert.AreEqual(1f, plain.Power, 1e-4f);
            Assert.AreEqual(2.5f, strong.Power, 1e-4f);

            GibRules.Throw(speeds, plain, out float speed1, out float lift1, out _);
            GibRules.Throw(speeds, strong, out float speed5, out float lift5, out _);
            Assert.AreEqual(2.5f, speed5 / speed1, 1e-4f);
            Assert.AreEqual(lift1, lift5, "the same arc, only faster");
        }

        [Test]
        public void KillingHit_ByDefault_HasPowerOne()
        {
            Assert.AreEqual(1f, new KillingHit(Vector3.zero, 10f, true, 1f).Power);
            Assert.AreEqual(1f, GibRules.ReadHit(null, Vector3.zero).Power);
        }
    }
}
