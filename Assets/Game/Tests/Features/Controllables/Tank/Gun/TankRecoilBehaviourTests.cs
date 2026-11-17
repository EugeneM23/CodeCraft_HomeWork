using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class TankRecoilBehaviourTests
    {
        private static readonly Vector3 Origin = new(-4900f, 300f, 4900f);

        private GameObject _hull;
        private Rigidbody _body;
        private Transform _firePoint;
        private IEntity _tank;
        private IEntity _cannon;

        [SetUp]
        public void SetUp()
        {
            _hull = new GameObject("Tank");
            _hull.transform.position = Origin;
            _body = _hull.AddComponent<Rigidbody>();
            _body.useGravity = false;

            // The barrel points right, high above the hull.
            _firePoint = new GameObject("FirePoint").transform;
            _firePoint.SetParent(_hull.transform, false);
            _firePoint.localPosition = new Vector3(3f, 2f, 0f);
            _firePoint.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Physics.SyncTransforms();

            _cannon = new Entity();
            _cannon.AddFireEvent(new Atomic.Elements.Event());
            _cannon.AddFirePoint(_firePoint);

            _tank = new Entity();
            _tank.AddRiggedBody(_body);
            _tank.AddWeapon(new ReactiveVariable<IEntity>(_cannon));

            new TankRecoilBehaviour(50f).Init(_tank);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_hull);

        [Test]
        public void Shot_KicksTheHullBackAlongTheBarrel()
        {
            _cannon.GetFireEvent().Invoke();

            Vector3 force = _body.GetAccumulatedForce();
            Assert.That(force.magnitude, Is.GreaterThan(0f));
            Assert.That(Vector3.Angle(force, -_firePoint.forward), Is.LessThan(0.1f));
        }

        [Test]
        public void ShotFromAboveTheCentreOfMass_RocksTheHull()
        {
            _cannon.GetFireEvent().Invoke();

            // Pushed back (-X) at a point above the centre: the hull tips about +Z.
            Assert.That(_body.GetAccumulatedTorque().z, Is.GreaterThan(0f));
        }

        [Test]
        public void KinematicWreck_IsNotKicked()
        {
            // A kinematic body reports nonsense from GetAccumulatedForce, so fire while kinematic
            // and read the accumulated force once the body is dynamic again.
            _body.isKinematic = true;

            _cannon.GetFireEvent().Invoke();
            _body.isKinematic = false;

            Assert.That(_body.GetAccumulatedForce(), Is.EqualTo(Vector3.zero));
            Assert.That(_body.GetAccumulatedTorque(), Is.EqualTo(Vector3.zero));
        }
    }
}
