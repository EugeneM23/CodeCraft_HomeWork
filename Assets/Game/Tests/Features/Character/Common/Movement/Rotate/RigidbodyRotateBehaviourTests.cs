using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class RigidbodyRotateBehaviourTests
    {
        private const float ROTATION_SPEED = 5f;

        private GameObject _gameObject;
        private Rigidbody _body;
        private IEntity _character;
        private RigidbodyRotateBehaviour _behaviour;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("Character");
            _body = _gameObject.AddComponent<Rigidbody>();
            _body.useGravity = false;
            _character = new Entity();
            _character.AddRiggedBody(_body);
            _character.AddRotationSpeed(new Variable<float>(ROTATION_SPEED));
            _character.AddRotateDirection(new ReactiveVariable<Vector3>());
            _behaviour = new RigidbodyRotateBehaviour();
            _behaviour.Init(_character);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_gameObject);

        [Test]
        public void Step_TurnsTheBodyPartWayTowardsTheDirection()
        {
            _character.GetRotateDirection().Value = Vector3.right;

            _behaviour.FixedTick(_character, 0.1f);

            float left = Quaternion.Angle(_body.rotation, Quaternion.LookRotation(Vector3.right));
            Assert.That(left, Is.EqualTo(90f * Mathf.Exp(-ROTATION_SPEED * 0.1f)).Within(0.05f));
        }

        [Test]
        public void DirectionPointingUp_StaysUpright()
        {
            _character.GetRotateDirection().Value = new Vector3(1f, 5f, 0f);

            _behaviour.FixedTick(_character, 10f);

            Vector3 up = _body.rotation * Vector3.up;
            Assert.That(Vector3.Dot(up, Vector3.up), Is.GreaterThan(0.999f));
        }

        [Test]
        public void StraightUpOrNoDirection_KeepsTheCurrentFacing()
        {
            Quaternion before = _body.rotation;

            _character.GetRotateDirection().Value = Vector3.up;
            _behaviour.FixedTick(_character, 0.1f);
            _character.GetRotateDirection().Value = Vector3.zero;
            _behaviour.FixedTick(_character, 0.1f);

            Assert.That(Quaternion.Angle(_body.rotation, before), Is.LessThan(1e-3f));
        }
    }
}
