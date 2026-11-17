using System.Reflection;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class CameraShakeBehaviourTests
    {
        private static readonly Vector3 Mount = new(0f, 1.5f, -0.25f);

        private GameObject _cameraObject;
        private IEntity _camera;
        private Event<CameraShakeArgs> _shake;
        private CameraShakeBehaviour _behaviour;

        [SetUp]
        public void SetUp()
        {
            Random.InitState(4242);

            _cameraObject = new GameObject("Camera");
            _cameraObject.transform.localPosition = Mount;

            _shake = new Event<CameraShakeArgs>();
            _camera = new Entity();
            _camera.AddTransform(_cameraObject.transform);
            _camera.AddCameraShakeEvent(_shake);

            _behaviour = new CameraShakeBehaviour();
            _camera.AddBehaviour(_behaviour);
            _camera.Enable();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_cameraObject);

        private static CameraShakeArgs Args(float duration, float strength)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var args = new CameraShakeArgs();
            typeof(CameraShakeArgs).GetField("<ShakeDuration>k__BackingField", flags).SetValue(args, duration);
            typeof(CameraShakeArgs).GetField("<ShakeStrength>k__BackingField", flags).SetValue(args, strength);
            return args;
        }

        private float DistanceFromMount => Vector3.Distance(_cameraObject.transform.localPosition, Mount);

        [Test]
        public void NoShake_CameraStaysOnItsMount()
        {
            _camera.Tick(0.1f);

            Assert.That(DistanceFromMount, Is.LessThan(1e-6f));
        }

        [Test]
        public void Shaking_CameraIsDisplacedWithinTheStrength()
        {
            _shake.Invoke(Args(1f, 0.5f));

            _camera.Tick(0.1f);

            Vector3 offset = _cameraObject.transform.localPosition - Mount;
            Assert.That(offset.magnitude, Is.GreaterThan(0f));
            // 0.9 of the shake is left: each axis moves at most 0.45.
            Assert.That(Mathf.Abs(offset.x), Is.LessThanOrEqualTo(0.45f + 1e-5f));
            Assert.That(Mathf.Abs(offset.y), Is.LessThanOrEqualTo(0.45f + 1e-5f));
            Assert.That(offset.z, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void ShakeRunsOut_CameraIsBackOnItsMount()
        {
            _shake.Invoke(Args(0.5f, 0.5f));

            _camera.Tick(0.2f);
            _camera.Tick(0.4f);

            Assert.That(DistanceFromMount, Is.LessThan(1e-6f));
        }

        [Test]
        public void ShakesStartedWhileDisplaced_DoNotWalkTheCameraOffItsMount()
        {
            for (int i = 0; i < 10; i++)
            {
                _shake.Invoke(Args(0.5f, 0.5f));
                _camera.Tick(0.05f);
            }

            _camera.Tick(1f);

            Assert.That(DistanceFromMount, Is.LessThan(1e-6f));
        }

        [Test]
        public void ZeroDurationShake_EndsOnTheNextFrame()
        {
            _shake.Invoke(Args(0f, 0.5f));

            _camera.Tick(0.016f);

            Assert.That(DistanceFromMount, Is.LessThan(1e-6f));
        }

        [Test]
        public void DisposedMidShake_CameraIsPutBack_AndLaterShakesAreIgnored()
        {
            _shake.Invoke(Args(1f, 0.5f));
            _camera.Tick(0.1f);

            _camera.Dispose();
            Vector3 afterDispose = _cameraObject.transform.localPosition;
            _shake.Invoke(Args(1f, 0.5f));
            _behaviour.Tick(_camera, 0.1f);

            Assert.That(Vector3.Distance(afterDispose, Mount), Is.LessThan(1e-6f));
            Assert.That(DistanceFromMount, Is.LessThan(1e-6f));
        }
    }
}
