using System.Collections.Generic;
using System.Reflection;
using Atomic.Entities;
using NUnit.Framework;
using RayFire;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// What a bullet takes for a destructible, and that a bullet without a RayFire gun leaves
    /// destruction alone. The shot itself (RayfireGun.Shoot) needs the RayFire runtime and a real
    /// collision, neither of which edit mode has.
    /// </summary>
    public sealed class DestructionUseCaseTests
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

        [Test]
        public void BulletWithoutARayFireGun_DoesNotImpact()
        {
            Assert.That(DestructionUseCase.TryImpact(new Entity(), null), Is.False);
        }

        [Test]
        public void BulletWhoseGunWasDestroyed_DoesNotImpact()
        {
            RayfireGun gun = Create("Gun").AddComponent<RayfireGun>();
            IEntity bullet = new Entity();
            bullet.AddDestructionImpact(gun);
            Object.DestroyImmediate(gun);

            Assert.That(DestructionUseCase.TryImpact(bullet, null), Is.False);
        }

        [Test]
        public void ColliderOfADestructiblePiece_IsDestructible()
        {
            GameObject piece = Create("Piece");
            piece.AddComponent<RayfireRigid>();

            Assert.That(IsDestructible(piece.AddComponent<BoxCollider>()), Is.True);
        }

        [Test]
        public void ColliderUnderTheBodyOfADestructibleFragment_IsDestructible()
        {
            // A fragment's RayfireRigid sits next to its Rigidbody, its colliders may hang below.
            GameObject fragment = Create("Fragment");
            fragment.AddComponent<Rigidbody>();
            fragment.AddComponent<RayfireRigid>();
            GameObject part = Create("Part");
            part.transform.SetParent(fragment.transform);

            Assert.That(IsDestructible(part.AddComponent<BoxCollider>()), Is.True);
        }

        [Test]
        public void ColliderUnderARigidRoot_IsDestructible()
        {
            GameObject root = Create("Root");
            root.AddComponent<RayfireRigidRoot>();
            GameObject shard = Create("Shard");
            shard.transform.SetParent(root.transform);

            Assert.That(IsDestructible(shard.AddComponent<BoxCollider>()), Is.True);
        }

        [Test]
        public void OrdinaryCollider_IsNotDestructible()
        {
            Assert.That(IsDestructible(Create("Crate").AddComponent<BoxCollider>()), Is.False);
        }

        [Test]
        public void ColliderOfAnOrdinaryBodyNextToADestructible_IsNotDestructible()
        {
            // The collider's own body decides, not a RayfireRigid that happens to sit beside the collider.
            GameObject crate = Create("Crate");
            crate.AddComponent<Rigidbody>();
            GameObject lid = Create("Lid");
            lid.transform.SetParent(crate.transform);
            lid.AddComponent<RayfireRigid>();

            Assert.That(IsDestructible(lid.AddComponent<BoxCollider>()), Is.False);
        }

        [Test]
        public void NoCollider_IsNotDestructible()
        {
            Assert.That(IsDestructible(null), Is.False);
        }

        private GameObject Create(string name)
        {
            var gameObject = new GameObject(name);
            _created.Add(gameObject);
            return gameObject;
        }

        private static bool IsDestructible(Collider collider) =>
            (bool)typeof(DestructionUseCase)
                .GetMethod("IsDestructible", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { collider });
    }
}
