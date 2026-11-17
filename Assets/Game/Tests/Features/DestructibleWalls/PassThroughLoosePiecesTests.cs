using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class PassThroughLoosePiecesTests
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

        private GameObject Track(GameObject gameObject)
        {
            _created.Add(gameObject);
            return gameObject;
        }

        private Collider Piece(bool hasBody, bool kinematic)
        {
            GameObject piece = Track(new GameObject("Piece"));
            Collider collider = piece.AddComponent<BoxCollider>();
            if (hasBody)
                piece.AddComponent<Rigidbody>().isKinematic = kinematic;
            return collider;
        }

        [Test]
        public void KnockedOutPiece_IsLoose()
        {
            Assert.That(PassThroughLoosePiecesBehaviour.IsLoose(Piece(hasBody: true, kinematic: false)), Is.True);
        }

        [Test]
        public void StandingPiece_IsNotLoose()
        {
            Assert.That(PassThroughLoosePiecesBehaviour.IsLoose(Piece(hasBody: true, kinematic: true)), Is.False);
        }

        [Test]
        public void StaticGeometry_IsNotLoose()
        {
            Assert.That(PassThroughLoosePiecesBehaviour.IsLoose(Piece(hasBody: false, kinematic: false)), Is.False);
        }

        [Test]
        public void PieceOfAFlyingChunk_IsLoose()
        {
            // A clusterized chunk: the pieces keep their colliders under a root that carries the body.
            GameObject chunk = Track(new GameObject("Chunk"));
            chunk.AddComponent<Rigidbody>();
            GameObject piece = Track(new GameObject("Piece"));
            piece.transform.SetParent(chunk.transform);
            Collider collider = piece.AddComponent<BoxCollider>();

            Assert.That(PassThroughLoosePiecesBehaviour.IsLoose(collider), Is.True);
        }

        [Test]
        public void NoCollider_IsNotLoose()
        {
            Assert.That(PassThroughLoosePiecesBehaviour.IsLoose(null), Is.False);
        }
    }
}
