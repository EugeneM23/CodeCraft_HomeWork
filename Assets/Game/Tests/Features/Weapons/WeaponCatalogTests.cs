using System.Collections.Generic;
using System.Reflection;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class WeaponCatalogTests
    {
        private static readonly Vector3 CatalogPosition = new(12f, 0f, -3f);

        private readonly List<GameObject> _objects = new();
        private WeaponCatalog _catalog;
        private MonoEntity _rifle;
        private MonoEntity _rifleOnTheFloor;

        [SetUp]
        public void SetUp()
        {
            _rifle = Track(MonoEntity.Create<MonoEntity>(name: "AR_01"));
            _rifleOnTheFloor = Track(MonoEntity.Create<MonoEntity>(name: "AR_01_PickUp"));

            var catalogObject = new GameObject("WeaponCatalog");
            _objects.Add(catalogObject);
            catalogObject.transform.position = CatalogPosition;
            _catalog = catalogObject.AddComponent<WeaponCatalog>();

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WeaponCatalog).GetField("_weapons", flags)
                .SetValue(_catalog, new Dictionary<WeaponID, MonoEntity> { [WeaponID.AR_01] = _rifle });
            typeof(WeaponCatalog).GetField("_pickUpWeapons", flags)
                .SetValue(_catalog, new Dictionary<WeaponID, MonoEntity> { [WeaponID.AR_01] = _rifleOnTheFloor });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _objects)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);

            _objects.Clear();
        }

        private MonoEntity Track(MonoEntity entity)
        {
            _objects.Add(entity.gameObject);
            return entity;
        }

        [Test]
        public void GetWeapon_MakesAFreshCopyOfThatWeapon_AtTheCatalog()
        {
            MonoEntity first = Track(_catalog.GetWeapon(WeaponID.AR_01));
            MonoEntity second = Track(_catalog.GetWeapon(WeaponID.AR_01));

            Assert.That(first, Is.Not.SameAs(_rifle));
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(first.name, Is.EqualTo("AR_01"));
            Assert.That(Vector3.Distance(first.transform.position, CatalogPosition), Is.LessThan(1e-4f));
        }

        [Test]
        public void GetPickUpWeapon_CopiesTheFloorVersion_NotTheHeldOne()
        {
            MonoEntity pickUp = Track(_catalog.GetPickUpWeapon(WeaponID.AR_01));

            Assert.That(pickUp.name, Is.EqualTo("AR_01_PickUp"));
        }

        [Test]
        public void GetWeapon_NotInTheCatalog_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => _catalog.GetWeapon(WeaponID.RPG_01));
        }
    }
}
