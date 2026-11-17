using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class EnemyCharacterChaseBehaviourTests
    {
        private readonly List<GameObject> _created = new();
        private IEntity _enemy;
        private EnemyCharacterChaseBehaviour _behaviour;

        [SetUp]
        public void SetUp()
        {
            _enemy = new Entity();
            _enemy.AddTransform(Track(new GameObject("Enemy")).transform);
            _enemy.AddTarget(new ReactiveVariable<IEntity>());
            _enemy.AddMoveDirection(new ReactiveVariable<Vector3>(Vector3.back));
            _enemy.AddRotateDirection(new ReactiveVariable<Vector3>(Vector3.back));
            _behaviour = new EnemyCharacterChaseBehaviour();
            _behaviour.Init(_enemy);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _created)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);
            _created.Clear();
        }

        private GameObject Track(GameObject gameObject)
        {
            _created.Add(gameObject);
            return gameObject;
        }

        private IEntity Player(Vector3 position)
        {
            IEntity player = new Entity();
            player.AddTransform(Track(new GameObject("Player")).transform);
            player.GetTransform().position = position;
            return player;
        }

        private static void AssertDirection(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), $"got {actual}, expected {expected}");

        [Test]
        public void FarTarget_HeadsAtItAtUnitLength_NotScaledByDistance()
        {
            _enemy.GetTarget().Value = Player(new Vector3(20f, 0f, 0f));

            _behaviour.Tick(_enemy, 0.02f);

            AssertDirection(_enemy.GetMoveDirection().Value, Vector3.right);
            AssertDirection(_enemy.GetRotateDirection().Value, Vector3.right);
        }

        [Test]
        public void TargetUpOrDown_HeadsAlongTheGroundOnly()
        {
            _enemy.GetTarget().Value = Player(new Vector3(0f, 5f, 3f));

            _behaviour.Tick(_enemy, 0.02f);

            AssertDirection(_enemy.GetMoveDirection().Value, Vector3.forward);
        }

        [Test]
        public void StandingOnTheTarget_StandsStill()
        {
            _enemy.GetTarget().Value = Player(new Vector3(0f, 2f, 0f));

            _behaviour.Tick(_enemy, 0.02f);

            Assert.That(_enemy.GetMoveDirection().Value, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void NoTarget_LeavesTheDirectionsToPatrol()
        {
            _behaviour.Tick(_enemy, 0.02f);

            Assert.That(_enemy.GetMoveDirection().Value, Is.EqualTo(Vector3.back));
            Assert.That(_enemy.GetRotateDirection().Value, Is.EqualTo(Vector3.back));
        }

        [Test]
        public void TargetDied_IsDropped_WithoutThrowing()
        {
            IEntity player = Player(new Vector3(5f, 0f, 0f));
            _enemy.GetTarget().Value = player;
            player.Dispose();

            Assert.DoesNotThrow(() => _behaviour.Tick(_enemy, 0.02f));
            Assert.That(_enemy.GetTarget().Value, Is.Null);
            Assert.That(_enemy.GetMoveDirection().Value, Is.EqualTo(Vector3.back));
        }
    }
}
