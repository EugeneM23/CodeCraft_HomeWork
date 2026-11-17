using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Saving.Tests
{
    /// <summary>Built-in serializers of players saved while stopped: they come back stopped.</summary>
    public class BuiltInSerializerEdgeTests
    {
        private readonly List<GameObject> _objects = new();
        private SaveSystemSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
            _settings.SetAssetCatalog(TestScenes.EntriesOf(TestScenes.TONE));
            SaveReferences.Settings = _settings;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _objects)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }

            _objects.Clear();
            SaveReferences.Settings = null;
            Object.DestroyImmediate(_settings);
        }

        private GameObject Make(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }

        private static IComponentSerializer For<T>() => ComponentSerializerCatalog.Default.For(typeof(T));

#if SAVE_AUDIO
        [UnityTest]
        public IEnumerator AudioSource_Stopped_StaysStoppedWithItsSettings()
        {
            var source = Make("Stopped").AddComponent<AudioSource>();
            source.clip = TestScenes.Tone;
            source.volume = 0.4f;
            JToken data = For<AudioSource>().Capture(source);

            source.volume = 1f;
            source.Play();
            yield return null;
            For<AudioSource>().Restore(source, data);

            Assert.IsFalse(source.isPlaying);
            Assert.AreSame(TestScenes.Tone, source.clip);
            Assert.AreEqual(0.4f, source.volume, 1e-4f);
        }
#endif

#if SAVE_PARTICLES
        [UnityTest]
        public IEnumerator ParticleSystem_Stopped_StaysStopped()
        {
            var particles = Make("Stopped").AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            JToken data = For<ParticleSystem>().Capture(particles);

            particles.Play();
            yield return null;
            For<ParticleSystem>().Restore(particles, data);

            Assert.IsFalse(particles.isPlaying);
            Assert.IsFalse(particles.isPaused);
            Assert.AreEqual(0, particles.particleCount);
        }
#endif
    }
}
