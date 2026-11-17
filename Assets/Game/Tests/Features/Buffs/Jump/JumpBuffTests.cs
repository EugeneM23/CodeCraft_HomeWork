using System.Reflection;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class JumpBuffTests
    {
        private JumpBuffConfig _zoneJump;
        private JumpBuffConfig _pickupJump;

        [SetUp]
        public void SetUp()
        {
            _zoneJump = CreateConfig("ZoneJump", 7f);
            _pickupJump = CreateConfig("Jump", 9f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_zoneJump);
            Object.DestroyImmediate(_pickupJump);
        }

        private static JumpBuffConfig CreateConfig(string name, float force)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var config = ScriptableObject.CreateInstance<JumpBuffConfig>();
            typeof(BuffConfig).GetField("<Name>k__BackingField", flags).SetValue(config, name);
            typeof(JumpBuffConfig).GetField("<JumpForce>k__BackingField", flags).SetValue(config, force);
            return config;
        }

        private static IEntity Character()
        {
            IEntity character = new Entity();
            character.AddHealth(new Health(10, 10));
            character.AddBuffsEffects(new ReactiveList<BuffBase>());
            return character;
        }

        [Test]
        public void Apply_GrantsJumpingWithTheConfiguredForce()
        {
            IEntity character = Character();

            bool applied = BuffUseCase.Apply(character, _zoneJump.CreateBuff());

            Assert.That(applied, Is.True);
            Assert.That(character.HasJumpableTag(), Is.True);
            Assert.That(character.GetJumpForce(), Is.EqualTo(7f));
            Assert.That(character.GetJumpCondition().Invoke(), Is.True);
        }

        [Test]
        public void Discard_TakesJumpingAway()
        {
            IEntity character = Character();
            BuffBase buff = _zoneJump.CreateBuff();
            BuffUseCase.Apply(character, buff);

            BuffUseCase.Discard(character, buff);

            Assert.That(character.HasJumpableTag(), Is.False);
            Assert.That(character.HasJumpForce(), Is.False);
        }

        [Test]
        public void TwoOverlappingGrants_EndingOne_KeepsJumping()
        {
            IEntity character = Character();
            BuffBase zone = _zoneJump.CreateBuff();
            BuffBase pickup = _pickupJump.CreateBuff();
            BuffUseCase.Apply(character, zone);
            BuffUseCase.Apply(character, pickup);

            BuffUseCase.Discard(character, zone);

            Assert.That(character.HasJumpableTag(), Is.True);
        }

        [Test]
        public void TwoOverlappingGrants_EndingBoth_TakesJumpingAway()
        {
            IEntity character = Character();
            BuffBase zone = _zoneJump.CreateBuff();
            BuffBase pickup = _pickupJump.CreateBuff();
            BuffUseCase.Apply(character, zone);
            BuffUseCase.Apply(character, pickup);

            BuffUseCase.Discard(character, pickup);
            BuffUseCase.Discard(character, zone);

            Assert.That(character.HasJumpableTag(), Is.False);
        }
    }
}
