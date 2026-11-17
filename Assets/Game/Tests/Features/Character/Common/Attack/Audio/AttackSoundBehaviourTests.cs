using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// What is checkable without an audio device: the guards that keep an attack from throwing
    /// when a prefab ships without clips or without a source.
    /// </summary>
    public sealed class AttackSoundBehaviourTests
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

        private AudioSource Source()
        {
            var gameObject = new GameObject("Voice");
            _created.Add(gameObject);
            return gameObject.AddComponent<AudioSource>();
        }

        private static IEntity Swing(AttackSoundBehaviour behaviour)
        {
            IEntity weapon = new Entity();
            weapon.AddFireEvent(new Atomic.Elements.Event());
            IEntity character = new Entity();
            character.AddWeapon(new ReactiveVariable<IEntity>(weapon));
            behaviour.Init(character);
            return weapon;
        }

        [Test]
        public void EmptyClipList_AttackDoesNotThrow()
        {
            IEntity weapon = Swing(new AttackSoundBehaviour(new AudioClip[0], this.Source()));

            Assert.DoesNotThrow(() => weapon.GetFireEvent().Invoke());
        }

        [Test]
        public void NoClipList_AttackDoesNotThrow()
        {
            IEntity weapon = Swing(new AttackSoundBehaviour(null, this.Source()));

            Assert.DoesNotThrow(() => weapon.GetFireEvent().Invoke());
        }

        [Test]
        public void NoAudioSource_AttackDoesNotThrow()
        {
            AudioClip clip = AudioClip.Create("Swing", 441, 1, 44100, false);
            try
            {
                IEntity weapon = Swing(new AttackSoundBehaviour(new[] { clip }, null));

                Assert.DoesNotThrow(() => weapon.GetFireEvent().Invoke());
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }
    }
}
