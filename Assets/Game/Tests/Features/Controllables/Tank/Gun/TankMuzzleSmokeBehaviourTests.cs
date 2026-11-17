using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class TankMuzzleSmokeBehaviourTests
    {
        private GameObject _gameObject;
        private ParticleSystem _wisp;
        private IEntity _tank;
        private IEntity _cannon;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("Barrel");
            _wisp = _gameObject.AddComponent<ParticleSystem>();
            _wisp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            _cannon = new Entity();
            _cannon.AddFireEvent(new Atomic.Elements.Event());
            _cannon.AddFirePoint(_gameObject.transform);

            _tank = new Entity();
            _tank.AddWeapon(new ReactiveVariable<IEntity>(_cannon));
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_gameObject);

        private TankMuzzleSmokeBehaviour Install()
        {
            // No burst prefab: only the wisp is under test, so no factory is reached.
            var smoke = new TankMuzzleSmokeBehaviour(null, null, _wisp);
            smoke.Init(_tank);
            return smoke;
        }

        [Test]
        public void Shot_StartsTheWisp()
        {
            Install();
            Assume.That(_wisp.isPlaying, Is.False);

            _cannon.GetFireEvent().Invoke();

            Assert.That(_wisp.isPlaying, Is.True);
        }

        [Test]
        public void AfterDispose_ShotsLeaveTheWispAlone()
        {
            Install().Dispose(_tank);

            _cannon.GetFireEvent().Invoke();

            Assert.That(_wisp.isPlaying, Is.False);
        }

        [Test]
        public void NoCannon_NothingToListenTo()
        {
            _tank.GetWeapon().Value = null;

            Assert.DoesNotThrow(() => Install().Dispose(_tank));
        }
    }
}
