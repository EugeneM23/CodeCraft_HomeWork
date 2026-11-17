using System.Reflection;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class BuffUseCaseTests
    {
        private sealed class SpyBuff : BuffBase
        {
            private readonly bool _accepts;

            public int Applied { get; private set; }
            public int Discarded { get; private set; }

            public SpyBuff(BuffConfig config, bool accepts = true) : base(config) => _accepts = accepts;

            public override bool Apply(IEntity entity)
            {
                if (!_accepts)
                    return false;

                this.Applied++;
                return true;
            }

            public override void Discard(IEntity entity) => this.Discarded++;
        }

        private MoveSpeedBuffConfig _haste;
        private MoveSpeedBuffConfig _slow;

        [SetUp]
        public void SetUp()
        {
            _haste = CreateConfig("Haste");
            _slow = CreateConfig("Slow");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_haste);
            Object.DestroyImmediate(_slow);
        }

        private static MoveSpeedBuffConfig CreateConfig(string name)
        {
            var config = ScriptableObject.CreateInstance<MoveSpeedBuffConfig>();
            typeof(BuffConfig).GetField("<Name>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(config, name);
            return config;
        }

        private static IEntity Character()
        {
            IEntity character = new Entity();
            character.AddBuffsEffects(new ReactiveList<BuffBase>());
            return character;
        }

        [Test]
        public void Apply_AcceptedBuff_IsAppliedAndRecorded()
        {
            IEntity character = Character();
            var buff = new SpyBuff(_haste);

            bool applied = BuffUseCase.Apply(character, buff);

            Assert.That(applied, Is.True);
            Assert.That(buff.Applied, Is.EqualTo(1));
            Assert.That(character.GetBuffsEffects(), Does.Contain(buff));
        }

        [Test]
        public void Apply_BuffRefuses_IsNotRecorded()
        {
            IEntity character = Character();
            var buff = new SpyBuff(_haste, accepts: false);

            bool applied = BuffUseCase.Apply(character, buff);

            Assert.That(applied, Is.False);
            Assert.That(character.GetBuffsEffects(), Is.Empty);
        }

        [Test]
        public void Apply_RefusedBuff_DoesNotBlockALaterOneOfTheSameName()
        {
            IEntity character = Character();
            BuffUseCase.Apply(character, new SpyBuff(_haste, accepts: false));
            var second = new SpyBuff(_haste);

            bool applied = BuffUseCase.Apply(character, second);

            Assert.That(applied, Is.True);
            Assert.That(character.GetBuffsEffects(), Does.Contain(second));
        }

        [Test]
        public void Apply_SameNameAlreadyActive_IsRefusedWithoutTouchingTheCharacter()
        {
            IEntity character = Character();
            BuffUseCase.Apply(character, new SpyBuff(_haste));
            var duplicate = new SpyBuff(_haste);

            bool applied = BuffUseCase.Apply(character, duplicate);

            Assert.That(applied, Is.False);
            Assert.That(duplicate.Applied, Is.Zero);
            Assert.That(character.GetBuffsEffects(), Has.Count.EqualTo(1));
        }

        [Test]
        public void Apply_DifferentNames_BothRecorded()
        {
            IEntity character = Character();

            BuffUseCase.Apply(character, new SpyBuff(_haste));
            BuffUseCase.Apply(character, new SpyBuff(_slow));

            Assert.That(character.GetBuffsEffects(), Has.Count.EqualTo(2));
        }

        [Test]
        public void CanApply_FalseOnlyWhileABuffOfThatNameIsActive()
        {
            IEntity character = Character();
            BuffUseCase.Apply(character, new SpyBuff(_haste));

            Assert.That(BuffUseCase.CanApply(character, _haste), Is.False);
            Assert.That(BuffUseCase.CanApply(character, _slow), Is.True);
        }

        [Test]
        public void DiscardByName_RemovesAndDiscardsTheBuff()
        {
            IEntity character = Character();
            var buff = new SpyBuff(_haste);
            BuffUseCase.Apply(character, buff);

            bool discarded = BuffUseCase.Discard(character, "Haste");

            Assert.That(discarded, Is.True);
            Assert.That(buff.Discarded, Is.EqualTo(1));
            Assert.That(character.GetBuffsEffects(), Is.Empty);
        }

        [Test]
        public void DiscardByName_UnknownName_ReturnsFalse()
        {
            IEntity character = Character();
            var buff = new SpyBuff(_haste);
            BuffUseCase.Apply(character, buff);

            bool discarded = BuffUseCase.Discard(character, "Slow");

            Assert.That(discarded, Is.False);
            Assert.That(buff.Discarded, Is.Zero);
        }

        [Test]
        public void DiscardInstance_CharacterWithoutBuffList_ReturnsFalse()
        {
            Assert.That(BuffUseCase.Discard(new Entity(), new SpyBuff(_haste)), Is.False);
        }

        [Test]
        public void DiscardInstance_SecondTime_ReturnsFalseAndDiscardsOnce()
        {
            IEntity character = Character();
            var buff = new SpyBuff(_haste);
            BuffUseCase.Apply(character, buff);

            bool first = BuffUseCase.Discard(character, buff);
            bool second = BuffUseCase.Discard(character, buff);

            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
            Assert.That(buff.Discarded, Is.EqualTo(1));
        }

        [Test]
        public void DiscardAll_DiscardsEveryBuffAndEmptiesTheList()
        {
            IEntity character = Character();
            var haste = new SpyBuff(_haste);
            var slow = new SpyBuff(_slow);
            BuffUseCase.Apply(character, haste);
            BuffUseCase.Apply(character, slow);

            BuffUseCase.DiscardAll(character);

            Assert.That(haste.Discarded, Is.EqualTo(1));
            Assert.That(slow.Discarded, Is.EqualTo(1));
            Assert.That(character.GetBuffsEffects(), Is.Empty);
        }

        [Test]
        public void DiscardAll_EntityWithoutBuffList_DoesNothing()
        {
            Assert.DoesNotThrow(() => BuffUseCase.DiscardAll(new Entity()));
        }
    }
}
