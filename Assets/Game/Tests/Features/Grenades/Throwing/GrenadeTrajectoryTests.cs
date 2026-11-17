using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class GrenadeTrajectoryTests
    {
        private static readonly Vector3 Gravity = new(0f, -9.81f, 0f);

        /// <summary>Where a drag-free body launched with <paramref name="velocity"/> is after <paramref name="time"/>.</summary>
        private static Vector3 PositionAt(Vector3 from, Vector3 velocity, float time) =>
            from + velocity * time + 0.5f * time * time * Gravity;

        private static float FlatSpeed(Vector3 velocity) => new Vector2(velocity.x, velocity.z).magnitude;

        [Test]
        public void TargetWithinRange_IsLeftAlone()
        {
            Vector3 target = new(3f, 2f, 4f);

            Assert.That(GrenadeTrajectory.ClampToRange(Vector3.zero, target, 10f), Is.EqualTo(target));
        }

        [Test]
        public void TargetBeyondRange_IsPulledInOnTheGround_KeepingItsHeight()
        {
            Vector3 from = new(0f, 1f, 0f);

            Vector3 clamped = GrenadeTrajectory.ClampToRange(from, new Vector3(30f, 5f, 40f), 10f);

            Assert.That(Vector3.Distance(clamped, new Vector3(6f, 5f, 8f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void LongThrow_FliesAtTheThrowSpeed_AndLandsOnTheTarget()
        {
            Vector3 from = new(0f, 1.5f, 0f);
            Vector3 to = new(6f, 0f, 8f);

            Vector3 velocity = GrenadeTrajectory.GetLaunchVelocity(from, to, 12f, 0.6f, Gravity);

            float flightTime = 10f / 12f;
            Assert.That(FlatSpeed(velocity), Is.EqualTo(12f).Within(1e-3f));
            Assert.That(Vector3.Distance(PositionAt(from, velocity, flightTime), to), Is.LessThan(1e-3f));
        }

        [Test]
        public void ThrowAtOwnFeet_StillArcsForTheMinimumFlightTime()
        {
            Vector3 from = new(0f, 1.5f, 0f);
            Vector3 to = new(1f, 0f, 0f);

            Vector3 velocity = GrenadeTrajectory.GetLaunchVelocity(from, to, 12f, 0.6f, Gravity);

            Assert.That(FlatSpeed(velocity), Is.EqualTo(1f / 0.6f).Within(1e-3f), "slower than the throw speed");
            Assert.That(velocity.y, Is.GreaterThan(0f), "launched upwards, not into the floor");
            Assert.That(Vector3.Distance(PositionAt(from, velocity, 0.6f), to), Is.LessThan(1e-3f));
        }

        [Test]
        public void TargetAboveTheHand_IsStillReached()
        {
            Vector3 from = Vector3.zero;
            Vector3 to = new(12f, 6f, 0f);

            Vector3 velocity = GrenadeTrajectory.GetLaunchVelocity(from, to, 12f, 0.6f, Gravity);

            Assert.That(Vector3.Distance(PositionAt(from, velocity, 1f), to), Is.LessThan(1e-3f));
        }

        [Test]
        public void ZeroThrowSpeed_GivesAFiniteVelocity()
        {
            Vector3 velocity = GrenadeTrajectory.GetLaunchVelocity(Vector3.zero, new Vector3(5f, 0f, 0f), 0f, 0f, Gravity);

            Assert.That(float.IsNaN(velocity.x) || float.IsInfinity(velocity.x), Is.False);
            Assert.That(float.IsNaN(velocity.y) || float.IsInfinity(velocity.y), Is.False);
        }
    }
}
