using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class VectorUseCaseTests
    {
        private GameObject _gameObject;
        private IEntity _entity;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("Entity");
            _gameObject.transform.position = new Vector3(1f, 2f, 3f);
            _entity = new Entity();
            _entity.AddTransform(_gameObject.transform);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_gameObject);

        [Test]
        public void GetDistance_MeasuresFromTheEntityPosition()
        {
            float distance = VectorUseCase.GetDistance(_entity, new Vector3(4f, 6f, 3f));

            Assert.That(distance, Is.EqualTo(5f).Within(1e-5f));
        }

        [Test]
        public void LessOrEqualsDistance_ExactlyOnTheEdge_True()
        {
            Assert.That(VectorUseCase.LessOrEqualsDistance(_entity, new Vector3(4f, 6f, 3f), 5f), Is.True);
        }

        [Test]
        public void LessOrEqualsDistance_JustBeyond_False()
        {
            Assert.That(VectorUseCase.LessOrEqualsDistance(_entity, new Vector3(4f, 6f, 3f), 4.99f), Is.False);
        }
    }
}
