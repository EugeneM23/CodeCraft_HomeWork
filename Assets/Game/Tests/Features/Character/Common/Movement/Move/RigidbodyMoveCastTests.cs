using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// The capsule cast the character slides along walls with. It has to see what the capsule
    /// actually collides with: a piece the character passes through (collisions switched off for
    /// the pair) is no wall, or the character would stop in front of rubble it walks through.
    /// </summary>
    public sealed class RigidbodyMoveCastTests
    {
        private readonly List<GameObject> _created = new();
        private CapsuleCollider _capsule;

        [SetUp]
        public void SetUp()
        {
            GameObject character = Track(new GameObject("Character"));
            _capsule = character.AddComponent<CapsuleCollider>();
            _capsule.radius = 0.38f;
            _capsule.height = 2.16f;
            _capsule.center = new Vector3(0f, 1.08f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
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

        private Collider Box(string name, float z, bool loose)
        {
            GameObject box = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            box.name = name;
            box.transform.position = new Vector3(0f, 0.5f, z);
            if (loose)
                box.AddComponent<Rigidbody>();
            return box.GetComponent<Collider>();
        }

        private bool Cast(out RaycastHit hit)
        {
            Physics.SyncTransforms();
            return RigidbodyMoveBehaviour.CastBlocking(_capsule, Vector3.forward, 3f, ~0, out hit);
        }

        [Test]
        public void Wall_Blocks()
        {
            Collider wall = Box("Wall", 2f, loose: false);

            Assert.That(Cast(out RaycastHit hit), Is.True);
            Assert.That(hit.collider, Is.SameAs(wall));
        }

        [Test]
        public void PieceItPassesThrough_DoesNotBlock()
        {
            Collider piece = Box("Piece", 1.5f, loose: true);
            Physics.IgnoreCollision(_capsule, piece, true);

            Assert.That(Cast(out _), Is.False);
        }

        [Test]
        public void PieceItPassesThrough_WallBehindStillBlocks()
        {
            Collider piece = Box("Piece", 1.2f, loose: true);
            Collider wall = Box("Wall", 2.6f, loose: false);
            Physics.IgnoreCollision(_capsule, piece, true);

            Assert.That(Cast(out RaycastHit hit), Is.True);
            Assert.That(hit.collider, Is.SameAs(wall));
        }

        [Test]
        public void PieceItStillCollidesWith_Blocks()
        {
            Collider piece = Box("Piece", 1.5f, loose: true);

            Assert.That(Cast(out RaycastHit hit), Is.True);
            Assert.That(hit.collider, Is.SameAs(piece));
        }

        [Test]
        public void SomethingAlreadyOverlapping_IsNotReported_AsWithASingleCast()
        {
            // A single CapsuleCast does not report what the capsule overlaps at the start of the
            // sweep; the all-hits version reports it at distance 0 and must leave it out the same way.
            Box("Overlapping", 0.3f, loose: false);

            Assert.That(Cast(out _), Is.False);
        }
    }
}
