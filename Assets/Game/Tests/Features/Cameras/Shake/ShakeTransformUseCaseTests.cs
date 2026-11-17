using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class ShakeTransformUseCaseTests
    {
        private static readonly Vector3 Rest = new(1f, 2f, 3f);

        private GameObject _camera;

        [SetUp]
        public void SetUp()
        {
            _camera = new GameObject("Camera");
            Random.InitState(12345);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_camera);

        private Vector3 Offset => _camera.transform.localPosition - Rest;

        [Test]
        public void FreshShake_StaysWithinStrength_OnTheScreenPlane()
        {
            for (int i = 0; i < 50; i++)
            {
                ShakeTransformUseCase.ShakePosition(_camera.transform, Rest, 0.5f, 1f);

                Assert.That(Mathf.Abs(Offset.x), Is.LessThanOrEqualTo(0.5f + 1e-5f));
                Assert.That(Mathf.Abs(Offset.y), Is.LessThanOrEqualTo(0.5f + 1e-5f));
                Assert.That(Offset.z, Is.EqualTo(0f).Within(1e-5f));
            }
        }

        [Test]
        public void NearlyOverShake_IsDampedToItsRemainingShare()
        {
            for (int i = 0; i < 50; i++)
            {
                ShakeTransformUseCase.ShakePosition(_camera.transform, Rest, 2f, 0.1f);

                Assert.That(Mathf.Abs(Offset.x), Is.LessThanOrEqualTo(0.2f + 1e-5f));
                Assert.That(Mathf.Abs(Offset.y), Is.LessThanOrEqualTo(0.2f + 1e-5f));
            }
        }

        [Test]
        public void NoTimeLeft_IsExactlyTheRestPosition()
        {
            ShakeTransformUseCase.ShakePosition(_camera.transform, Rest, 2f, 0f);

            Assert.That(Vector3.Distance(_camera.transform.localPosition, Rest), Is.LessThan(1e-6f));
        }
    }
}
