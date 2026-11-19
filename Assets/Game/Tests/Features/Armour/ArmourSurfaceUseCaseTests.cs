using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class ArmourSurfaceUseCaseTests
    {
        private GameObject _plate;

        [TearDown]
        public void TearDown()
        {
            if (_plate != null)
                Object.DestroyImmediate(_plate);
        }

        [Test]
        public void SmoothNormal_OfATiltedPlate_IsThePlatesNormal()
        {
            // A 2 m plate leaning back 35°, far from anything else.
            _plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _plate.transform.position = new Vector3(500f, 500f, 500f);
            _plate.transform.rotation = Quaternion.Euler(-35f, 0f, 0f);
            _plate.transform.localScale = new Vector3(2f, 2f, 0.1f);
            Physics.SyncTransforms();

            Vector3 dir = Quaternion.Euler(0f, 20f, 0f) * Vector3.forward;
            Assert.That(Physics.Raycast(_plate.transform.position - dir * 5f, dir, out RaycastHit hit, 10f), Is.True);
            Vector3 normal = ArmourSurfaceUseCase.SmoothNormal(hit, dir);
            Vector3 expected = _plate.transform.rotation * Vector3.back;
            Assert.That(Vector3.Angle(normal, expected), Is.LessThan(1f));
        }
    }
}
