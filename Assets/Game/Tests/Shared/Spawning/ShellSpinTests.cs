using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class ShellSpinTests
    {
        private static readonly Vector3 Up = Vector3.up;
        private static readonly Vector3 Right = Vector3.right;
        private static readonly Vector3 Along = Vector3.forward;

        [Test]
        public void SpinsAtTheAskedRate()
        {
            Vector3 spin = SpawnUseCase.EjectSpin(Up, Right, 15f, 0.8f, 0.3f);

            Assert.That(spin.magnitude, Is.EqualTo(15f).Within(1e-3f));
        }

        [Test]
        public void TumblesEndOverEnd_NotAroundItsOwnLength()
        {
            // The casing lies along the point's forward: a spin about that axis would not show.
            Vector3 spin = SpawnUseCase.EjectSpin(Up, Right, 15f, 0.8f, -0.4f);

            Assert.That(Mathf.Abs(Vector3.Dot(spin.normalized, Along)), Is.LessThan(1e-4f));
        }

        [Test]
        public void NoSpin_NoAngularVelocity()
        {
            Assert.That(SpawnUseCase.EjectSpin(Up, Right, 0f, 0.8f, 0.3f), Is.EqualTo(Vector3.zero));
        }
    }
}
