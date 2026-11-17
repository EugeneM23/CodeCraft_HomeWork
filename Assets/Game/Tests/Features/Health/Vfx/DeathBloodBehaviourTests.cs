using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public sealed class DeathBloodBehaviourTests
    {
        private static readonly Vector3 AnchorPosition = new(-4000f, 0f, 4000f);

        private readonly List<GameObject> _objects = new();
        private DeathBloodConfig _config;
        private Transform _anchor;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<DeathBloodConfig>();
            _config.height = 0.5f;
            _config.scale = 2f;
            _anchor = NewObject("Anchor").transform;
            _anchor.position = AnchorPosition;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _objects)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);

            _objects.Clear();
            Object.DestroyImmediate(_config);
        }

        private GameObject NewObject(string name)
        {
            var gameObject = new GameObject(name);
            _objects.Add(gameObject);
            return gameObject;
        }

        /// <summary>The spawned copy of <paramref name="variant"/>, tracked for teardown.</summary>
        private GameObject SpawnedFrom(GameObject variant)
        {
            GameObject spawned = GameObject.Find(variant.name + "(Clone)");
            if (spawned != null)
                _objects.Add(spawned);
            return spawned;
        }

        private IEntity Character(int variant)
        {
            IEntity character = new Entity();
            character.AddDeathTakenEvent(new Event<TakeDamageArgs>());
            character.AddBehaviour(new DeathBloodBehaviour(_config, _anchor, variant));
            character.Init();
            return character;
        }

        private static void Die(IEntity character) =>
            character.GetDeathTakenEvent().Invoke(new TakeDamageArgs(character));

        [Test]
        public void FixedVariant_WrapsAroundTheList()
        {
            // The spawned effect is scheduled for removal with Object.Destroy, which edit mode
            // reports as an error; the spawn itself is what is under test.
            LogAssert.ignoreFailingMessages = true;
            GameObject first = NewObject("DeathBloodTestA");
            GameObject second = NewObject("DeathBloodTestB");
            _config.variants = new[] { first, second };

            Die(Character(variant: 3));

            Assert.That(SpawnedFrom(second), Is.Not.Null);
            Assert.That(SpawnedFrom(first), Is.Null);
        }

        [Test]
        public void Death_ThrowsTheBloodAboveTheAnchor_ScaledAndLevel()
        {
            LogAssert.ignoreFailingMessages = true;
            GameObject variant = NewObject("DeathBloodTestC");
            _config.variants = new[] { variant };

            Die(Character(variant: 0));

            GameObject blood = SpawnedFrom(variant);
            Assert.That(blood, Is.Not.Null);
            Assert.That(Vector3.Distance(blood.transform.position, AnchorPosition + Vector3.up * 0.5f), Is.LessThan(1e-3f));
            Assert.That(blood.transform.localScale, Is.EqualTo(Vector3.one * 2f));
            Assert.That(Vector3.Angle(blood.transform.up, Vector3.up), Is.LessThan(0.01f));
        }

        [Test]
        public void NoVariants_NothingIsSpawned()
        {
            _config.variants = new GameObject[0];

            Assert.DoesNotThrow(() => Die(Character(variant: -1)));
        }

        [Test]
        public void EmptySlot_NothingIsSpawned()
        {
            _config.variants = new GameObject[] { null };

            Assert.DoesNotThrow(() => Die(Character(variant: 0)));
        }
    }
}
