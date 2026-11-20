using System.Collections.Generic;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AIKit.Entities.Tests
{
    /// <summary>Candidates for the senses from a collection of entities: within the radius and passing the predicate.</summary>
    public sealed class EntityTargetScannerTests
    {
        private readonly List<GameObject> _made = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
                Object.DestroyImmediate(go);
            _made.Clear();
        }

        private MonoEntity Make(string name, Vector3 at, bool tagged)
        {
            MonoEntity entity = MonoEntity.Create(new MonoEntity.CreateArgs { name = name });
            entity.transform.position = at;
            if (tagged)
                entity.AddAIAgentTag();
            _made.Add(entity.gameObject);
            return entity;
        }

        [Test]
        public void Scan_TheOnesWithinTheRadius_ThatPassThePredicate()
        {
            var entities = new EntityCollection<IEntity>();
            MonoEntity near = Make("Near", new Vector3(3f, 0f, 0f), true);
            MonoEntity untagged = Make("Untagged", new Vector3(0f, 0f, 2f), false);
            MonoEntity far = Make("Far", new Vector3(30f, 0f, 0f), true);
            entities.Add(near);
            entities.Add(untagged);
            entities.Add(far);
            entities.Add(new Entity("No place"));
            var scanner = new EntityTargetScanner(entities, static e => e.HasAIAgentTag());
            var results = new List<Transform> { null };

            int count = scanner.Scan(Vector3.zero, 10f, results);

            Assert.AreEqual(1, count);
            CollectionAssert.AreEqual(new[] { near.transform }, results);
        }

        [Test]
        public void Scan_NoPredicate_EveryEntityInReach()
        {
            var entities = new EntityCollection<IEntity>();
            entities.Add(Make("A", new Vector3(1f, 0f, 0f), false));
            entities.Add(Make("B", new Vector3(0f, 0f, -1f), true));

            int count = new EntityTargetScanner(entities).Scan(Vector3.zero, 2f, new List<Transform>());

            Assert.AreEqual(2, count);
        }
    }
}
