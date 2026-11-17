using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class CameraFollowUseCaseTests
    {
        private static readonly Vector3 TargetPosition = new(10f, 0f, 0f);
        private static readonly Vector3 Offset = new(0f, 10f, -6f);

        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _objects)
                Object.DestroyImmediate(gameObject);

            _objects.Clear();
        }

        private Transform NewTransform(Vector3 position)
        {
            GameObject gameObject = new("Follow");
            gameObject.transform.position = position;
            _objects.Add(gameObject);
            return gameObject.transform;
        }

        [Test]
        public void LongFrame_ArrivesAtTheTargetPlusOffset()
        {
            Transform source = NewTransform(Vector3.zero);

            CameraFollowUseCase.Follow(source, NewTransform(TargetPosition), 100f, 5, Offset);

            Assert.That(Vector3.Distance(source.position, TargetPosition + Offset), Is.LessThan(1e-4f));
        }

        [Test]
        public void ZeroDeltaTime_StaysPut()
        {
            Transform source = NewTransform(Vector3.zero);

            CameraFollowUseCase.Follow(source, NewTransform(TargetPosition), 0f, 5, Offset);

            Assert.That(source.position, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void OneFrame_ClosesOneMinusExpOfTheGap()
        {
            Transform source = NewTransform(Vector3.zero);

            // speed * deltaTime = 1: the share of the gap closed is 1 - e^-1.
            CameraFollowUseCase.Follow(source, NewTransform(TargetPosition), 0.2f, 5);

            Assert.That(source.position.x, Is.EqualTo(10f * (1f - Mathf.Exp(-1f))).Within(1e-4f));
        }

        [Test]
        public void TwoShortFrames_EndWhereOneLongFrameDoes()
        {
            Transform target = NewTransform(TargetPosition);
            Transform once = NewTransform(Vector3.zero);
            Transform twice = NewTransform(Vector3.zero);

            CameraFollowUseCase.Follow(once, target, 0.1f, 5, Offset);
            CameraFollowUseCase.Follow(twice, target, 0.05f, 5, Offset);
            CameraFollowUseCase.Follow(twice, target, 0.05f, 5, Offset);

            Assert.That(Vector3.Distance(once.position, twice.position), Is.LessThan(1e-4f));
        }
    }
}
