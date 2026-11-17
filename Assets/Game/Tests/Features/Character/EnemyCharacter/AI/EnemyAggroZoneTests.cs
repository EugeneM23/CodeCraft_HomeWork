using System.Collections.Generic;
using System.Reflection;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// The trigger callbacks are private and need a physics step to fire, so they are invoked
    /// directly with the collider that would have entered.
    /// </summary>
    public sealed class EnemyAggroZoneTests
    {
        private readonly List<GameObject> _created = new();
        private EnemyAggroZone _zone;

        [SetUp]
        public void SetUp()
        {
            GameObject zone = Track(new GameObject("AggroZone"));
            zone.AddComponent<BoxCollider>().isTrigger = true;
            _zone = zone.AddComponent<EnemyAggroZone>();
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

        private MonoEntity NewEntity(string name)
        {
            GameObject gameObject = Track(new GameObject(name));
            gameObject.AddComponent<SphereCollider>();
            var entity = gameObject.AddComponent<MonoEntity>();
            ((ISerializationCallbackReceiver)entity).OnAfterDeserialize();
            return entity;
        }

        private MonoEntity Enemy()
        {
            MonoEntity enemy = NewEntity("Enemy");
            enemy.AddTarget(new ReactiveVariable<IEntity>());
            return enemy;
        }

        private MonoEntity Player()
        {
            MonoEntity player = NewEntity("Player");
            player.AddPlayerTag();
            return player;
        }

        private void Guard(params MonoEntity[] enemies) =>
            typeof(EnemyAggroZone).GetField("_enemies", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(_zone, enemies);

        private void Trigger(string callback, Component other) =>
            typeof(EnemyAggroZone).GetMethod(callback, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_zone, new object[] { other.GetComponent<Collider>() });

        [Test]
        public void PlayerEnters_EveryGuardTargetsThePlayer()
        {
            MonoEntity first = Enemy();
            MonoEntity second = Enemy();
            MonoEntity player = Player();
            Guard(first, second);

            Trigger("OnTriggerEnter", player);

            Assert.That(first.GetTarget().Value, Is.SameAs(player));
            Assert.That(second.GetTarget().Value, Is.SameAs(player));
        }

        [Test]
        public void SomethingElseEnters_GuardsTakeNoNotice()
        {
            MonoEntity enemy = Enemy();
            MonoEntity crate = NewEntity("Crate");
            Guard(enemy);

            Trigger("OnTriggerEnter", crate);

            Assert.That(enemy.GetTarget().Value, Is.Null);
        }

        [Test]
        public void PlayerLeaves_GuardsForgetThePlayer()
        {
            MonoEntity enemy = Enemy();
            MonoEntity player = Player();
            Guard(enemy);
            Trigger("OnTriggerEnter", player);

            Trigger("OnTriggerExit", player);

            Assert.That(enemy.GetTarget().Value, Is.Null);
        }

        [Test]
        public void DeadOrMissingGuardInTheList_DoesNotStopTheOnesAfterIt()
        {
            MonoEntity dead = Enemy();
            dead.DelTarget(); // DeathBehaviour's dispose strips the values.
            MonoEntity alive = Enemy();
            MonoEntity player = Player();
            Guard(dead, null, alive);

            Assert.DoesNotThrow(() => Trigger("OnTriggerEnter", player));
            Assert.That(alive.GetTarget().Value, Is.SameAs(player));
        }
    }
}
