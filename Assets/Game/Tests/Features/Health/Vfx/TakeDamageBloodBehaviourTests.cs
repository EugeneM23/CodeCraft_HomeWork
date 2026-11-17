using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class TakeDamageBloodBehaviourTests
    {
        private static readonly Vector3 Origin = new(4000f, 0f, 4000f);

        private readonly List<GameObject> _objects = new();

        private ParticleSystem _bulletBlood;
        private IEntity _character;
        private TakeDamageBloodBehaviour _behaviour;

        [SetUp]
        public void SetUp()
        {
            _bulletBlood = NewObject("BulletBlood", Origin).AddComponent<ParticleSystem>();
            Transform origin = NewObject("Origin", Origin).transform;

            _behaviour = new TakeDamageBloodBehaviour(_bulletBlood, null, origin);
            _character = new Entity();
            _character.AddDamageTakenEvent(new Event<TakeDamageArgs>());
            _character.AddDeathTakenEvent(new Event<TakeDamageArgs>());
            _character.AddBehaviour(_behaviour);
            _character.Init();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _objects)
                Object.DestroyImmediate(gameObject);

            _objects.Clear();
        }

        private GameObject NewObject(string name, Vector3 position)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.position = position;
            _objects.Add(gameObject);
            return gameObject;
        }

        private IEntity Shooter(Vector3 position, bool ranged)
        {
            IEntity source = new Entity();
            source.AddTransform(NewObject("Shooter", position).transform);
            if (ranged)
                source.AddRangeWeaponTag();
            else
                source.AddMeleeWeaponTag();
            return source;
        }

        [Test]
        public void BulletHit_TurnsTheSprayLevelTowardsTheShooter()
        {
            // The shooter stands above and to the east: the spray points east, not up into the air.
            IEntity shooter = Shooter(Origin + new Vector3(10f, 5f, 0f), ranged: true);

            _character.GetDamageTakenEvent().Invoke(new TakeDamageArgs(shooter));

            Vector3 forward = _bulletBlood.transform.forward;
            Assert.That(forward.x, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(forward.y, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void KillingBlow_AlsoSprays()
        {
            IEntity shooter = Shooter(Origin + new Vector3(0f, 0f, -10f), ranged: true);

            _character.GetDeathTakenEvent().Invoke(new TakeDamageArgs(shooter));

            Assert.That(_bulletBlood.transform.forward.z, Is.EqualTo(-1f).Within(1e-4f));
        }

        [Test]
        public void MeleeHit_LeavesTheBulletSprayAlone()
        {
            IEntity attacker = Shooter(Origin + new Vector3(-10f, 0f, 0f), ranged: false);

            _character.GetDamageTakenEvent().Invoke(new TakeDamageArgs(attacker));

            Assert.That(_bulletBlood.transform.forward, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void HitWithoutASource_IsIgnored()
        {
            Assert.DoesNotThrow(() => _character.GetDamageTakenEvent().Invoke(new TakeDamageArgs(null)));
            Assert.That(_bulletBlood.transform.forward, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void AfterDispose_HitsNoLongerSpray()
        {
            IEntity shooter = Shooter(Origin + new Vector3(10f, 0f, 0f), ranged: true);
            IEvent<TakeDamageArgs> damageEvent = _character.GetDamageTakenEvent();

            _behaviour.Dispose(_character);
            damageEvent.Invoke(new TakeDamageArgs(shooter));

            Assert.That(_bulletBlood.transform.forward, Is.EqualTo(Vector3.forward));
        }
    }
}
