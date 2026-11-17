using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace Game.Tests
{
    public sealed class TankPloughMathTests
    {
        private static readonly Vector3 Slope32 = new Vector3(0f, Mathf.Cos(32f * Mathf.Deg2Rad), -Mathf.Sin(32f * Mathf.Deg2Rad));

        [Test]
        public void Strength_IsZeroBelowMinClosing_AndOneFromFullClosing()
        {
            Assert.That(TankPloughMath.Strength(1.9f, 2f, 8f), Is.EqualTo(0f));
            Assert.That(TankPloughMath.Strength(5f, 2f, 8f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(TankPloughMath.Strength(9f, 2f, 8f), Is.EqualTo(1f));
        }

        [Test]
        public void HardHit_KeepsTwoThirdsOfTheSpeed()
        {
            Assert.That(TankPloughMath.SpeedAfter(9f, 1f, 0.33f), Is.EqualTo(6.03f).Within(1e-4f));
        }

        [Test]
        public void LightHit_StillCostsHalfTheLoss()
        {
            Assert.That(TankPloughMath.SpeedAfter(6f, 0.1f, 0.33f), Is.EqualTo(6f * (1f - 0.165f)).Within(1e-4f));
        }

        [Test]
        public void LevelHullIntoA32DegreeSlope_IsANoseHit()
        {
            var v = new Vector3(0f, 0f, 8f);
            float closing = -Vector3.Dot(v, Slope32);
            Assert.IsTrue(TankPloughMath.IsNoseHit(new Vector3(0f, -0.15f, 1.3f), Slope32, Vector3.forward, closing, 2f));
        }

        [Test]
        public void FlatBellyLanding_IsNotANoseHit()
        {
            var v = new Vector3(0f, -6f, 4f);
            float closing = -Vector3.Dot(v, Vector3.up);
            Assert.IsFalse(TankPloughMath.IsNoseHit(new Vector3(0f, -0.2f, 0.5f), Vector3.up, Vector3.forward, closing, 2f));
        }

        [Test]
        public void NoseDownLanding_IsANoseHit()
        {
            Vector3 forward = Quaternion.Euler(30f, 0f, 0f) * Vector3.forward; // pitched 30° nose down
            var v = new Vector3(0f, -5f, 6f);
            float closing = -Vector3.Dot(v, Vector3.up);
            Assert.IsTrue(TankPloughMath.IsNoseHit(new Vector3(0f, -0.2f, 1.2f), Vector3.up, forward, closing, 2f));
        }

        [Test]
        public void HitBehindTheMiddle_IsNotANoseHit()
        {
            var v = new Vector3(0f, 0f, 8f);
            float closing = -Vector3.Dot(v, Slope32);
            Assert.IsFalse(TankPloughMath.IsNoseHit(new Vector3(0f, -0.15f, -0.4f), Slope32, Vector3.forward, closing, 2f));
        }

        [Test]
        public void Deflect_TurnsTheVelocityUpTheSlope_AndCostsTheLoss()
        {
            var v = new Vector3(0f, 0f, 8f);
            Vector3 after = TankPloughMath.Deflect(v, Slope32, 1f, 0.33f, 0.3f, 0f);
            Assert.That(after.y, Is.GreaterThan(0f), "goes up");
            Assert.That(after.z, Is.GreaterThan(0f), "still forward");
            Assert.That(Vector3.Dot(after, Slope32), Is.GreaterThanOrEqualTo(0f), "no longer into the slope");
            Assert.That(after.magnitude, Is.EqualTo(8f * 0.67f).Within(1e-3f));
        }

        [Test]
        public void Deflect_HopAddsStraightUp()
        {
            var v = new Vector3(0f, 0f, 8f);
            Vector3 without = TankPloughMath.Deflect(v, Slope32, 1f, 0.33f, 0.3f, 0f);
            Vector3 with = TankPloughMath.Deflect(v, Slope32, 1f, 0.33f, 0.3f, 1.5f);
            Assert.That(with.y - without.y, Is.EqualTo(1.5f).Within(1e-4f));
        }

        [Test]
        public void Ramp_LeavesAGentleSlopeAsItIs()
        {
            Assert.That(TankPloughMath.Ramp(Slope32, 35f), Is.EqualTo(Slope32).Using(Vector3EqualityComparer.Instance));
        }

        [Test]
        public void Ramp_TipsAVerticalStepFaceBackToTheMaxSlope()
        {
            var face = new Vector3(0f, 0.07f, -1f).normalized;
            Vector3 ramp = TankPloughMath.Ramp(face, 30f);
            Assert.That(Vector3.Angle(ramp, Vector3.up), Is.EqualTo(30f).Within(1e-3f));
            Assert.That(ramp.z, Is.LessThan(0f), "still faces the nose");
            Assert.That(ramp.magnitude, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Ramp_LeavesAnOverhangsUndersideAsItIs()
        {
            var underside = new Vector3(0f, -0.3f, -1f).normalized;
            Assert.That(TankPloughMath.Ramp(underside, 30f), Is.EqualTo(underside).Using(Vector3EqualityComparer.Instance));
        }

        [Test]
        public void NoseUpSpin_OverridesANoseDownSpin()
        {
            var angular = new Vector3(0.5f, 0.2f, 0f); // +right: nose going down
            Vector3 after = TankPloughMath.NoseUpSpin(angular, Vector3.right, 0.8f);
            Assert.That(after, Is.EqualTo(new Vector3(-0.8f, 0.2f, 0f)).Using(Vector3EqualityComparer.Instance));
            Vector3 nose = Quaternion.AngleAxis(after.x * Mathf.Rad2Deg * 0.1f, Vector3.right) * Vector3.forward;
            Assert.That(nose.y, Is.GreaterThan(0f), "the nose goes up");
        }

        [Test]
        public void NoseUpSpin_KeepsAFasterNoseUpSpin()
        {
            var angular = new Vector3(-1.5f, 0f, 0.1f);
            Assert.That(TankPloughMath.NoseUpSpin(angular, Vector3.right, 0.8f),
                Is.EqualTo(angular).Using(Vector3EqualityComparer.Instance));
        }

        [Test]
        public void Throw_GoesForwardAndUp_ScaledBySpeed()
        {
            var v = new Vector3(0f, 0f, 8f);
            Vector3 thrown = TankPloughMath.Throw(v, Slope32, 0.8f, 0.9f);
            Assert.That(thrown.y, Is.GreaterThan(0f));
            Assert.That(thrown.z, Is.GreaterThan(0f));
            Vector3 along = Vector3.ProjectOnPlane(v, Slope32).normalized;
            Assert.That(thrown, Is.EqualTo((along + Slope32 * 0.8f) * (8f * 0.9f)).Using(Vector3EqualityComparer.Instance));
        }
    }
}
