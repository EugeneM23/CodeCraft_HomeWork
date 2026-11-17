using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class InteractUseCaseTests
    {
        // Far from anything a scene might hold, so the overlap sees only what the test made.
        private static readonly Vector3 Here = new(7000f, 7000f, 7000f);

        private readonly List<GameObject> _objects = new();
        private Transform _origin;

        [SetUp]
        public void SetUp()
        {
            var origin = new GameObject("Origin");
            origin.transform.position = Here;
            _objects.Add(origin);
            _origin = origin.transform;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _objects)
                Object.DestroyImmediate(gameObject);

            _objects.Clear();
        }

        private MonoEntity Item(Vector3 offset, bool interactable = true)
        {
            MonoEntity item = MonoEntity.Create<MonoEntity>(name: "Item");
            _objects.Add(item.gameObject);
            item.transform.position = Here + offset;
            item.gameObject.AddComponent<SphereCollider>().radius = 0.25f;
            item.AddTransform(item.transform);
            if (interactable)
                item.AddInteractableTag();
            return item;
        }

        private bool FindClosest(out IEntity target)
        {
            Physics.SyncTransforms();
            return InteractUseCase.FindClosestItem(_origin, 3f, ~0, QueryTriggerInteraction.Collide, out target);
        }

        private static IEntity Character(IEntity target)
        {
            IEntity character = new Entity();
            character.AddTargetInteractable(new ReactiveVariable<IEntity>(target));
            return character;
        }

        private static IEntity Interactable(List<IEntity> users, bool tagged = true)
        {
            IEntity target = new Entity();
            if (tagged)
                target.AddInteractableTag();
            target.AddInteractAction(new InlineAction<IEntity>(users.Add));
            return target;
        }

        [Test]
        public void InteractWithCharacter_UsesTheTargetedItem_OnBehalfOfTheCharacter()
        {
            var users = new List<IEntity>();
            IEntity character = Character(Interactable(users));

            InteractUseCase.InteractWithCharacter(character);

            Assert.That(users, Is.EqualTo(new[] { character }));
        }

        [Test]
        public void InteractWithCharacter_NothingTargeted_DoesNothing()
        {
            Assert.DoesNotThrow(() => InteractUseCase.InteractWithCharacter(Character(null)));
        }

        [Test]
        public void InteractWithCharacter_TargetNoLongerInteractable_IsLeftAlone()
        {
            var users = new List<IEntity>();
            IEntity character = Character(Interactable(users, tagged: false));

            InteractUseCase.InteractWithCharacter(character);

            Assert.That(users, Is.Empty);
        }

        [Test]
        public void FindClosestItem_PicksTheNearestInteractable()
        {
            Item(new Vector3(2f, 0f, 0f));
            MonoEntity near = Item(new Vector3(0f, 0f, 1f));

            bool found = FindClosest(out IEntity target);

            Assert.That(found, Is.True);
            Assert.That(target, Is.SameAs(near));
        }

        [Test]
        public void FindClosestItem_SkipsWhatCannotBeInteractedWith()
        {
            Item(new Vector3(0.5f, 0f, 0f), interactable: false);
            MonoEntity far = Item(new Vector3(0f, 0f, 2f));

            FindClosest(out IEntity target);

            Assert.That(target, Is.SameAs(far));
        }

        [Test]
        public void FindClosestItem_NothingInReach_ReportsNothing()
        {
            Item(new Vector3(10f, 0f, 0f));

            bool found = FindClosest(out IEntity target);

            Assert.That(found, Is.False);
            Assert.That(target, Is.Null);
        }
    }
}
