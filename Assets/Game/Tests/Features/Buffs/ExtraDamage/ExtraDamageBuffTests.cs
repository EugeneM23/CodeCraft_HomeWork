using System.Reflection;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class ExtraDamageBuffTests
    {
        private ExtraDamageBuffConfig _config;

        [SetUp]
        public void SetUp()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            _config = ScriptableObject.CreateInstance<ExtraDamageBuffConfig>();
            typeof(BuffConfig).GetField("<Name>k__BackingField", flags).SetValue(_config, "ExtraDamage");
            typeof(BuffConfig).GetField("<Duration>k__BackingField", flags).SetValue(_config, 1f);
            typeof(ExtraDamageBuffConfig).GetField("<Damage>k__BackingField", flags).SetValue(_config, 5);
            typeof(ExtraDamageBuffConfig).GetField("<CooldownFactor>k__BackingField", flags).SetValue(_config, 0.5f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        private static IEntity Weapon(int damage = 10, float cooldown = 1f)
        {
            IEntity weapon = new Entity();
            weapon.AddDamage(new ReactiveInt(damage));
            weapon.AddWeaponCooldown(new CooldownTimer(cooldown));
            return weapon;
        }

        private static IEntity Character(IEntity weapon)
        {
            IEntity character = new Entity();
            character.AddWeapon(new ReactiveVariable<IEntity>(weapon));
            character.AddBuffsEffects(new ReactiveList<BuffBase>());
            return character;
        }

        [Test]
        public void Apply_AddsDamageAndScalesCooldown()
        {
            IEntity weapon = Weapon();
            IEntity character = Character(weapon);

            bool applied = BuffUseCase.Apply(character, _config.CreateBuff());

            Assert.That(applied, Is.True);
            Assert.That(weapon.GetDamage().Value, Is.EqualTo(15));
            Assert.That(weapon.GetWeaponCooldown().Duration, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void Discard_RestoresDamageAndCooldown()
        {
            IEntity weapon = Weapon();
            IEntity character = Character(weapon);
            BuffBase buff = _config.CreateBuff();
            BuffUseCase.Apply(character, buff);

            BuffUseCase.Discard(character, buff);

            Assert.That(weapon.GetDamage().Value, Is.EqualTo(10));
            Assert.That(weapon.GetWeaponCooldown().Duration, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Apply_NoWeaponInHand_RefusedAndNotRecorded()
        {
            IEntity character = Character(null);

            bool applied = BuffUseCase.Apply(character, _config.CreateBuff());

            Assert.That(applied, Is.False);
            Assert.That(character.GetBuffsEffects(), Is.Empty);
        }

        [Test]
        public void WeaponSwappedWhileActive_BonusLeavesTheBuffedWeaponAndSparesTheNewOne()
        {
            IEntity oldWeapon = Weapon();
            IEntity newWeapon = Weapon(damage: 20, cooldown: 2f);
            IEntity character = Character(oldWeapon);
            BuffBase buff = _config.CreateBuff();
            BuffUseCase.Apply(character, buff);

            character.GetWeapon().Value = newWeapon;
            BuffUseCase.Discard(character, buff);

            Assert.That(oldWeapon.GetDamage().Value, Is.EqualTo(10));
            Assert.That(oldWeapon.GetWeaponCooldown().Duration, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(newWeapon.GetDamage().Value, Is.EqualTo(20));
            Assert.That(newWeapon.GetWeaponCooldown().Duration, Is.EqualTo(2f).Within(1e-5f));
        }

        [Test]
        public void WeaponDroppedWhileActive_ExpiryStillRestoresTheWeapon()
        {
            IEntity weapon = Weapon();
            IEntity character = Character(weapon);
            var buff = (TemporaryBuff)_config.CreateBuff();
            BuffUseCase.Apply(character, buff);

            character.GetWeapon().Value = null;
            buff.FixedTick(character, 1f);

            Assert.That(character.GetBuffsEffects(), Is.Empty);
            Assert.That(character.HasBehaviour(buff), Is.False);
            Assert.That(weapon.GetDamage().Value, Is.EqualTo(10));
            Assert.That(weapon.GetWeaponCooldown().Duration, Is.EqualTo(1f).Within(1e-5f));
        }
    }
}
