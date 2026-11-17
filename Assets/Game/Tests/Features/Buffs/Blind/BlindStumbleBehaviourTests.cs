using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class BlindStumbleBehaviourTests
    {
        private const float Speed = 0.35f;

        private Random.State _random;

        [SetUp]
        public void SetUp()
        {
            _random = Random.state;
            Random.InitState(12345);
        }

        [TearDown]
        public void TearDown() => Random.state = _random;

        private static IEntity Enemy(out BlindStumbleBehaviour stumble)
        {
            IEntity enemy = new Entity();
            enemy.AddMoveDirection(new ReactiveVector3());
            enemy.AddRotateDirection(new ReactiveVector3());

            stumble = new BlindStumbleBehaviour(Speed);
            stumble.Init(enemy);
            return enemy;
        }

        [Test]
        public void FirstTick_TurnsAtOnceAndShufflesAtTheGivenShareOfSpeed()
        {
            IEntity enemy = Enemy(out BlindStumbleBehaviour stumble);

            stumble.Tick(enemy, 0.02f);

            Vector3 move = enemy.GetMoveDirection().Value;
            Assert.That(move.magnitude, Is.EqualTo(Speed).Within(1e-4f));
            Assert.That(move.y, Is.Zero);
        }

        [Test]
        public void Tick_FacesTheWayItShuffles()
        {
            IEntity enemy = Enemy(out BlindStumbleBehaviour stumble);

            stumble.Tick(enemy, 0.02f);

            Vector3 rotate = enemy.GetRotateDirection().Value;
            Assert.That(rotate.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(Vector3.Angle(rotate, enemy.GetMoveDirection().Value), Is.LessThan(0.01f));
        }

        [Test]
        public void Tick_WithinTheTurnInterval_KeepsTheDirection()
        {
            IEntity enemy = Enemy(out BlindStumbleBehaviour stumble);
            stumble.Tick(enemy, 0.02f);
            Vector3 first = enemy.GetMoveDirection().Value;

            // The shortest interval between turns is 0.6 s.
            stumble.Tick(enemy, 0.5f);

            Assert.That(enemy.GetMoveDirection().Value, Is.EqualTo(first));
        }

        [Test]
        public void Tick_PastTheLongestInterval_TurnsAgain()
        {
            IEntity enemy = Enemy(out BlindStumbleBehaviour stumble);
            stumble.Tick(enemy, 0.02f);
            Vector3 first = enemy.GetMoveDirection().Value;

            // The longest interval between turns is 1.2 s.
            stumble.Tick(enemy, 1.3f);

            Assert.That(enemy.GetMoveDirection().Value, Is.Not.EqualTo(first));
            Assert.That(enemy.GetMoveDirection().Value.magnitude, Is.EqualTo(Speed).Within(1e-4f));
        }
    }
}
