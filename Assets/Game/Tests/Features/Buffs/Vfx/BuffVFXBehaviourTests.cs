using System.Collections.Generic;
using System.Reflection;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class BuffVFXBehaviourTests
    {
        private MoveSpeedBuffConfig _haste;
        private MoveSpeedBuffConfig _slow;
        private GameObject _hasteVfx;

        [SetUp]
        public void SetUp()
        {
            _haste = CreateConfig("Haste");
            _slow = CreateConfig("Slow");
            _hasteVfx = new GameObject("HasteVfx");
            _hasteVfx.AddComponent<ParticleSystem>();
            _hasteVfx.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_haste);
            Object.DestroyImmediate(_slow);
            Object.DestroyImmediate(_hasteVfx);
        }

        private static MoveSpeedBuffConfig CreateConfig(string name)
        {
            var config = ScriptableObject.CreateInstance<MoveSpeedBuffConfig>();
            typeof(BuffConfig).GetField("<Name>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(config, name);
            return config;
        }

        private IEntity Character(out BuffVFXBehaviour vfx)
        {
            IEntity character = new Entity();
            character.AddBuffsEffects(new ReactiveList<BuffBase>());
            // Discarding a move-speed buff restores the speed, so the character needs one.
            character.AddMoveSpeed(new ReactiveFloat(4f));

            vfx = new BuffVFXBehaviour(new Dictionary<string, ParticleSystem>
            {
                ["Haste"] = _hasteVfx.GetComponent<ParticleSystem>()
            });
            vfx.Init(character);
            return character;
        }

        [Test]
        public void BuffAdded_ShowsItsEffect()
        {
            IEntity character = Character(out _);

            character.GetBuffsEffects().Add(_haste.CreateBuff());

            Assert.That(_hasteVfx.activeSelf, Is.True);
        }

        [Test]
        public void BuffRemoved_HidesItsEffect()
        {
            IEntity character = Character(out _);
            BuffBase buff = _haste.CreateBuff();
            character.GetBuffsEffects().Add(buff);

            character.GetBuffsEffects().Remove(buff);

            Assert.That(_hasteVfx.activeSelf, Is.False);
        }

        [Test]
        public void AllBuffsDiscarded_HidesItsEffect()
        {
            IEntity character = Character(out _);
            character.GetBuffsEffects().Add(_haste.CreateBuff());

            BuffUseCase.DiscardAll(character);

            Assert.That(_hasteVfx.activeSelf, Is.False);
        }

        [Test]
        public void BuffWithoutEffect_LeavesOtherEffectsAlone()
        {
            IEntity character = Character(out _);

            Assert.DoesNotThrow(() => character.GetBuffsEffects().Add(_slow.CreateBuff()));
            Assert.That(_hasteVfx.activeSelf, Is.False);
        }

        [Test]
        public void AfterDispose_NoLongerReacts()
        {
            IEntity character = Character(out BuffVFXBehaviour vfx);

            vfx.Dispose(character);
            character.GetBuffsEffects().Add(_haste.CreateBuff());

            Assert.That(_hasteVfx.activeSelf, Is.False);
        }
    }
}
