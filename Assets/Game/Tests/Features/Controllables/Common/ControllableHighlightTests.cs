using System.Collections.Generic;
using HighlightPlus;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class ControllableHighlightTests
    {
        private static readonly Vector3 Origin = new(-5000f, 300f, 5000f);

        private readonly List<GameObject> _objects = new();

        private GameObject _vehicle;
        private HighlightEffect _highlight;

        [SetUp]
        public void SetUp()
        {
            _vehicle = Track(new GameObject("Vehicle"));
            _vehicle.transform.position = Origin;
            _highlight = _vehicle.AddComponent<HighlightEffect>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _objects)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);

            _objects.Clear();
        }

        private GameObject Track(GameObject gameObject)
        {
            _objects.Add(gameObject);
            return gameObject;
        }

        private void AddPart(Transform parent, Vector3 localPosition)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
        }

        [Test]
        public void ManyParts_OneMarkerAtTheCentreOfTheWholeVehicle()
        {
            AddPart(_vehicle.transform, new Vector3(-1f, 0f, 0f));
            AddPart(_vehicle.transform, new Vector3(5f, 2f, 0f));

            ControllableHighlight.UseSingleTargetFX(_highlight);

            Transform center = _highlight.targetFXCenter;
            Assert.That(center, Is.Not.Null);
            Assert.That(center.parent, Is.SameAs(_vehicle.transform));
            Assert.That(Vector3.Distance(center.position, Origin + new Vector3(2f, 1f, 0f)), Is.LessThan(1e-4f));
            Assert.That(_highlight.targetFXUseEnclosingBounds, Is.True);
        }

        [Test]
        public void NoMeshes_MarkerSitsOnTheVehicle()
        {
            ControllableHighlight.UseSingleTargetFX(_highlight);

            Assert.That(Vector3.Distance(_highlight.targetFXCenter.position, Origin), Is.LessThan(1e-4f));
        }

        [Test]
        public void EffectTargetSet_MeasuresThatInstead()
        {
            GameObject target = Track(new GameObject("Model"));
            target.transform.position = Origin + new Vector3(0f, 0f, 10f);
            AddPart(target.transform, new Vector3(0f, 3f, 0f));
            _highlight.effectTarget = target.transform;

            ControllableHighlight.UseSingleTargetFX(_highlight);

            Assert.That(_highlight.targetFXCenter.parent, Is.SameAs(target.transform));
            Assert.That(Vector3.Distance(_highlight.targetFXCenter.position, target.transform.position + Vector3.up * 3f),
                Is.LessThan(1e-4f));
        }

        [Test]
        public void CalledTwice_KeepsTheFirstCentre()
        {
            AddPart(_vehicle.transform, Vector3.zero);
            ControllableHighlight.UseSingleTargetFX(_highlight);
            Transform first = _highlight.targetFXCenter;

            ControllableHighlight.UseSingleTargetFX(_highlight);

            Assert.That(_highlight.targetFXCenter, Is.SameAs(first));
            Assert.That(_vehicle.transform.childCount, Is.EqualTo(2), "one part, one centre");
        }

        [Test]
        public void NoHighlight_IsIgnored()
        {
            Assert.DoesNotThrow(() => ControllableHighlight.UseSingleTargetFX(null));
        }
    }
}
