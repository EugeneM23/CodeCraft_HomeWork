using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class EnemyCharacterAttackBehaviourTests
    {
        private readonly List<GameObject> _created = new();
        private IEntity _enemy;
        private EnemyCharacterAttackBehaviour _behaviour;
        private bool _weaponReady;
        private int _attacks;

        [SetUp]
        public void SetUp()
        {
            _weaponReady = true;
            _attacks = 0;

            IEntity claws = new Entity();
            claws.AddFireCondition(new AndExpression(new ExpressionMember<bool>(() => _weaponReady)));

            _enemy = new Entity();
            _enemy.AddTransform(Track(new GameObject("Enemy")).transform);
            _enemy.AddTarget(new ReactiveVariable<IEntity>());
            _enemy.AddWeapon(new ReactiveVariable<IEntity>(claws));
            _enemy.AddFireAction(new InlineAction(() => _attacks++));
            _behaviour = new EnemyCharacterAttackBehaviour();
            _behaviour.Init(_enemy);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _created)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);
            _created.Clear();
        }

        private GameObject Track(GameObject gameObject)
        {
            _created.Add(gameObject);
            return gameObject;
        }

        private IEntity Target(Vector3 position)
        {
            IEntity target = new Entity();
            target.AddTransform(Track(new GameObject("Target")).transform);
            target.GetTransform().position = position;
            _enemy.GetTarget().Value = target;
            return target;
        }

        [Test]
        public void TargetWithinReach_IsAttacked()
        {
            Target(new Vector3(0.8f, 0f, 0f));

            _behaviour.Tick(_enemy, 0.02f);

            Assert.That(_attacks, Is.EqualTo(1));
        }

        [Test]
        public void TargetOutOfReach_IsNotAttacked()
        {
            Target(new Vector3(1.5f, 0f, 0f));

            _behaviour.Tick(_enemy, 0.02f);

            Assert.That(_attacks, Is.Zero);
        }

        [Test]
        public void WeaponNotReady_DoesNotAttack()
        {
            Target(new Vector3(0.5f, 0f, 0f));
            _weaponReady = false;

            _behaviour.Tick(_enemy, 0.02f);

            Assert.That(_attacks, Is.Zero);
        }

        [Test]
        public void NoTarget_DoesNotAttack()
        {
            _behaviour.Tick(_enemy, 0.02f);

            Assert.That(_attacks, Is.Zero);
        }

        [Test]
        public void TargetDied_IsDropped_WithoutThrowing()
        {
            IEntity target = Target(new Vector3(0.5f, 0f, 0f));
            target.Dispose();

            Assert.DoesNotThrow(() => _behaviour.Tick(_enemy, 0.02f));
            Assert.That(_attacks, Is.Zero);
            Assert.That(_enemy.GetTarget().Value, Is.Null);
        }

        [Test]
        public void VehicleAlongside_IsAttacked_ThoughItsCentreIsFarAway()
        {
            // A 4 m wide hull centred 2.5 m away: its side is half a metre from the enemy.
            GameObject hull = Track(new GameObject("Tank"));
            hull.transform.position = new Vector3(2.5f, 0f, 0f);
            hull.AddComponent<BoxCollider>().size = new Vector3(4f, 2f, 6f);
            var body = hull.AddComponent<Rigidbody>();
            body.isKinematic = true;
            Physics.SyncTransforms();

            IEntity tank = new Entity();
            tank.AddVehicleTag();
            tank.AddTransform(hull.transform);
            tank.AddRiggedBody(body);
            _enemy.GetTarget().Value = tank;

            _behaviour.Tick(_enemy, 0.02f);

            Assert.That(_attacks, Is.EqualTo(1));
        }

        [Test]
        public void PersonCentredAsFarAsThatHull_IsNotAttacked()
        {
            Target(new Vector3(2.5f, 0f, 0f));

            _behaviour.Tick(_enemy, 0.02f);

            Assert.That(_attacks, Is.Zero);
        }
    }
}
