using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class DeathBehaviourTests
    {
        private GameContext _context;
        private EntityWorld _world;
        private MonoEntity _character;
        private CapsuleCollider _capsule;
        private Health _health;

        /// <summary>
        /// A character the way the scene has one: a MonoEntity in the context's world, with a
        /// capsule on its root and health that raises the death event.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _world = new EntityWorld();
            _context = MonoEntity.Create<GameContext>(name: "GameContext");
            _context.AddEntityWorld(_world);

            _character = MonoEntity.Create<MonoEntity>(name: "Character");
            _capsule = _character.gameObject.AddComponent<CapsuleCollider>();
            _health = HealthUseCase.AddHealthWithEvents(_character, 10);
            _character.AddGameObject(_character.gameObject);
            _character.AddBehaviour(new DeathBehaviour(_context));
            _character.Init();
            _world.Add(_character);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_character.gameObject);
            Object.DestroyImmediate(_context.gameObject);
        }

        [Test]
        public void Killed_RaisesTheDeathEvent_WithTheCorpseAsSource()
        {
            IEntity source = null;
            int deaths = 0;
            _character.GetDeathTakenEvent().Subscribe(args =>
            {
                deaths++;
                source = args.source;
            });

            _health.Reduce(10);

            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(source, Is.SameAs(_character));
        }

        [Test]
        public void Killed_SwitchesOffTheCapsule_SoBulletsPassThroughTheCorpse()
        {
            _health.Reduce(10);

            Assert.That(_capsule.enabled, Is.False);
        }

        [Test]
        public void Killed_LeavesTheWorld_AndReleasesItsValues()
        {
            _health.Reduce(10);

            Assert.That(_world.Count, Is.Zero);
            Assert.That(_character.HasHealth(), Is.False);
        }

        [Test]
        public void Hurt_StaysWholeAndInTheWorld()
        {
            int deaths = 0;
            _character.GetDeathTakenEvent().Subscribe(_ => deaths++);

            _health.Reduce(9);

            Assert.That(deaths, Is.Zero);
            Assert.That(_capsule.enabled, Is.True);
            Assert.That(_world.Count, Is.EqualTo(1));
            Assert.That(_character.HasHealth(), Is.True);
        }

        [Test]
        public void HealthEmptiedAgainAfterDeath_DoesNotRunTheDeathAgain()
        {
            _health.Reduce(10);
            _capsule.enabled = true;

            // Health outlives the entity; whatever still holds it can refill and empty it.
            _health.SetCurrent(5);
            _health.SetCurrent(0);

            Assert.That(_capsule.enabled, Is.True, "the behaviour let go of the health on dispose");
        }
    }
}
