using System;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class GameFactoryTests
    {
        private MonoEntity _prefab;
        private GameObject _poolsParent;
        private GameFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _prefab = MonoEntity.Create<MonoEntity>(name: "Bullet");
            _poolsParent = new GameObject("Pools");
            _factory = new GameFactory(_poolsParent.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_poolsParent);
            Object.DestroyImmediate(_prefab.gameObject);
        }

        [Test]
        public void Create_UnknownPrefab_RegistersAPoolUnderItsName()
        {
            IEntity bullet = _factory.Create(_prefab);

            Assert.That(bullet, Is.Not.Null);
            Assert.That(MonoEntity.Cast(bullet), Is.Not.SameAs(_prefab));
            Assert.That(_factory.Pools.ContainsKey("Bullet"), Is.True);
            Assert.That(MonoEntity.Cast(bullet).transform.parent.name, Is.EqualTo("Pool_Bullet"));
        }

        [Test]
        public void Destroy_PooledEntity_IsRentedAgainByTheNextCreate()
        {
            IEntity bullet = _factory.Create(_prefab);
            // The bullet installer writes the pool id; the bare prefab here has no installer.
            bullet.AddEntityID("Bullet");

            _factory.Destroy(bullet);
            IEntity next = _factory.Create(_prefab);

            Assert.That(next, Is.SameAs(bullet));
        }

        [Test]
        public void CreateById_ComesFromThePoolRegisteredUnderThatId()
        {
            _factory.RegisterNewPrefab(_prefab, "Bullet_01");

            IEntity byId = _factory.Create("Bullet_01");
            byId.AddEntityID("Bullet_01");
            _factory.Destroy(byId);

            Assert.That(_factory.Create(_prefab), Is.SameAs(byId), "one pool, reachable by prefab and by id");
        }

        [Test]
        public void CreateById_Unregistered_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _factory.Create("Nothing"));
        }

        [Test]
        public void RegisterNewPrefab_Prewarm_ParksThatManyInstances()
        {
            EntityPrefabPool pool = _factory.RegisterNewPrefab(_prefab, "Bullet_01", prewarm: 4);

            Assert.That(pool.Count, Is.EqualTo(4));
            Assert.That(_factory.Pools["Bullet_01"], Is.SameAs(pool));
        }
    }
}
