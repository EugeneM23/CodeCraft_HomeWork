using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class FindTargetUseCaseTests
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

        private IEntity Weapon(float radius, LayerMask layers)
        {
            var gameObject = new GameObject("Blade");
            _created.Add(gameObject);
            IEntity weapon = new Entity();
            weapon.AddTransform(gameObject.transform);
            weapon.AddDamageRadius(new Const<float>(radius));
            weapon.AddDamageLayer(layers);
            return weapon;
        }

        /// <summary>A body with a collider and an entity on it, the way FindTarget finds characters.</summary>
        private MonoEntity Body(Vector3 position, bool damageable, int layer = 0)
        {
            var gameObject = new GameObject("Body");
            _created.Add(gameObject);
            gameObject.layer = layer;
            gameObject.transform.position = position;
            gameObject.AddComponent<SphereCollider>().radius = 0.3f;
            var entity = gameObject.AddComponent<MonoEntity>();
            ((ISerializationCallbackReceiver)entity).OnAfterDeserialize();
            if (damageable)
                entity.AddDamageableTag();
            Physics.SyncTransforms();
            return entity;
        }

        [Test]
        public void DamageableBodyInReach_IsFound()
        {
            MonoEntity body = Body(new Vector3(0.8f, 0f, 0f), damageable: true);

            bool found = FindTargetUseCase.TryGetTarget(Weapon(1f, ~0), out IEntity target);

            Assert.That(found, Is.True);
            Assert.That(target, Is.SameAs(body));
        }

        [Test]
        public void BodyOutOfReach_IsNotFound()
        {
            Body(new Vector3(3f, 0f, 0f), damageable: true);

            Assert.That(FindTargetUseCase.TryGetTarget(Weapon(1f, ~0), out IEntity target), Is.False);
            Assert.That(target, Is.Null);
        }

        [Test]
        public void EntityThatCannotBeDamaged_IsNotFound()
        {
            Body(new Vector3(0.5f, 0f, 0f), damageable: false);

            Assert.That(FindTargetUseCase.TryGetTarget(Weapon(1f, ~0), out _), Is.False);
        }

        [Test]
        public void BodyOnALayerOutsideTheDamageLayer_IsNotFound()
        {
            Body(new Vector3(0.5f, 0f, 0f), damageable: true, layer: PhysicsLayers.Enemy);

            LayerMask everythingButEnemies = ~(1 << PhysicsLayers.Enemy);
            Assert.That(FindTargetUseCase.TryGetTarget(Weapon(1f, everythingButEnemies), out _), Is.False);
        }

        [Test]
        public void ManyConnectedSwings_KeepFindingTheTarget()
        {
            // The hit path used to skip returning the pooled buffer; it must stay usable swing after swing.
            MonoEntity body = Body(new Vector3(0.5f, 0f, 0f), damageable: true);
            IEntity weapon = Weapon(1f, ~0);

            for (int i = 0; i < 100; i++)
            {
                Assert.That(FindTargetUseCase.TryGetTarget(weapon, out IEntity target), Is.True);
                Assert.That(target, Is.SameAs(body));
            }
        }
    }
}
