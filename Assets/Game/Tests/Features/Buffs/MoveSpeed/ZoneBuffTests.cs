using System.Reflection;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class ZoneBuffTests
    {
        private MoveSpeedBuffConfig _config;

        [SetUp]
        public void SetUp() => _config = ScriptableObject.CreateInstance<MoveSpeedBuffConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        private void Configure(string name, float duration, float speed)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(BuffConfig).GetField("<Name>k__BackingField", flags).SetValue(_config, name);
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
        public void ZeroDuration_NeverRunsOut()
        {
            Configure("Zone", 0f, 0.5f);
            IEntity character = Character(4f);
            var buff = (TemporaryBuff)_config.CreateBuff();
            BuffUseCase.Apply(character, buff);

            for (int i = 0; i < 1000; i++)
                buff.FixedTick(character, 0.1f);

            Assert.That(character.GetBuffsEffects(), Does.Contain(buff));
            Assert.That(character.GetMoveSpeed().Value, Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void FractionalSpeed_SlowsAndRestores()
        {
            Configure("Zone", 0f, 0.4f);
            IEntity character = Character(5f);
            BuffBase buff = _config.CreateBuff();

            BuffUseCase.Apply(character, buff);
            float slowed = character.GetMoveSpeed().Value;
            BuffUseCase.Discard(character, buff);

            Assert.That(slowed, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(character.GetMoveSpeed().Value, Is.EqualTo(5f).Within(1e-4f));
        }

        [Test]
        public void DiscardInstance_LeavesAnotherBuffOfTheSameNameAlone()
        {
            Configure("Zone", 0f, 0.5f);
            IEntity character = Character(4f);
            BuffBase given = _config.CreateBuff();
            BuffBase stranger = _config.CreateBuff();
            BuffUseCase.Apply(character, given);

            bool removedStranger = BuffUseCase.Discard(character, stranger);

            Assert.That(removedStranger, Is.False);
            Assert.That(character.GetBuffsEffects(), Does.Contain(given));
        }
    }
}
