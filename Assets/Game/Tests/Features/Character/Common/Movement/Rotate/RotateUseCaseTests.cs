using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Turning towards a facing with frame-rate independent smoothing: the share of the turn left
    /// after a step is exp(-speed * deltaTime), however that time is cut into frames.
    /// </summary>
    public sealed class RotateUseCaseTests
    {
        private const float ROTATION_SPEED = 5f;

        private GameObject _body;
        private IEntity _character;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Character");
            _character = new Entity();
            _character.AddTransform(_body.transform);
            _character.AddRotationSpeed(new Variable<float>(ROTATION_SPEED));
            _character.AddRotateDirection(new ReactiveVariable<Vector3>());
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_body);

        private float AngleLeftTo(Vector3 direction) =>
            Quaternion.Angle(_body.transform.rotation, Quaternion.LookRotation(direction));

        [Test]
        public void OneStep_LeavesExpOfMinusSpeedTimesDeltaTimeOfTheTurn()
        {
            _character.Rotate(Vector3.right, 0.1f);

            Assert.That(AngleLeftTo(Vector3.right), Is.EqualTo(90f * Mathf.Exp(-ROTATION_SPEED * 0.1f)).Within(0.05f));
        }

        [Test]
        public void TwoHalfSteps_TurnAsFarAsOneWholeStep()
        {
            _character.Rotate(Vector3.right, 0.05f);
            _character.Rotate(Vector3.right, 0.05f);
            float halves = AngleLeftTo(Vector3.right);

            _body.transform.rotation = Quaternion.identity;
            _character.Rotate(Vector3.right, 0.1f);
            float whole = AngleLeftTo(Vector3.right);

            Assert.That(halves, Is.EqualTo(whole).Within(0.05f));
        }

        [Test]
        public void LongFrame_NeverOvershootsTheFacing()
        {
            _character.Rotate(Vector3.right, 10f);

            Assert.That(AngleLeftTo(Vector3.right), Is.LessThan(0.05f));
            Assert.That(Vector3.Dot(_body.transform.forward, Vector3.right), Is.GreaterThan(0.999f));
        }

        [Test]
        public void NoDirection_KeepsTheCurrentFacing()
        {
            _body.transform.rotation = Quaternion.Euler(0f, 30f, 0f);

            _character.Rotate(Vector3.zero, 0.1f);

            Assert.That(Quaternion.Angle(_body.transform.rotation, Quaternion.Euler(0f, 30f, 0f)), Is.LessThan(1e-3f));
        }

        [Test]
        public void TransformRotateBehaviour_TurnsTowardsTheRotateDirection()
        {
            var behaviour = new TransformRotateBehaviour();
            behaviour.Init(_character);
            _character.GetRotateDirection().Value = Vector3.back;

            behaviour.Tick(_character, 0.1f);

            Assert.That(AngleLeftTo(Vector3.back), Is.EqualTo(180f * Mathf.Exp(-ROTATION_SPEED * 0.1f)).Within(0.05f));
        }
    }
}
