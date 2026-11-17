using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class GunRecoilBehaviourTests
    {
        private const float DISTANCE = 0.6f;
        private const float KICK = 0.05f;
        private const float HOLD = 0.12f;
        private const float RETURN = 0.8f;

        private static readonly Vector3 Battery = new(0f, 1f, 2f);

        private GameObject _gameObject;
        private Transform _gun;
        private IEntity _tank;
        private IEntity _cannon;
        private GunRecoilBehaviour _recoil;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("Turret");
            _gun = new GameObject("Barrel").transform;
            _gun.SetParent(_gameObject.transform, false);
            _gun.localPosition = Battery;

            _cannon = new Entity();
            _cannon.AddFireEvent(new Atomic.Elements.Event());

            _tank = new Entity();
            _tank.AddWeapon(new ReactiveVariable<IEntity>(_cannon));

            _recoil = new GunRecoilBehaviour(_gun, DISTANCE, KICK, HOLD, RETURN);
            _recoil.Init(_tank);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_gameObject);

        private void Fire() => _cannon.GetFireEvent().Invoke();

        private void Tick(float deltaTime, int times = 1)
        {
            for (int i = 0; i < times; i++)
                _recoil.Tick(_tank, deltaTime);
        }

        [Test]
        public void NoShot_BarrelStaysInBattery()
        {
            Tick(0.1f, 5);

            Assert.That(_gun.localPosition, Is.EqualTo(Battery));
        }

        [Test]
        public void Shot_BarrelRunsBackAlongItsOwnAxis()
        {
            Fire();

            Tick(KICK);

            Assert.That(Vector3.Distance(_gun.localPosition, Battery + Vector3.back * DISTANCE), Is.LessThan(1e-5f));
        }

        [Test]
        public void AfterTheWholeCycle_BarrelIsBackInBattery()
        {
            Fire();

            Tick(0.1f, 12);

            Assert.That(Vector3.Distance(_gun.localPosition, Battery), Is.LessThan(1e-5f));
        }

        [Test]
        public void ShotWhileRunningOut_KicksBackAgainFromTheStart()
        {
            Fire();
            Tick(0.5f);

            Fire();
            Tick(KICK);

            Assert.That(Vector3.Distance(_gun.localPosition, Battery + Vector3.back * DISTANCE), Is.LessThan(1e-5f));
        }

        [Test]
        public void DisposedMidRecoil_PutsTheBarrelBack()
        {
            Fire();
            Tick(KICK);

            _recoil.Dispose(_tank);

            Assert.That(_gun.localPosition, Is.EqualTo(Battery));
        }

        [Test]
        public void AfterDispose_ShotsNoLongerMoveTheBarrel()
        {
            _recoil.Dispose(_tank);

            Fire();
            Tick(KICK);

            Assert.That(_gun.localPosition, Is.EqualTo(Battery));
        }
    }
}
