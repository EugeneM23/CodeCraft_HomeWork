using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MoveStepSoundBehaviourTests
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

        private IEntity Walker(float speed)
        {
            var gameObject = new GameObject("Walker");
            _created.Add(gameObject);
            IEntity walker = new Entity();
            walker.AddAudioSource(gameObject.AddComponent<AudioSource>());
            walker.AddVelocity(new ReactiveFloat(speed));
            return walker;
        }

        [Test]
        public void Walking_WithAnEmptyClipList_DoesNotThrowOnTheStep()
        {
            // AttackSoundBehaviour was fixed for exactly this: Random.Range(0, 0) is 0 and an empty
            // array has no element 0. The step sound has no such guard.
            IEntity walker = Walker(3f);
            var behaviour = new MoveStepSoundBehaviour(new AudioClip[0], 0.35f);
            behaviour.Init(walker);

            Assert.DoesNotThrow(() => behaviour.Tick(walker, 0.5f));
        }
    }
}
