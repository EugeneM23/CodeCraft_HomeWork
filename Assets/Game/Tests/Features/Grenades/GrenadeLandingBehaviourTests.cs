using System;
using System.Reflection;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class GrenadeLandingBehaviourTests
    {
        private const float GroundDamping = 5f;

        private GameObject _grenadeObject;
        private Rigidbody _body;
        private CollisionEventReceiver _receiver;
        private IEntity _grenade;

        [SetUp]
        public void SetUp()
        {
            _grenadeObject = new GameObject("Grenade");
            _body = _grenadeObject.AddComponent<Rigidbody>();
            _receiver = _grenadeObject.AddComponent<CollisionEventReceiver>();

            _grenade = new Entity();
            _grenade.AddBehaviour(new GrenadeLandingBehaviour(_body, _receiver, GroundDamping));
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_grenadeObject);

        /// <summary>
        /// What OnCollisionEnter does in Play mode. The behaviour ignores the collision itself, so
        /// none is needed: a Collision cannot be built outside the physics step anyway.
        /// </summary>
        private void Touch()
        {
            FieldInfo field = typeof(CollisionEventReceiver)
                .GetField(nameof(CollisionEventReceiver.OnEntered), BindingFlags.Instance | BindingFlags.NonPublic);
            ((Action<Collision>)field.GetValue(_receiver))?.Invoke(null);
        }

        [Test]
        public void InFlight_BodyFliesWithoutDamping()
        {
            _body.linearDamping = 3f;

            _grenade.Enable();

            Assert.That(_body.linearDamping, Is.EqualTo(0f));
        }

        [Test]
        public void FirstContact_BrakesTheBody()
        {
            _grenade.Enable();

            Touch();

            Assert.That(_body.linearDamping, Is.EqualTo(GroundDamping));
        }

        [Test]
        public void RentedAgainFromThePool_FliesFreelyOnceMore()
        {
            _grenade.Enable();
            Touch();

            _grenade.Disable();
            _grenade.Enable();

            Assert.That(_body.linearDamping, Is.EqualTo(0f));
        }

        [Test]
        public void AfterDispose_ContactsAreIgnored()
        {
            _grenade.Enable();

            _grenade.Dispose();
            Touch();

            Assert.That(_body.linearDamping, Is.EqualTo(0f));
        }
    }
}
