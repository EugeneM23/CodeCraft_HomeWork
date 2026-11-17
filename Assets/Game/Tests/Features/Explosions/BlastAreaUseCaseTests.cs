using System.Collections.Generic;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class BlastAreaUseCaseTests
    {
        // Far from anything a scene open in the editor might hold.
        private static readonly Vector3 Origin = new(-3000f, 800f, 3000f);
        private const float Radius = 4f;

        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _objects)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);

            _objects.Clear();
        }

        private MonoEntity Damageable(string name, Vector3 offset, bool damageable = true)
        {
            MonoEntity entity = MonoEntity.Create(new MonoEntity.CreateArgs { name = name });
            _objects.Add(entity.gameObject);
            entity.transform.position = Origin + offset;
            entity.gameObject.AddComponent<BoxCollider>();
            if (damageable)
                entity.AddDamageableTag();
            return entity;
        }

        private BoxCollider ChildCollider(Component parent, string name, Vector3 localPosition)
        {
            GameObject child = new(name);
            _objects.Add(child);
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = localPosition;
            return child.AddComponent<BoxCollider>();
        }

        private static Dictionary<IEntity, float> Visit(System.Func<IEntity, Collider, bool> accept = null)
        {
            Physics.SyncTransforms();
            var visits = new Dictionary<IEntity, float>();
            BlastAreaUseCase.ForEachDamageable(Origin, Radius, accept, (target, share) =>
            {
                Assert.That(visits.ContainsKey(target), Is.False, $"{target.Name} visited twice");
                visits[target] = share;
            });
            return visits;
        }

        [Test]
        public void EntityWithSeveralColliders_IsVisitedOnce()
        {
            MonoEntity target = Damageable("Target", new Vector3(2f, 0f, 0f));
            ChildCollider(target, "Arm", new Vector3(0f, 0.5f, 0f));

            Dictionary<IEntity, float> visits = Visit();

            Assert.That(visits.Keys, Is.EquivalentTo(new IEntity[] { target }));
        }

        [Test]
        public void ShareOfTheRadius_IsMeasuredToTheNearestPointOfTheCollider()
        {
            MonoEntity atCentre = Damageable("AtCentre", Vector3.zero);
            MonoEntity halfway = Damageable("Halfway", new Vector3(2.5f, 0f, 0f)); // near face at 2 m

            Dictionary<IEntity, float> visits = Visit();

            Assert.That(visits[atCentre], Is.EqualTo(0f).Within(1e-4f));
            Assert.That(visits[halfway], Is.EqualTo(0.5f).Within(1e-3f));
        }

        [Test]
        public void NotDamageable_OrOutOfRange_IsNotVisited()
        {
            Damageable("Crate", new Vector3(1f, 0f, 0f), damageable: false);
            Damageable("FarAway", new Vector3(10f, 0f, 0f));

            Assert.That(Visit(), Is.Empty);
        }

        [Test]
        public void OneColliderTurnedAway_AnotherOfTheSameEntityStillLetsItThrough()
        {
            MonoEntity target = Damageable("Target", new Vector3(2f, 0f, 0f));
            BoxCollider head = ChildCollider(target, "Head", new Vector3(0f, 0.5f, 0f));

            Dictionary<IEntity, float> visits = Visit((_, hit) => hit == head);

            Assert.That(visits.Keys, Is.EquivalentTo(new IEntity[] { target }));
        }

        [Test]
        public void EveryColliderTurnedAway_EntityIsNotVisited()
        {
            Damageable("Target", new Vector3(2f, 0f, 0f));

            Assert.That(Visit((_, _) => false), Is.Empty);
        }

        [Test]
        public void FindEntity_ColliderBelowTheRoot_BelongsToTheRoot()
        {
            MonoEntity root = Damageable("Root", Vector3.zero);
            BoxCollider limb = ChildCollider(root, "Limb", Vector3.up);

            Assert.That(BlastAreaUseCase.FindEntity(limb), Is.SameAs(root));
        }

        [Test]
        public void FindEntity_BodysEntityWinsOverOneOnTheColliderItself()
        {
            MonoEntity vehicle = Damageable("Vehicle", Vector3.zero);
            Rigidbody body = vehicle.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            MonoEntity part = MonoEntity.Create(new MonoEntity.CreateArgs { name = "Part" });
            _objects.Add(part.gameObject);
            part.transform.SetParent(vehicle.transform, false);
            BoxCollider partCollider = part.gameObject.AddComponent<BoxCollider>();

            Assert.That(BlastAreaUseCase.FindEntity(partCollider), Is.SameAs(vehicle));
        }

        [Test]
        public void FindEntity_ColliderOfNoEntity_IsNull()
        {
            GameObject wall = new("Wall");
            _objects.Add(wall);

            Assert.That(BlastAreaUseCase.FindEntity(wall.AddComponent<BoxCollider>()), Is.Null);
        }
    }
}
