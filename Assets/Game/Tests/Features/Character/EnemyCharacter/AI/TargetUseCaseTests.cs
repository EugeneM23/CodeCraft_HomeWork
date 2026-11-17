using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class TargetUseCaseTests
    {
        private GameObject _player;

        [SetUp]
        public void SetUp() => _player = new GameObject("Player");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_player);

        private IEntity Player()
        {
            IEntity player = new Entity();
            player.AddTransform(_player.transform);
            return player;
        }

        [Test]
        public void NoTarget_ResolvesNothing()
        {
            var slot = new ReactiveVariable<IEntity>(null);

            Assert.That(TargetUseCase.TryResolve(slot, out Transform transform), Is.False);
            Assert.That(transform, Is.Null);
        }

        [Test]
        public void LivingTarget_ResolvesToItsTransform_AndIsKept()
        {
            IEntity player = Player();
            var slot = new ReactiveVariable<IEntity>(player);

            Assert.That(TargetUseCase.TryResolve(slot, out Transform transform), Is.True);
            Assert.That(transform, Is.SameAs(_player.transform));
            Assert.That(slot.Value, Is.SameAs(player));
        }

        [Test]
        public void TargetThatDied_IsForgotten()
        {
            IEntity player = Player();
            var slot = new ReactiveVariable<IEntity>(player);

            player.Dispose();

            Assert.That(TargetUseCase.TryResolve(slot, out _), Is.False);
            Assert.That(slot.Value, Is.Null);
        }
    }
}
