using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class PassThroughShooterTests
    {
        private GameObject _bulletObject;
        private GameObject _hullObject;
        private SphereCollider _bulletCollider;
        private BoxCollider _hull;
        private IEntity _bullet;

        [SetUp]
        public void SetUp()
        {
            _bulletObject = new GameObject("Bullet");
            _bulletCollider = _bulletObject.AddComponent<SphereCollider>();
            _bullet = new Entity();
            _bullet.AddGameObject(_bulletObject);

            _hullObject = new GameObject("Hull");
            _hull = _hullObject.AddComponent<BoxCollider>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_bulletObject);
            Object.DestroyImmediate(_hullObject);
        }

        private IEntity Gun(params Collider[] shooter)
        {
            IEntity gun = new Entity();
            if (shooter.Length > 0)
                gun.AddShooterColliders(shooter);
            return gun;
        }

        [Test]
        public void GunWithShooterColliders_BulletPassesThroughThem()
        {
            SpawnUseCase.PassThroughShooter(_bullet, Gun(_hull));

            Assert.That(Physics.GetIgnoreCollision(_bulletCollider, _hull), Is.True);
        }

        [Test]
        public void PlainGun_BulletHitsEverything()
        {
            SpawnUseCase.PassThroughShooter(_bullet, Gun());

            Assert.That(Physics.GetIgnoreCollision(_bulletCollider, _hull), Is.False);
        }

        [Test]
        public void PooledBullet_FiredByAnotherGun_HitsTheFormerShooterAgain()
        {
            SpawnUseCase.PassThroughShooter(_bullet, Gun(_hull));
            SpawnUseCase.PassThroughShooter(_bullet, Gun());

            Assert.That(Physics.GetIgnoreCollision(_bulletCollider, _hull), Is.False);
        }

        [Test]
        public void PooledBullet_DeactivatedBetweenFlights_IsPassedThroughAgainBySameGun()
        {
            IEntity gun = Gun(_hull);
            SpawnUseCase.PassThroughShooter(_bullet, gun);

            _bulletObject.SetActive(false);
            _bulletObject.SetActive(true);
            SpawnUseCase.PassThroughShooter(_bullet, gun);

            Assert.That(Physics.GetIgnoreCollision(_bulletCollider, _hull), Is.True);
        }
    }
}
