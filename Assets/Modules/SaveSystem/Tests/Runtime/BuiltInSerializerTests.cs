using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace Saving.Tests
{
    /// <summary>Each built-in serializer: a round trip on a live component.</summary>
    public class BuiltInSerializerTests
    {
        private readonly List<GameObject> _objects = new();
        private SaveSystemSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
            var entries = new List<SaveSystemSettings.AssetEntry>(TestScenes.EntriesOf(TestScenes.ICON));
            entries.AddRange(TestScenes.EntriesOf(TestScenes.TONE));
            _settings.SetAssetCatalog(entries);
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

#if SAVE_PHYSICS
        [Test]
        public void Transform_WithCharacterController_NextMoveKeepsTheLoadedPosition()
        {
            // A level loaded after the first scene: the controller is already awake and holds the scene position
            bool sync = Physics.autoSyncTransforms;
            Physics.autoSyncTransforms = false;
            try
            {
                var controller = Make("Player").AddComponent<CharacterController>();
                controller.transform.position = new Vector3(5f, 0f, 3f);
                JToken data = For<Transform>().Capture(controller.transform);
                controller.transform.position = Vector3.zero;
                Physics.SyncTransforms();

                For<Transform>().Restore(controller.transform, data);
                controller.Move(Vector3.zero);

                Assert.That(Vector3.Distance(controller.transform.position, new Vector3(5f, 0f, 3f)), Is.LessThan(0.01f));
            }
            finally
            {
                Physics.autoSyncTransforms = sync;
            }
        }
#endif

#if SAVE_PHYSICS_2D
        [Test]
        public void Rigidbody2D_BodyAndMotion_RoundTrip()
        {
            var body = Make("Body2D").AddComponent<Rigidbody2D>();
            body.gravityScale = 0.5f;
            body.position = new Vector2(1f, 2f);
            body.rotation = 30f;
            body.linearVelocity = new Vector2(3f, -4f);
            body.angularVelocity = 90f;
            JToken data = For<Rigidbody2D>().Capture(body);

            body.bodyType = RigidbodyType2D.Kinematic;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            body.gravityScale = 1f;
            body.position = Vector2.zero;
            body.rotation = 0f;
            For<Rigidbody2D>().Restore(body, data);

            Assert.AreEqual(RigidbodyType2D.Dynamic, body.bodyType);
            Assert.AreEqual(RigidbodyConstraints2D.None, body.constraints);
            Assert.AreEqual(0.5f, body.gravityScale);
            Assert.AreEqual(new Vector2(1f, 2f), body.position);
            Assert.AreEqual(30f, body.rotation, 1e-3f);
            Assert.AreEqual(new Vector2(3f, -4f), body.linearVelocity);
            Assert.AreEqual(90f, body.angularVelocity, 1e-3f);
        }

        [Test]
        public void Rigidbody2D_Asleep_StaysAsleep()
        {
            var body = Make("Sleeper2D").AddComponent<Rigidbody2D>();
            body.Sleep();
            JToken data = For<Rigidbody2D>().Capture(body);
            body.WakeUp();

            For<Rigidbody2D>().Restore(body, data);

            Assert.IsFalse(body.IsAwake());
        }
#endif

#if SAVE_PHYSICS
        [Test]
        public void Rigidbody_BodyAndMotion_RoundTrip()
        {
            var body = Make("Body").AddComponent<Rigidbody>();
            body.useGravity = false;
            body.linearVelocity = new Vector3(1f, 2f, 3f);
            body.angularVelocity = new Vector3(0f, 0f, 2f);
            JToken data = For<Rigidbody>().Capture(body);

            body.useGravity = true;
            body.constraints = RigidbodyConstraints.FreezeAll;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            For<Rigidbody>().Restore(body, data);

            Assert.IsFalse(body.useGravity);
            Assert.AreEqual(RigidbodyConstraints.None, body.constraints);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), body.linearVelocity);
            Assert.AreEqual(new Vector3(0f, 0f, 2f), body.angularVelocity);
        }

        [Test]
        public void Rigidbody_Asleep_StaysAsleep()
        {
            var body = Make("Sleeper").AddComponent<Rigidbody>();
            body.Sleep();
            JToken data = For<Rigidbody>().Capture(body);
            body.WakeUp();

            For<Rigidbody>().Restore(body, data);

            Assert.IsTrue(body.IsSleeping());
        }
#endif

#if SAVE_PHYSICS && SAVE_PHYSICS_2D
        [Test]
        public void Colliders_ListedAndEnabledKept()
        {
            GameObject go = Make("Colliders");
            var saveable = go.AddComponent<Saveable>();
            var box2D = go.AddComponent<BoxCollider2D>();
            // 2D and 3D physics do not mix on one object: the 3D collider sits on a child
            var solid = new GameObject("Solid");
            solid.transform.SetParent(go.transform, false);
            var box = solid.AddComponent<BoxCollider>();
            box2D.enabled = false;
            box.enabled = false;
            ObjectData data = ObjectState.Capture(saveable, ComponentSerializerCatalog.Default);

            box2D.enabled = true;
            box.enabled = true;
            ObjectState.RestoreComponents(saveable, data, ComponentSerializerCatalog.Default);

            Assert.IsTrue(data.Components.ContainsKey("BoxCollider2D"));
            Assert.IsTrue(data.Components.ContainsKey("Solid/BoxCollider"));
            Assert.IsFalse(box2D.enabled);
            Assert.IsFalse(box.enabled);
        }
#endif

#if SAVE_ANIMATION && UNITY_EDITOR
        [UnityTest]
        public IEnumerator Animator_StateTimeAndParameters_RoundTrip()
        {
            var animator = Make("Animated").AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(TestScenes.ANIMATOR);
            yield return null;
            animator.Play("Slide", 0, 0.25f);
            animator.SetFloat("Speed", 2.5f);
            animator.SetInteger("Count", 3);
            animator.SetBool("On", true);
            animator.speed = 0.5f;
            animator.Update(0f);
            float savedTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            JToken data = For<Animator>().Capture(animator);

            animator.Play("Idle", 0, 0.9f);
            animator.SetFloat("Speed", 0f);
            animator.SetInteger("Count", 0);
            animator.SetBool("On", false);
            animator.speed = 1f;
            animator.Update(0f);
            For<Animator>().Restore(animator, data);

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            Assert.AreEqual(Animator.StringToHash("Base Layer.Slide"), state.fullPathHash);
            Assert.AreEqual(savedTime, state.normalizedTime, 0.01f);
            Assert.AreEqual(2.5f, animator.GetFloat("Speed"));
            Assert.AreEqual(3, animator.GetInteger("Count"));
            Assert.IsTrue(animator.GetBool("On"));
            Assert.AreEqual(0.5f, animator.speed);
        }

        [Test]
        public void Animator_WithoutController_OnlySpeed()
        {
            var animator = Make("Bare").AddComponent<Animator>();
            animator.speed = 2f;

            JToken data = For<Animator>().Capture(animator);
            animator.speed = 1f;
            For<Animator>().Restore(animator, data);

            Assert.AreEqual(2f, animator.speed);
            Assert.AreEqual(0, ((JArray)data["layers"]).Count);
        }
#endif

#if SAVE_PARTICLES
        // A looping system no camera sees is paused by culling: tests have no camera, so it always simulates
        private static ParticleSystem MakeParticles(GameObject go)
        {
            var particles = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            return particles;
        }

        [UnityTest]
        public IEnumerator ParticleSystem_PlayingAndTime_RoundTrip()
        {
            ParticleSystem particles = MakeParticles(Make("Particles"));
            particles.Play();
            yield return new WaitForSeconds(0.3f);
            float time = particles.time;
            JToken data = For<ParticleSystem>().Capture(particles);

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            For<ParticleSystem>().Restore(particles, data);

            Assert.IsTrue(particles.isPlaying);
            Assert.AreEqual(time, particles.time, 0.05f);
        }

        [UnityTest]
        public IEnumerator ParticleSystem_Paused_StaysPaused()
        {
            ParticleSystem particles = MakeParticles(Make("Paused"));
            particles.Play();
            yield return new WaitForSeconds(0.2f);
            particles.Pause();
            JToken data = For<ParticleSystem>().Capture(particles);

            particles.Play();
            For<ParticleSystem>().Restore(particles, data);

            Assert.IsTrue(particles.isPaused);
        }

        [UnityTest]
        public IEnumerator ParticleSystem_LiveParticles_OnlyWithTheOption()
        {
            GameObject go = Make("Live");
            var saveable = go.AddComponent<Saveable>();
            ParticleSystem particles = MakeParticles(go);
            particles.Play();
            yield return new WaitForSeconds(0.5f);
            particles.Pause();
            var live = new ParticleSystem.Particle[particles.particleCount];
            int count = particles.GetParticles(live);
            Assert.Greater(count, 0, "precondition: some particles are alive");

            ObjectData without = ObjectState.Capture(saveable, ComponentSerializerCatalog.Default);
            saveable.SetOption("ParticleSystem", ParticleSystemSerializer.LIVE_PARTICLES, true);
            ObjectData with = ObjectState.Capture(saveable, ComponentSerializerCatalog.Default);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ObjectState.RestoreComponents(saveable, with, ComponentSerializerCatalog.Default);

            Assert.AreEqual(JTokenType.Null, without.Components["ParticleSystem"].Data["particles"].Type);
            var restored = new ParticleSystem.Particle[particles.particleCount];
            Assert.AreEqual(count, particles.GetParticles(restored));
            Assert.AreEqual(live[0].position, restored[0].position);
        }
#endif

        [Test]
        public void SpriteRenderer_SpriteColorAndFlip_RoundTrip()
        {
            var renderer = Make("Sprite").AddComponent<SpriteRenderer>();
            renderer.sprite = TestScenes.Icon;
            renderer.color = new Color(1f, 0.5f, 0.25f, 0.8f);
            renderer.flipX = true;
            JToken data = For<SpriteRenderer>().Capture(renderer);

            renderer.sprite = null;
            renderer.color = Color.white;
            renderer.flipX = false;
            renderer.flipY = true;
            For<SpriteRenderer>().Restore(renderer, data);

            Assert.AreSame(TestScenes.Icon, renderer.sprite);
            Assert.AreEqual(new Color(1f, 0.5f, 0.25f, 0.8f), renderer.color);
            Assert.IsTrue(renderer.flipX);
            Assert.IsFalse(renderer.flipY);
            Assert.IsNotNull(data["sprite"]["asset"], "the sprite is saved as an asset reference");
        }

#if SAVE_AUDIO
        [UnityTest]
        public IEnumerator AudioSource_PlayingClipAndTime_RoundTrip()
        {
            var source = Make("Audio").AddComponent<AudioSource>();
            source.clip = TestScenes.Tone;
            source.volume = 0.3f;
            source.pitch = 1.2f;
            source.Play();
            yield return new WaitForSeconds(0.3f);
            float time = source.time;
            Assert.Greater(time, 0f, "precondition: the clip is playing");
            JToken data = For<AudioSource>().Capture(source);

            source.Stop();
            source.clip = null;
            source.volume = 1f;
            source.pitch = 1f;
            For<AudioSource>().Restore(source, data);

            Assert.AreSame(TestScenes.Tone, source.clip);
            Assert.IsTrue(source.isPlaying);
            Assert.AreEqual(time, source.time, 0.1f);
            Assert.AreEqual(0.3f, source.volume, 1e-4f);
            Assert.AreEqual(1.2f, source.pitch, 1e-4f);
        }

        [UnityTest]
        public IEnumerator AudioSource_Paused_StaysPaused()
        {
            var source = Make("Paused").AddComponent<AudioSource>();
            source.clip = TestScenes.Tone;
            source.Play();
            yield return new WaitForSeconds(0.3f);
            source.Pause();
            float time = source.time;
            JToken data = For<AudioSource>().Capture(source);

            source.Stop();
            For<AudioSource>().Restore(source, data);

            Assert.IsFalse(source.isPlaying);
            Assert.AreEqual(time, source.time, 0.1f);
        }
#endif
    }
}
