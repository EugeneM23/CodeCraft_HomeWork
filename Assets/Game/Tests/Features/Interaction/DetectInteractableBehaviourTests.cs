using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class DetectInteractableBehaviourTests
    {
        private static readonly Vector3 Here = new(-7000f, 7000f, -7000f);

        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _objects)
                Object.DestroyImmediate(gameObject);

            _objects.Clear();
        }

        private MonoEntity Item(Vector3 position)
        {
            MonoEntity item = MonoEntity.Create<MonoEntity>(name: "Item");
            _objects.Add(item.gameObject);
            item.transform.position = position;
            item.gameObject.AddComponent<SphereCollider>().radius = 0.25f;
            item.AddTransform(item.transform);
            item.AddInteractableTag();
            return item;
        }

        private IEntity Character()
        {
            var center = new GameObject("Center");
            center.transform.position = Here;
            _objects.Add(center);

            IEntity character = new Entity();
            character.AddTargetInteractable(new ReactiveVariable<IEntity>());
            character.AddBehaviour(new DetectInteractableBehaviour(
                center.transform, ~0, 2f, QueryTriggerInteraction.Collide));
            character.Enable();
            return character;
        }

        [Test]
        public void ItemInReach_BecomesTheTarget()
        {
            MonoEntity item = Item(Here + Vector3.forward);
            IEntity character = Character();

            Physics.SyncTransforms();
            character.FixedTick(0.02f);

            Assert.That(character.GetTargetInteractable().Value, Is.SameAs(item));
        }

        [Test]
        public void ItemMovesOutOfReach_TargetIsCleared()
        {
            MonoEntity item = Item(Here + Vector3.forward);
            IEntity character = Character();
            Physics.SyncTransforms();
            character.FixedTick(0.02f);

            item.transform.position = Here + Vector3.forward * 20f;
            Physics.SyncTransforms();
            character.FixedTick(0.02f);

            Assert.That(character.GetTargetInteractable().Value, Is.Null);
        }
    }
}
