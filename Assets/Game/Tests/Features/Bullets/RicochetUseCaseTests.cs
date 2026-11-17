using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class RicochetUseCaseTests
    {
        // The surface is a floor: its normal points up, towards a bullet coming down onto it.
        private static readonly Vector3 Normal = Vector3.up;

        private GameObject _gameObject;

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
                Object.DestroyImmediate(_gameObject);
        }

        // Flies along +X and down, at the given angle to the floor.
        private static Vector3 Incoming(float angleToSurface) =>
            Quaternion.AngleAxis(-angleToSurface, Vector3.forward) * Vector3.right;

        private IEntity CreateBullet(Vector3 direction, int maxCount = 2, float maxAngle = 30f, int count = 0)
        {
            _gameObject = new GameObject("Bullet");
            _gameObject.AddComponent<Rigidbody>().useGravity = false;

            IEntity bullet = new Entity();
            bullet.AddTransform(_gameObject.transform);
            bullet.AddMoveDirection(new ReactiveVector3(direction));
            bullet.AddRiggedBody(_gameObject.GetComponent<Rigidbody>());
            bullet.AddRicochetMaxCount(maxCount);
            bullet.AddRicochetMaxAngle(maxAngle);
            bullet.AddRicochetImpactFactor(0.3f);
            bullet.AddRicochetCount(count);
            return bullet;
        }

        [Test]
        public void GetSurfaceAngle_GrazingHit_ReturnsAngleToPlane()
        {
            Assert.That(RicochetUseCase.GetSurfaceAngle(Incoming(20f), Normal), Is.EqualTo(20f).Within(0.01f));
        }

        [Test]
        public void GetSurfaceAngle_HeadOnHit_Returns90()
        {
            Assert.That(RicochetUseCase.GetSurfaceAngle(Vector3.down, Normal), Is.EqualTo(90f).Within(0.01f));
        }

        [Test]
        public void IsMovingAway_TowardsSurface_False()
        {
            Assert.That(RicochetUseCase.IsMovingAway(Incoming(20f), Normal), Is.False);
        }

        [Test]
        public void IsMovingAway_AfterBounce_True()
        {
            Vector3 bounced = Vector3.Reflect(Incoming(20f), Normal);
            Assert.That(RicochetUseCase.IsMovingAway(bounced, Normal), Is.True);
        }

        [Test]
        public void IsEnabled_WithoutRicochetValues_False()
        {
            Assert.That(RicochetUseCase.IsEnabled(new Entity()), Is.False);
        }

        [Test]
        public void IsEnabled_ZeroMaxCount_False()
        {
            Assert.That(RicochetUseCase.IsEnabled(CreateBullet(Incoming(20f), maxCount: 0)), Is.False);
        }

        [Test]
        public void IsEnabled_PositiveMaxCount_True()
        {
            Assert.That(RicochetUseCase.IsEnabled(CreateBullet(Incoming(20f))), Is.True);
        }

        [Test]
        public void CanRicochet_GrazingHit_True()
        {
            Assert.That(RicochetUseCase.CanRicochet(CreateBullet(Incoming(20f)), Normal), Is.True);
        }

        [Test]
        public void CanRicochet_SteepHit_False()
        {
            Assert.That(RicochetUseCase.CanRicochet(CreateBullet(Incoming(45f)), Normal), Is.False);
        }

        [Test]
        public void CanRicochet_LimitReached_False()
        {
            Assert.That(RicochetUseCase.CanRicochet(CreateBullet(Incoming(20f), count: 2), Normal), Is.False);
        }

        [Test]
        public void CanRicochet_Disabled_False()
        {
            Assert.That(RicochetUseCase.CanRicochet(new Entity(), Normal), Is.False);
        }

        [Test]
        public void Ricochet_ReflectsDirectionAndTurnsBullet()
        {
            IEntity bullet = CreateBullet(Incoming(20f));
            Vector3 expected = Vector3.Reflect(Incoming(20f), Normal).normalized;

            RicochetUseCase.Ricochet(bullet, Normal);

            Assert.That(Vector3.Distance(bullet.GetMoveDirection().Value, expected), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(bullet.GetTransform().forward, expected), Is.LessThan(1e-4f));
        }

        [Test]
        public void Ricochet_CountsBounce()
        {
            IEntity bullet = CreateBullet(Incoming(20f), count: 1);

            RicochetUseCase.Ricochet(bullet, Normal);

            Assert.That(bullet.GetRicochetCount(), Is.EqualTo(2));
        }

        [Test]
        public void Ricochet_StopsRigidbody()
        {
            IEntity bullet = CreateBullet(Incoming(20f));
            Rigidbody body = bullet.GetRiggedBody();
            body.linearVelocity = new Vector3(3f, 1f, 0f);
            body.angularVelocity = new Vector3(0f, 2f, 0f);

            RicochetUseCase.Ricochet(bullet, Normal);

            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
        }
    }
}
