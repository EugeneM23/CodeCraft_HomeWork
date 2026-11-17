using NUnit.Framework;

namespace Game.Tests
{
    public sealed class TankDriveMathTests
    {
        private const float ACCELERATION = 6f;
        private const float BRAKING = 12f;
        private const float STEP = 0.1f;

        private static float Step(float current, float target) =>
            TankDriveMath.Approach(current, target, ACCELERATION, BRAKING, STEP);

        [Test]
        public void FromRest_SpeedsUpAtAcceleration()
        {
            Assert.That(Step(0f, 6f), Is.EqualTo(0.6f).Within(1e-5f));
        }

        [Test]
        public void Reverse_FromRest_SpeedsUpAtAcceleration()
        {
            Assert.That(Step(0f, -6f), Is.EqualTo(-0.6f).Within(1e-5f));
        }

        [Test]
        public void ThrottleReleased_SlowsAtBraking()
        {
            Assert.That(Step(5f, 0f), Is.EqualTo(3.8f).Within(1e-5f));
        }

        [Test]
        public void ReverseWhileMovingForward_BrakesFirst()
        {
            Assert.That(Step(5f, -6f), Is.EqualTo(3.8f).Within(1e-5f));
        }

        [Test]
        public void ReverseWhileMovingForward_StopsAtZeroBeforeReversing()
        {
            Assert.That(Step(0.5f, -6f), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void NeverOvershootsTarget()
        {
            Assert.That(Step(5.9f, 6f), Is.EqualTo(6f).Within(1e-5f));
            Assert.That(Step(0.5f, 0f), Is.EqualTo(0f).Within(1e-5f));
        }
    }
}
