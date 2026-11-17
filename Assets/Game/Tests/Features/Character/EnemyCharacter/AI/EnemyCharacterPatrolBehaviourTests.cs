using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class EnemyCharacterPatrolBehaviourTests
    {
        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _created)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);
            _created.Clear();
        }

        private Transform Point(Vector3 position)
        {
            var gameObject = new GameObject("PatrolPoint");
            _created.Add(gameObject);
            gameObject.transform.position = position;
            return gameObject.transform;
        }

        private IEntity Enemy(params Transform[] points)
        {
            var body = new GameObject("Enemy");
            _created.Add(body);
            IEntity enemy = new Entity();
            enemy.AddTransform(body.transform);
            enemy.AddTarget(new ReactiveVariable<IEntity>());
            enemy.AddPatrolPoints(points);
            enemy.AddMoveDirection(new ReactiveVariable<Vector3>());
            enemy.AddRotateDirection(new ReactiveVariable<Vector3>());
            return enemy;
        }

        private static void AssertDirection(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), $"got {actual}, expected {expected}");

        [Test]
        public void NoTarget_WalksAndFacesTowardsTheFirstPoint()
        {
            IEntity enemy = Enemy(Point(new Vector3(0f, 0f, 8f)), Point(new Vector3(8f, 0f, 8f)));
            var behaviour = new EnemyCharacterPatrolBehaviour();
            behaviour.Init(enemy);

            behaviour.Tick(enemy, 0.02f);

            AssertDirection(enemy.GetMoveDirection().Value, Vector3.forward);
            AssertDirection(enemy.GetRotateDirection().Value, Vector3.forward);
        }

        [Test]
        public void ReachingAPoint_MovesOnToTheNext()
        {
            IEntity enemy = Enemy(Point(new Vector3(0f, 0f, 8f)), Point(new Vector3(8f, 0f, 8f)));
            var behaviour = new EnemyCharacterPatrolBehaviour();
            behaviour.Init(enemy);

            enemy.GetTransform().position = new Vector3(0f, 0f, 8f);
            behaviour.Tick(enemy, 0.02f);

            AssertDirection(enemy.GetMoveDirection().Value, Vector3.right);
        }

        [Test]
        public void WithATarget_LeavesTheDirectionsToTheChase()
        {
            IEntity enemy = Enemy(Point(new Vector3(0f, 0f, 8f)));
            var behaviour = new EnemyCharacterPatrolBehaviour();
            behaviour.Init(enemy);
            enemy.GetTarget().Value = new Entity();
            enemy.GetMoveDirection().Value = Vector3.left;

            behaviour.Tick(enemy, 0.02f);

            Assert.That(enemy.GetMoveDirection().Value, Is.EqualTo(Vector3.left));
        }

        [Test]
        public void UnassignedPatrolPoints_AreSkipped()
        {
            IEntity enemy = Enemy(null, Point(new Vector3(-6f, 0f, 0f)), null);
            var behaviour = new EnemyCharacterPatrolBehaviour();

            Assert.DoesNotThrow(() => behaviour.Init(enemy));
            behaviour.Tick(enemy, 0.02f);

            AssertDirection(enemy.GetMoveDirection().Value, Vector3.left);
        }

        [Test]
        public void ReInitAfterAStun_RestartsFromTheFirstPoint()
        {
            IEntity enemy = Enemy(Point(new Vector3(0f, 0f, 8f)), Point(new Vector3(8f, 0f, 8f)));
            var behaviour = new EnemyCharacterPatrolBehaviour();
            behaviour.Init(enemy);
            enemy.GetTransform().position = new Vector3(0f, 0f, 8f);
            behaviour.Tick(enemy, 0.02f);
            enemy.GetTransform().position = Vector3.zero;

            behaviour.Init(enemy);
            behaviour.Tick(enemy, 0.02f);

            AssertDirection(enemy.GetMoveDirection().Value, Vector3.forward);
        }

        [Test]
        public void NoPatrolPoints_StandsStill()
        {
            IEntity enemy = Enemy();
            var behaviour = new EnemyCharacterPatrolBehaviour();
            behaviour.Init(enemy);

            behaviour.Tick(enemy, 0.02f);

            Assert.That(enemy.GetMoveDirection().Value, Is.EqualTo(Vector3.zero));
        }
    }
}
