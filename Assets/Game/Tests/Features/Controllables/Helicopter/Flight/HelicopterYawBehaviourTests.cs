using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class HelicopterYawBehaviourTests
    {
        private static readonly Vector3 Origin = new(-4200f, 300f, 4200f);

        private GameObject _gameObject;
        private Rigidbody _body;
        private IEntity _helicopter;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("Helicopter");
            _gameObject.transform.position = Origin;
            _body = _gameObject.AddComponent<Rigidbody>();
            _body.useGravity = false;
            Physics.SyncTransforms();

            _helicopter = new Entity();
            _helicopter.AddRiggedBody(_body);
            _helicopter.AddRotateDirection(new ReactiveVariable<Vector3>(Vector3.forward));
            _helicopter.AddRotationSpeed(new ReactiveVariable<float>(90f));
            _helicopter.AddDriver(new ReactiveVariable<IEntity>(new Entity()));
            _helicopter.AddFlightState(new ReactiveVariable<HelicopterFlightState>(HelicopterFlightState.Flying));
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_gameObject);

        [TestCase(HelicopterFlightState.Flying)]
        [TestCase(HelicopterFlightState.Landed)]
        public void KnockedIntoASpin_SpinIsTakenOut(HelicopterFlightState state)
        {
            _helicopter.GetFlightState().Value = state;
            var yaw = new HelicopterYawBehaviour(360f);
            yaw.Init(_helicopter);
            _body.angularVelocity = new Vector3(1f, 3f, -2f);

            yaw.FixedTick(_helicopter, 0.02f);

            Assert.That(_body.angularVelocity, Is.EqualTo(Vector3.zero));
        }
    }
}
