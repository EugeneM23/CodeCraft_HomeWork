using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class CharacterVelocityBehaviourTests
    {
        private GameObject _body;
        private IEntity _character;
        private CharacterVelocityBehaviour _behaviour;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Character");
            _character = new Entity();
            _character.AddTransform(_body.transform);
            _character.AddVelocity(new ReactiveFloat());
            _behaviour = new CharacterVelocityBehaviour();
            _behaviour.Init(_character);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_body);

        [Test]
        public void MovedTwoMetresInHalfASecond_IsFourMetresPerSecond()
        {
            _body.transform.position = new Vector3(2f, 0f, 0f);

            _behaviour.Tick(_character, 0.5f);

            Assert.That(_character.GetVelocity().Value, Is.EqualTo(4f).Within(1e-5f));
        }

        [Test]
        public void MeasuresFromTheLastFrame_NotFromTheStart()
        {
            _body.transform.position = new Vector3(2f, 0f, 0f);
            _behaviour.Tick(_character, 0.5f);

            _behaviour.Tick(_character, 0.5f);

            Assert.That(_character.GetVelocity().Value, Is.EqualTo(0f));
        }

        [Test]
        public void ZeroDeltaTime_ReportsZero_NotInfinity()
        {
            _body.transform.position = new Vector3(2f, 0f, 0f);

            _behaviour.Tick(_character, 0f);

            Assert.That(_character.GetVelocity().Value, Is.EqualTo(0f));
        }

        [Test]
        public void CountsEveryAxis()
        {
            _body.transform.position = new Vector3(3f, 0f, 4f);

            _behaviour.Tick(_character, 1f);

            Assert.That(_character.GetVelocity().Value, Is.EqualTo(5f).Within(1e-5f));
        }
    }
}
