using System.Reflection;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// TemporaryBuff through its simplest concrete buff, MoveSpeedBuff.
    /// </summary>
    public sealed class TemporaryBuffTests
    {
        private MoveSpeedBuffConfig _config;

        [SetUp]
        public void SetUp() => _config = ScriptableObject.CreateInstance<MoveSpeedBuffConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        private void Configure(float duration, float speed)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(BuffConfig).GetField("<Name>k__BackingField", flags).SetValue(_config, "Haste");
            typeof(BuffConfig).GetField("<Duration>k__BackingField", flags).SetValue(_config, duration);
            typeof(MoveSpeedBuffConfig).GetField("<Speed>k__BackingField", flags).SetValue(_config, speed);
        }

        private static IEntity Character(float moveSpeed)
        {
            IEntity character = new Entity();
            character.AddMoveSpeed(new ReactiveFloat(moveSpeed));
            character.AddBuffsEffects(new ReactiveList<BuffBase>());
            return character;
        }

        [Test]
        public void CreateBuff_MakesAFreshMoveSpeedBuffEachTime()
        {
            Configure(1f, 2f);

            BuffBase first = _config.CreateBuff();
            BuffBase second = _config.CreateBuff();

            Assert.That(first, Is.InstanceOf<MoveSpeedBuff>());
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(first.Name, Is.EqualTo("Haste"));
        }

        [Test]
        public void Apply_AttachesItselfAsBehaviourAndAppliesTheEffect()
        {
            Configure(1f, 2f);
            IEntity character = Character(3f);
            var buff = (TemporaryBuff)_config.CreateBuff();

            bool applied = buff.Apply(character);

            Assert.That(applied, Is.True);
            Assert.That(character.HasBehaviour(buff), Is.True);
            Assert.That(character.GetMoveSpeed().Value, Is.EqualTo(6f).Within(1e-4f));
        }

        [Test]
        public void Apply_InvalidEntity_RefusesAndAttachesNothing()
        {
            Configure(1f, 2f);
            IEntity character = new Entity();
            character.AddBuffsEffects(new ReactiveList<BuffBase>());
            var buff = (TemporaryBuff)_config.CreateBuff();

            bool applied = BuffUseCase.Apply(character, buff);

            Assert.That(applied, Is.False);
            Assert.That(character.HasBehaviour(buff), Is.False);
            Assert.That(character.GetBuffsEffects(), Is.Empty);
        }

        [Test]
        public void Discard_DetachesBehaviourAndUndoesTheEffect()
        {
            Configure(1f, 2f);
            IEntity character = Character(3f);
            var buff = (TemporaryBuff)_config.CreateBuff();
            buff.Apply(character);

            buff.Discard(character);

            Assert.That(character.HasBehaviour(buff), Is.False);
            Assert.That(character.GetMoveSpeed().Value, Is.EqualTo(3f).Within(1e-4f));
        }

        [Test]
        public void FixedTick_BeforeDuration_KeepsTheBuff()
        {
            Configure(1f, 2f);
            IEntity character = Character(3f);
            var buff = (TemporaryBuff)_config.CreateBuff();
            BuffUseCase.Apply(character, buff);

            buff.FixedTick(character, 0.5f);

            Assert.That(character.GetBuffsEffects(), Does.Contain(buff));
            Assert.That(character.GetMoveSpeed().Value, Is.EqualTo(6f).Within(1e-4f));
        }

        [Test]
        public void FixedTick_DurationReached_ExpiresAndRestores()
        {
            Configure(1f, 2f);
            IEntity character = Character(3f);
            var buff = (TemporaryBuff)_config.CreateBuff();
            BuffUseCase.Apply(character, buff);

            buff.FixedTick(character, 0.5f);
            buff.FixedTick(character, 0.5f);

            Assert.That(character.GetBuffsEffects(), Is.Empty);
            Assert.That(character.HasBehaviour(buff), Is.False);
            Assert.That(character.GetMoveSpeed().Value, Is.EqualTo(3f).Within(1e-4f));
        }

        [Test]
        public void EnabledEntity_FixedTicksTheBuffToExpiry()
        {
            Configure(1f, 2f);
            IEntity character = Character(3f);
            character.Enable();
            BuffUseCase.Apply(character, _config.CreateBuff());

            character.FixedTick(0.6f);
            character.FixedTick(0.6f);

            Assert.That(character.GetBuffsEffects(), Is.Empty);
            Assert.That(character.GetMoveSpeed().Value, Is.EqualTo(3f).Within(1e-4f));
        }
    }
}
