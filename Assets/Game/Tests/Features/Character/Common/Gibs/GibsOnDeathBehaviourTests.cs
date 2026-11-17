using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class GibsOnDeathBehaviourTests
    {
        private static readonly Vector3 BodyPosition = new(2f, 0f, 0f);

        private readonly List<GameObject> _created = new();
        private Gibs _prefab;
        private Transform _skeleton;
        private Renderer _skin;
        private IEntity _character;

        [SetUp]
        public void SetUp()
        {
            _prefab = GibsPrefab();
            _skeleton = Skeleton();
            _skin = Track(new GameObject("Skin")).AddComponent<MeshRenderer>();

            _character = new Entity();
            _character.AddDamageTakenEvent(new Event<TakeDamageArgs>());
            _character.AddDeathTakenEvent(new Event<TakeDamageArgs>());
            new GibsOnDeathBehaviour(_prefab, _skeleton, new Renderer[] { _skin }).Init(_character);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Gibs gibs in Object.FindObjectsByType<Gibs>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Track(gibs.gameObject);

            foreach (GameObject gameObject in _created)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);
            _created.Clear();
        }

        private GameObject Track(GameObject gameObject)
        {
            _created.Add(gameObject);
            return gameObject;
        }

        /// <summary>A gibs prefab cut into a single piece: one bone with one body.</summary>
        private Gibs GibsPrefab()
        {
            GameObject root = Track(new GameObject("Gibs"));
            var pelvis = new GameObject("Pelvis");
            pelvis.transform.SetParent(root.transform, false);
            pelvis.AddComponent<BoxCollider>();
            var body = pelvis.AddComponent<Rigidbody>();

            var gibs = root.AddComponent<Gibs>();
            SetField(gibs, "_pieces", new[] { new Gibs.Piece { root = pelvis.transform, bodies = new[] { body } } });
            SetField(gibs, "_seams", new Gibs.Seam[0]);
            return gibs;
        }

        private Transform Skeleton()
        {
            Transform root = Track(new GameObject("Body")).transform;
            root.position = BodyPosition;
            var pelvis = new GameObject("Pelvis");
            pelvis.transform.SetParent(root, false);
            pelvis.transform.localPosition = new Vector3(0f, 1f, 0f);
            return root;
        }

        private static void SetField(Gibs gibs, string name, object value) =>
            typeof(Gibs).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(gibs, value);

        private Gibs Spawned() =>
            Object.FindObjectsByType<Gibs>(FindObjectsSortMode.None).Single(gibs => gibs != _prefab);

        private void KilledBy(Vector3 killerPosition)
        {
            IEntity killer = new Entity();
            killer.AddTransform(Track(new GameObject("Killer")).transform);
            killer.GetTransform().position = killerPosition;
            killer.AddDamage(new ReactiveInt(50));

            var hit = new TakeDamageArgs(killer);
            _character.GetDamageTakenEvent().Invoke(hit);
            _character.GetDeathTakenEvent().Invoke(new TakeDamageArgs(_character));
        }

        [Test]
        public void Death_HidesTheBody_AndPutsGibsWhereItStood()
        {
            KilledBy(BodyPosition + Vector3.left * 5f);

            Assert.That(_skin.enabled, Is.False);
            Assert.That(Spawned().transform.position, Is.EqualTo(BodyPosition));
        }

        [Test]
        public void Gibs_TakeTheBodysPose()
        {
            KilledBy(BodyPosition + Vector3.left * 5f);

            Transform pelvis = Spawned().Pieces[0].root;
            Assert.That(Vector3.Distance(pelvis.position, BodyPosition + Vector3.up), Is.LessThan(1e-4f));
        }

        [Test]
        public void KillerOnTheLeft_ThrowsTheGibsRight()
        {
            KilledBy(BodyPosition + Vector3.left * 5f);

            Vector3 velocity = Spawned().Pieces[0].bodies[0].linearVelocity;
            Assert.That(velocity.x, Is.GreaterThan(0f));
        }

        [Test]
        public void KillerOnTheRight_ThrowsTheGibsLeft()
        {
            KilledBy(BodyPosition + Vector3.right * 5f);

            Vector3 velocity = Spawned().Pieces[0].bodies[0].linearVelocity;
            Assert.That(velocity.x, Is.LessThan(0f));
        }

        [Test]
        public void DeathWithNoHitRecorded_StillBurstsTheBody()
        {
            _character.GetDeathTakenEvent().Invoke(new TakeDamageArgs(_character));

            Assert.That(_skin.enabled, Is.False);
            Assert.That(Spawned(), Is.Not.Null);
        }
    }
}
