using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class DeathEffectBehaviourTests
    {
        private GameObject _effect;

        [SetUp]
        public void SetUp()
        {
            _effect = new GameObject("DeathEffect");
            _effect.SetActive(false);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_effect);

        private static IEntity Character(DeathEffectBehaviour behaviour)
        {
            IEntity character = new Entity();
            character.AddDeathTakenEvent(new Event<TakeDamageArgs>());
            character.AddBehaviour(behaviour);
            character.Init();
            return character;
        }

        [Test]
        public void Death_SwitchesTheEffectOn()
        {
            IEntity character = Character(new DeathEffectBehaviour(_effect));

            character.GetDeathTakenEvent().Invoke(new TakeDamageArgs(character));

            Assert.That(_effect.activeSelf, Is.True);
        }

        [Test]
        public void Death_TurnsTheEffectOnlyAboutTheVertical()
        {
            _effect.transform.rotation = Quaternion.Euler(30f, 0f, 20f);
            IEntity character = Character(new DeathEffectBehaviour(_effect));

            character.GetDeathTakenEvent().Invoke(new TakeDamageArgs(character));

            Assert.That(Vector3.Angle(_effect.transform.up, Vector3.up), Is.LessThan(0.01f));
        }

        [Test]
        public void NoEffectAssigned_DeathIsHarmless()
        {
            IEntity character = Character(new DeathEffectBehaviour(null));

            Assert.DoesNotThrow(() => character.GetDeathTakenEvent().Invoke(new TakeDamageArgs(character)));
        }

        [Test]
        public void AfterDispose_DeathNoLongerShowsTheEffect()
        {
            var behaviour = new DeathEffectBehaviour(_effect);
            IEvent<TakeDamageArgs> deathEvent = new Event<TakeDamageArgs>();
            IEntity character = new Entity();
            character.AddDeathTakenEvent(deathEvent);
            behaviour.Init(character);

            behaviour.Dispose(character);
            deathEvent.Invoke(new TakeDamageArgs(character));

            Assert.That(_effect.activeSelf, Is.False);
        }
    }
}
