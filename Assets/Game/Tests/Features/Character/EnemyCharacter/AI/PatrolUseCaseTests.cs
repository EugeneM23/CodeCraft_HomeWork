using System.Collections.Generic;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class PatrolUseCaseTests
    {
        private static readonly List<Vector3> Points = new()
        {
            new Vector3(10f, 0f, 0f),
            new Vector3(10f, 0f, 10f),
            new Vector3(0f, 0f, 10f),
        };

        private GameObject _body;
        private IEntity _enemy;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Enemy");
            _enemy = new Entity();
            _enemy.AddTransform(_body.transform);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_body);

        private static void AssertDirection(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), $"got {actual}, expected {expected}");

        [Test]
        public void FarFromThePoint_HeadsToIt_AtUnitLength()
        {
            int index = 0;

            Vector3 direction = PatrolUseCase.GetDirectionToPatrolPoint(_enemy, Points, ref index);

            AssertDirection(direction, Vector3.right);
            Assert.That(index, Is.Zero);
        }

        [Test]
        public void ReachedThePoint_HeadsToTheNextOne()
        {
            _body.transform.position = new Vector3(9.7f, 0f, 0f);
            int index = 0;

            Vector3 direction = PatrolUseCase.GetDirectionToPatrolPoint(_enemy, Points, ref index);

            Assert.That(index, Is.EqualTo(1));
            AssertDirection(direction, new Vector3(0.3f, 0f, 10f).normalized);
        }

        [Test]
        public void ReachedTheLastPoint_WrapsToTheFirst()
        {
            _body.transform.position = new Vector3(0f, 0f, 10f);
            int index = 2;

            Vector3 direction = PatrolUseCase.GetDirectionToPatrolPoint(_enemy, Points, ref index);

            Assert.That(index, Is.Zero);
            AssertDirection(direction, new Vector3(10f, 0f, -10f).normalized);
        }

        [Test]
        public void NoPoints_StandsStill()
        {
            int index = 0;

            Assert.That(PatrolUseCase.GetDirectionToPatrolPoint(_enemy, new List<Vector3>(), ref index), Is.EqualTo(Vector3.zero));
            Assert.That(PatrolUseCase.GetDirectionToPatrolPoint(_enemy, null, ref index), Is.EqualTo(Vector3.zero));
        }
    }
}
