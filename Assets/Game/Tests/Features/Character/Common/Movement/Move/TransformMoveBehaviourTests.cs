using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class TransformMoveBehaviourTests
    {
        private GameObject _body;
        private IEntity _enemy;
        private TransformMoveBehaviour _behaviour;
        private bool _canMove;

        [SetUp]
        public void SetUp()
        {
            _canMove = true;
            _body = new GameObject("Enemy");
            _enemy = new Entity();
            _enemy.AddTransform(_body.transform);
            _enemy.AddMoveSpeed(new ReactiveFloat(3f));
            _enemy.AddMoveDirection(new ReactiveVariable<Vector3>(Vector3.forward));
            _enemy.AddMoveCondition(new AndExpression(new ExpressionMember<bool>(() => _canMove)));
            _behaviour = new TransformMoveBehaviour();
            _behaviour.Init(_enemy);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_body);

        [Test]
        public void Tick_WalksAlongTheMoveDirection()
        {
            _behaviour.Tick(_enemy, 0.5f);

            Assert.That(_body.transform.position.z, Is.EqualTo(1.5f).Within(1e-4f));
        }

        [Test]
        public void Tick_FollowsADirectionChangedAfterInit()
        {
            _enemy.GetMoveDirection().Value = Vector3.left;

            _behaviour.Tick(_enemy, 1f);

            Assert.That(_body.transform.position.x, Is.EqualTo(-3f).Within(1e-4f));
        }

        [Test]
        public void Tick_NotAllowed_StaysPut()
        {
            _canMove = false;

            _behaviour.Tick(_enemy, 0.5f);

            Assert.That(_body.transform.position, Is.EqualTo(Vector3.zero));
        }
    }
}
