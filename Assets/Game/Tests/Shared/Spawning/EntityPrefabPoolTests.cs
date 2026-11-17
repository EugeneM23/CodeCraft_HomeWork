using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class EntityPrefabPoolTests
    {
        private MonoEntity _prefab;
        private Transform _container;
        private EntityPrefabPool _pool;

        [SetUp]
        public void SetUp()
        {
            _prefab = MonoEntity.Create<MonoEntity>(name: "PooledThing");
            _container = new GameObject("Container").transform;
            _pool = new EntityPrefabPool(_prefab, _container);
        }

        [TearDown]
        public void TearDown()
        {
            // Every instance the pool made lives under the container until a test moves it.
            Object.DestroyImmediate(_container.gameObject);
            Object.DestroyImmediate(_prefab.gameObject);
        }

        [Test]
        public void Init_ParksTheInstancesInactiveUnderTheContainer()
        {
            _pool.Init(3);

            Assert.That(_pool.Count, Is.EqualTo(3));
            Assert.That(_container.childCount, Is.EqualTo(3));
            foreach (Transform child in _container)
                Assert.That(child.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Rent_HandsOutAParkedInstance_Active()
        {
            _pool.Init(2);

            IEntity rented = _pool.Rent();

            Assert.That(_pool.Count, Is.EqualTo(1));
            Assert.That(MonoEntity.Cast(rented).gameObject.activeSelf, Is.True);
            Assert.That(_container.childCount, Is.EqualTo(2), "a prewarmed instance, not a new one");
        }

        [Test]
        public void Rent_FromAnEmptyPool_MakesANewInstance()
        {
            IEntity rented = _pool.Rent();

            Assert.That(rented, Is.Not.Null);
            Assert.That(MonoEntity.Cast(rented), Is.Not.SameAs(_prefab));
            Assert.That(MonoEntity.Cast(rented).gameObject.activeSelf, Is.True);
            Assert.That(_pool.Count, Is.Zero);
        }

        [Test]
        public void Return_ParksTheInstanceAgain_BackUnderTheContainer()
        {
            MonoEntity rented = MonoEntity.Cast(_pool.Rent());
            var elsewhere = new GameObject("Elsewhere");
            rented.transform.SetParent(elsewhere.transform);

            _pool.Return(rented);
            Object.DestroyImmediate(elsewhere);

            Assert.That(_pool.Count, Is.EqualTo(1));
            Assert.That(rented.transform.parent, Is.SameAs(_container));
            Assert.That(rented.gameObject.activeSelf, Is.False);
            Assert.That(_pool.Rent(), Is.SameAs(rented));
        }

        [Test]
        public void Return_Twice_ParksTheInstanceOnce()
        {
            IEntity rented = _pool.Rent();

            _pool.Return(rented);
            _pool.Return(rented);

            Assert.That(_pool.Count, Is.EqualTo(1));
        }

        [Test]
        public void Return_SomethingThatIsNotAGameObject_IsIgnored()
        {
            _pool.Return(new Entity());

            Assert.That(_pool.Count, Is.Zero);
        }
    }
}
