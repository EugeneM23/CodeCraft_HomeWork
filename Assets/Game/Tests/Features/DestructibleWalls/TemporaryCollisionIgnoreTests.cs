using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class TemporaryCollisionIgnoreTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private readonly List<GameObject> _created = new();

        [SetUp]
        public void SetUp() => DestroyInstance();

        [TearDown]
        public void TearDown()
        {
            DestroyInstance();
            foreach (GameObject gameObject in _created)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);
            _created.Clear();
        }

        [Test]
        public void Ignore_LetsThePairPassThroughEachOther()
        {
            Collider a = Box(), b = Box();

            TemporaryCollisionIgnore.Ignore(a, b, 1f);

            Assert.That(Physics.GetIgnoreCollision(a, b), Is.True);
        }

        [Test]
        public void PairWhoseTimeIsUp_CollidesAgain()
        {
            Collider a = Box(), b = Box();
            TemporaryCollisionIgnore.Ignore(a, b, -1f);

            Tick();

            Assert.That(Physics.GetIgnoreCollision(a, b), Is.False);
            Assert.That(Pairs().Count, Is.Zero);
        }

        [Test]
        public void PairStillWithinItsTime_KeepsPassingThrough()
        {
            Collider a = Box(), b = Box();
            TemporaryCollisionIgnore.Ignore(a, b, 1000f);

            Tick();

            Assert.That(Physics.GetIgnoreCollision(a, b), Is.True);
            Assert.That(Pairs().Count, Is.EqualTo(1));
        }

        [Test]
        public void OnlyThePairWhoseTimeIsUp_IsRestored()
        {
            Collider a = Box(), b = Box(), c = Box();
            TemporaryCollisionIgnore.Ignore(a, b, -1f);
            TemporaryCollisionIgnore.Ignore(a, c, 1000f);

            Tick();

            Assert.That(Physics.GetIgnoreCollision(a, b), Is.False);
            Assert.That(Physics.GetIgnoreCollision(a, c), Is.True);
        }

        [Test]
        public void PairWithADestroyedCollider_IsDroppedQuietly()
        {
            Collider a = Box(), b = Box();
            TemporaryCollisionIgnore.Ignore(a, b, -1f);
            Object.DestroyImmediate(b.gameObject);

            Assert.DoesNotThrow(Tick);
            Assert.That(Pairs().Count, Is.Zero);
        }

        [Test]
        public void ColliderWithItself_IsNotTracked()
        {
            Collider a = Box();

            TemporaryCollisionIgnore.Ignore(a, a, 1f);
            TemporaryCollisionIgnore.Ignore(a, null, 1f);

            Assert.That(Instance(), Is.Null);
        }

        private Collider Box()
        {
            var gameObject = new GameObject("Brick");
            _created.Add(gameObject);
            gameObject.transform.position = new Vector3(5000f, 5000f, 5000f);
            return gameObject.AddComponent<BoxCollider>();
        }

        private static FieldInfo InstanceField =>
            typeof(TemporaryCollisionIgnore).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);

        private static TemporaryCollisionIgnore Instance()
        {
            var instance = (TemporaryCollisionIgnore)InstanceField.GetValue(null);
            return instance != null ? instance : null;
        }

        private static void Tick() =>
            typeof(TemporaryCollisionIgnore).GetMethod("Update", Private).Invoke(Instance(), null);

        private static IList Pairs() =>
            (IList)typeof(TemporaryCollisionIgnore).GetField("_pairs", Private).GetValue(Instance());

        // In edit mode OnDestroy does not run, so the static is cleared by hand.
        private static void DestroyInstance()
        {
            TemporaryCollisionIgnore instance = Instance();
            if (instance != null)
                Object.DestroyImmediate(instance.gameObject);
            InstanceField.SetValue(null, null);
        }
    }
}
