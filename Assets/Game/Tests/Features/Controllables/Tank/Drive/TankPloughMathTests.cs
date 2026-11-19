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
        public void GlancingARutWallAt12Degrees_PassesIsNoseHit_ButDoesNotFaceTravel()
        {
            // A wall along the way, turned 12° into it: the nose brushes it at 13 m/s.
            var v = new Vector3(0f, 0f, 13f);
            Vector3 wall = Quaternion.Euler(0f, -12f, 0f) * Vector3.left;
            float closing = -Vector3.Dot(v, wall);
            Assert.IsTrue(TankPloughMath.IsNoseHit(new Vector3(0.9f, -0.15f, 1.3f), wall, Vector3.forward, closing, 2f),
                "on its own the nose test lets it through");
            Assert.IsFalse(TankPloughMath.FacesTravel(wall, v), "but the face does not stand across the way");
        }

        [Test]
        public void FacesTravel_AStepHeadOnOrTurnedUpTo45Degrees()
        {
            var v = new Vector3(0f, -2f, 13f);
            Assert.IsTrue(TankPloughMath.FacesTravel(new Vector3(0f, 0.05f, -1f).normalized, v), "head on");
            Assert.IsTrue(TankPloughMath.FacesTravel(Quaternion.Euler(0f, 40f, 0f) * new Vector3(0f, 0.2f, -1f).normalized, v), "40° aside");
            Assert.IsFalse(TankPloughMath.FacesTravel(Quaternion.Euler(0f, 50f, 0f) * new Vector3(0f, 0.2f, -1f).normalized, v), "50° aside");
        }

        [Test]
        public void FacesTravel_LevelGroundOrAVerticalFall_Counts()
        {
            Assert.IsTrue(TankPloughMath.FacesTravel(Vector3.up, new Vector3(0f, -5f, 6f)), "landing nose down on level ground");
            Assert.IsTrue(TankPloughMath.FacesTravel(new Vector3(0f, 0.5f, -1f).normalized, Vector3.down), "falling straight down");
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
            Vector3 after = TankPloughMath.Deflect(v, Slope32, 1f, 0.33f, 0.3f);
            Assert.That(after.y, Is.GreaterThan(0f), "goes up");
            Assert.That(after.z, Is.GreaterThan(0f), "still forward");
            Assert.That(Vector3.Dot(after, Slope32), Is.GreaterThanOrEqualTo(0f), "no longer into the slope");
            Assert.That(after.magnitude, Is.EqualTo(8f * 0.67f).Within(1e-3f));
        }

        [Test]
        public void Deflect_Zero_GoesExactlyAlongTheSlope()
        {
            var v = new Vector3(0f, 0f, 8f);
            Vector3 after = TankPloughMath.Deflect(v, Slope32, 1f, 0.33f, 0f);
            Assert.That(Vector3.Dot(after, Slope32), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(after.magnitude, Is.EqualTo(8f * 0.67f).Within(1e-3f));
        }

        [Test]
        public void RampAhead_RisesStraightAheadAtTheAngle()
        {
            var v = new Vector3(0f, -3f, 12f);
            Vector3 ramp = TankPloughMath.RampAhead(v, 18f);
            Assert.That(Vector3.Angle(ramp, Vector3.up), Is.EqualTo(18f).Within(1e-3f));
            Assert.That(ramp.x, Is.EqualTo(0f).Within(1e-6f), "no sideways");
            Assert.That(ramp.z, Is.LessThan(0f), "faces the hull coming at it");
            Assert.That(ramp.magnitude, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void RampAhead_DoesNotCareHowTheHullDives()
        {
            Assert.That(TankPloughMath.RampAhead(new Vector3(3f, -6f, 4f), 25f),
                Is.EqualTo(TankPloughMath.RampAhead(new Vector3(3f, 1f, 4f), 25f)).Using(Vector3EqualityComparer.Instance));
        }

        [Test]
        public void RampAhead_StraightDown_IsUp()
        {
            Assert.That(TankPloughMath.RampAhead(new Vector3(0f, -5f, 0f), 18f), Is.EqualTo(Vector3.up));
        }

        [Test]
        public void Deflect_AlongRampAhead_ClimbsAtTheRamp()
        {
            var v = new Vector3(0f, -3f, 12f);
            Vector3 after = TankPloughMath.Deflect(v, TankPloughMath.RampAhead(v, 18f), 1f, 0.3f, 0f);
            Assert.That(Mathf.Atan2(after.y, after.z) * Mathf.Rad2Deg, Is.EqualTo(18f).Within(1e-3f));
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
        public void ChunkThrow_GoesAheadAndUp_ScaledBySpeed()
        {
            var v = new Vector3(0f, 0f, 8f);
            Vector3 thrown = TankPloughMath.ChunkThrow(v, 1.1f, 0.6f);
            Assert.That(thrown, Is.EqualTo(new Vector3(0f, 1.1f, 1f) * (8f * 0.6f)).Using(Vector3EqualityComparer.Instance));
        }

        [Test]
        public void ChunkThrow_NoseDown_StillGoesUpAndLevelAhead()
        {
            var v = new Vector3(3f, -6f, 4f); // falling into a crater
            Vector3 thrown = TankPloughMath.ChunkThrow(v, 1f, 0.5f);
            Assert.That(thrown.y, Is.GreaterThan(0f), "up, not down the way the hull falls");
            Vector3 flat = new Vector3(thrown.x, 0f, thrown.z);
            Assert.That(Vector3.Angle(flat, new Vector3(3f, 0f, 4f)), Is.LessThan(1e-3f), "ahead the way the hull goes");
            Assert.That(thrown.y, Is.EqualTo(flat.magnitude).Within(1e-4f), "lift 1 is 45°");
        }

        [Test]
        public void ChunkThrow_StraightDown_GoesStraightUp()
        {
            Vector3 thrown = TankPloughMath.ChunkThrow(new Vector3(0f, -5f, 0f), 1f, 1f);
            Assert.That(thrown, Is.EqualTo(new Vector3(0f, 5f, 0f)).Using(Vector3EqualityComparer.Instance));
        }

        [Test]
        public void ChunkVolume_IsVoxelsTimesVoxelVolumeTimesShare()
        {
            Assert.That(TankPloughMath.ChunkVolume(5000, 0.1f, 0.2f), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void ChunkVolume_NothingDugOrNoShare_IsZero()
        {
            Assert.That(TankPloughMath.ChunkVolume(0, 0.1f, 0.5f), Is.EqualTo(0f));
            Assert.That(TankPloughMath.ChunkVolume(100, 0.1f, 0f), Is.EqualTo(0f));
            Assert.That(TankPloughMath.ChunkVolume(100, 0.1f, -1f), Is.EqualTo(0f));
        }

        [Test]
        public void ChunkVolume_NotFinite_IsZero()
        {
            Assert.That(TankPloughMath.ChunkVolume(100, float.NaN, 0.5f), Is.EqualTo(0f));
            Assert.That(TankPloughMath.ChunkVolume(100, float.PositiveInfinity, 0.5f), Is.EqualTo(0f));
            Assert.That(TankPloughMath.ChunkVolume(int.MaxValue, 1e20f, 1f), Is.EqualTo(0f));
        }

        [Test]
        public void ImpactClosing_IsAgainstA30DegreeRampAhead()
        {
            var v = new Vector3(0f, -2.5f, 13f);
            var slope30 = new Vector3(0f, Mathf.Cos(30f * Mathf.Deg2Rad), -Mathf.Sin(30f * Mathf.Deg2Rad));
            Assert.That(TankPloughMath.ImpactClosing(v), Is.EqualTo(-Vector3.Dot(v, slope30)).Within(1e-3f));
        }

        [Test]
        public void ImpactClosing_GrowsWithSpeed()
        {
            Assert.That(TankPloughMath.ImpactClosing(new Vector3(0f, -2f, 8f)),
                Is.LessThan(TankPloughMath.ImpactClosing(new Vector3(0f, -2f, 12f))));
        }

        [Test]
        public void ImpactStrength_IsZeroBelowTheCommonMinimum()
        {
            Assert.That(TankPloughMath.ImpactStrength(TankPloughMath.IMPACT_MIN_CLOSING - 0.1f, 1f), Is.EqualTo(0f));
        }

        [Test]
        public void ImpactStrength_ACraterHitAt13MetresASecond_DoesNotSaturate()
        {
            float impact = TankPloughMath.ImpactStrength(12.9f, 1f);
            Assert.That(impact, Is.GreaterThan(0.5f));
            Assert.That(impact, Is.LessThan(1f));
        }

        [Test]
        public void ImpactStrength_GrowsWithTheClosingSpeed()
        {
            Assert.That(TankPloughMath.ImpactStrength(8f, 1f), Is.LessThan(TankPloughMath.ImpactStrength(12f, 1f)));
        }

        [Test]
        public void ImpactStrength_IsScaledByThePreset_AndNeverAboveOne()
        {
            float full = TankPloughMath.ImpactStrength(9f, 1f);
            Assert.That(TankPloughMath.ImpactStrength(9f, 0.6f), Is.EqualTo(full * 0.6f).Within(1e-5f));
            Assert.That(TankPloughMath.ImpactStrength(9f, 0.8f), Is.EqualTo(full * 0.8f).Within(1e-5f));
            Assert.That(TankPloughMath.ImpactStrength(100f, 1.5f), Is.EqualTo(1f));
        }

        [Test]
        public void ImpactStrength_SameHit_SubtleBelowPunchyBelowCinema()
        {
            const float closing = 11f;
            float subtle = TankPloughMath.ImpactStrength(closing, 0.6f);
            float punchy = TankPloughMath.ImpactStrength(closing, 0.8f);
            float cinema = TankPloughMath.ImpactStrength(closing, 1f);
            Assert.That(subtle, Is.LessThan(punchy));
            Assert.That(punchy, Is.LessThan(cinema));
        }
    }
}
