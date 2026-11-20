using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class SpawnUseCaseTests
    {
        private const string BulletId = "Bullet_01";

        private MonoEntity _prefab;
        private GameObject _poolsParent;
        private GameObject _firePointObject;
        private GameFactory _factory;
        private IEntity _pooledBullet;

        /// <summary>
        /// A factory whose bullet pool holds one bullet that has already flown: it carries the
        /// values the bullet installer gives it, and two bounces from its last flight.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _prefab = MonoEntity.Create<MonoEntity>(name: "Bullet");
            _poolsParent = new GameObject("Pools");
            _factory = new GameFactory(_poolsParent.transform);
            _factory.RegisterNewPrefab(_prefab, BulletId);

            _pooledBullet = _factory.Create(BulletId);
            _pooledBullet.AddEntityID(BulletId);
            _pooledBullet.AddTransform(MonoEntity.Cast(_pooledBullet).transform);
            _pooledBullet.AddMoveDirection(new ReactiveVariable<Vector3>());
            _pooledBullet.AddRicochetCount(2);
            _pooledBullet.AddDamage(new ReactiveInt(1));
            _factory.Destroy(_pooledBullet);

            _firePointObject = new GameObject("FirePoint");
            _firePointObject.transform.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_poolsParent);
            Object.DestroyImmediate(_prefab.gameObject);
            Object.DestroyImmediate(_firePointObject);
        }

        private IEntity Weapon(int damage, float spread = 0f)
        {
            IEntity weapon = new Entity();
            weapon.AddBulletPrefab(_prefab);
            weapon.AddDamage(new ReactiveInt(damage));
            if (spread > 0f)
                weapon.AddSpread(new Const<float>(spread));
            return weapon;
        }

        [Test]
        public void SpawnBullet_ReusesThePooledBullet_AtTheFirePoint_FlyingItsWay()
        {
            Transform firePoint = _firePointObject.transform;

            IEntity bullet = SpawnUseCase.SpawnBullet(Weapon(5), _factory, firePoint);

            Assert.That(bullet, Is.SameAs(_pooledBullet));
            Assert.That(Vector3.Distance(bullet.GetTransform().position, firePoint.position), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(bullet.GetMoveDirection().Value, firePoint.forward), Is.LessThan(1e-4f));
        }

        [Test]
        public void SpawnBullet_CarriesTheWeaponsDamage_AndForgetsItsOldBounces()
        {
            IEntity weapon = Weapon(5);

            IEntity bullet = SpawnUseCase.SpawnBullet(weapon, _factory, _firePointObject.transform);

            Assert.That(bullet.GetDamage().Value, Is.EqualTo(5));
            Assert.That(bullet.GetRicochetCount(), Is.Zero);
        }

        [Test]
        public void SpawnBullet_Spread_TurnsTheShotWithinTheCone_AndLessUpAndDown()
        {
            Transform firePoint = _firePointObject.transform;

            IEntity bullet = SpawnUseCase.SpawnBullet(Weapon(5, spread: 10f), _factory, firePoint);

            Vector3 direction = bullet.GetMoveDirection().Value;
            // Sideways up to the spread, up and down up to 0.6 of it (SpawnUseCase.VERTICAL_SPREAD).
            Assert.That(Vector3.Angle(direction, firePoint.forward), Is.LessThanOrEqualTo(11.7f));
            float pitch = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
            Assert.That(Mathf.Abs(pitch), Is.LessThanOrEqualTo(6.01f), "up and down: at most 0.6 of the spread");
        }

        [Test]
        public void UnSpawnEntity_SendsItBackToItsPool()
        {
            IEntity bullet = SpawnUseCase.SpawnBullet(Weapon(5), _factory, _firePointObject.transform);

            SpawnUseCase.UnSpawnEntity(_factory, bullet);

            Assert.That(_factory.Pools[BulletId].Count, Is.EqualTo(1));
            Assert.That(MonoEntity.Cast(bullet).gameObject.activeSelf, Is.False);
        }
    }
}
