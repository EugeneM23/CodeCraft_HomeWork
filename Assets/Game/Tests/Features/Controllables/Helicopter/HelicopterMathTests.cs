using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class HelicopterMathTests
    {
        private const float STEP = 0.1f;

        // ------------------------------------------------------------ flight

        [Test]
        public void Planar_FromRest_SpeedsUpAtAcceleration()
        {
            Vector3 v = HelicopterFlightMath.ApproachPlanar(Vector3.zero, new Vector3(0f, 0f, 10f), 7f, 5f, STEP);
            Assert.That(v.z, Is.EqualTo(0.7f).Within(1e-5f));
        }

        [Test]
        public void Planar_StickReleased_SlowsAtBraking()
        {
            Vector3 v = HelicopterFlightMath.ApproachPlanar(new Vector3(0f, 0f, 10f), Vector3.zero, 7f, 5f, STEP);
            Assert.That(v.z, Is.EqualTo(9.5f).Within(1e-5f));
        }

        [Test]
        public void Planar_IgnoresVertical()
        {
            Vector3 v = HelicopterFlightMath.ApproachPlanar(new Vector3(0f, 3f, 0f), new Vector3(0f, 5f, 0f), 7f, 5f, STEP);
            Assert.That(v, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Climb_FarBelow_IsCappedUpwards()
        {
            Assert.That(HelicopterFlightMath.ClimbSpeed(0f, 5f, 3f, 3f), Is.EqualTo(3f));
        }

        [Test]
        public void Climb_Above_GoesDown()
        {
            Assert.That(HelicopterFlightMath.ClimbSpeed(5.5f, 5f, 3f, 3f), Is.EqualTo(-1.5f).Within(1e-5f));
        }

        [Test]
        public void Touchdown_SkidsAtGround()
        {
            Assert.That(new TouchdownDetector().Step(0.03f, -2f, STEP), Is.True);
        }

        [Test]
        public void Touchdown_StalledLongEnough()
        {
            TouchdownDetector detector = new();
            Assert.That(detector.Step(1f, 0f, 0.1f), Is.False);
            Assert.That(detector.Step(1f, 0f, 0.1f), Is.False);
            Assert.That(detector.Step(1f, 0f, 0.1f), Is.True);
        }

        [Test]
        public void Touchdown_StillComingDown_IsNotDown()
        {
            TouchdownDetector detector = new();
            for (int i = 0; i < 10; i++)
                Assert.That(detector.Step(1f, -1f, 0.1f), Is.False);
        }

        [Test]
        public void Touchdown_StallInterrupted_StartsOver()
        {
            TouchdownDetector detector = new();
            detector.Step(1f, 0f, 0.2f);
            detector.Step(1f, -1f, 0.1f);
            Assert.That(detector.Step(1f, 0f, 0.2f), Is.False);
        }

        // ------------------------------------------------------------ camera point

        private static readonly Vector3 View = Quaternion.Euler(70f, 0f, 0f) * Vector3.forward;

        [Test]
        public void CameraPoint_SlidesDownTheView_ToTheShareOfTheHeight()
        {
            Vector3 point = HelicopterCameraPointBehaviour.Place(new Vector3(1f, 5f, 2f), View, 0f, 0.2f);
            Assert.That(point.y, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void CameraPoint_StaysOnTheLineThroughTheHelicopter()
        {
            Vector3 body = new(1f, 5f, 2f);
            Vector3 point = HelicopterCameraPointBehaviour.Place(body, View, 0f, 0.2f);
            Vector3 along = (body - point).normalized;
            Assert.That(Vector3.Angle(along, -View), Is.LessThan(0.01f));
        }

        [Test]
        public void CameraPoint_OnTheGround_IsTheHelicopter()
        {
            Vector3 body = new(1f, 0f, 2f);
            Assert.That(HelicopterCameraPointBehaviour.Place(body, View, 0f, 0.2f), Is.EqualTo(body));
        }

        [Test]
        public void CameraPoint_LevelView_IsTheHelicopter()
        {
            Vector3 body = new(1f, 5f, 2f);
            Assert.That(HelicopterCameraPointBehaviour.Place(body, Vector3.forward, 0f, 0.2f), Is.EqualTo(body));
        }

        // ------------------------------------------------------------ tilt

        private static Vector2 Tilt(Vector3 acceleration, Vector3 velocity = default, float yawRate = 0f) =>
            HelicopterTiltMath.TargetAngles(acceleration, velocity, yawRate, 2f, 0.4f, 0.05f, 15f);

        [Test]
        public void Tilt_SpeedingUpForward_NoseDown()
        {
            Assert.That(Tilt(new Vector3(0f, 0f, 5f)).x, Is.GreaterThan(0f));
        }

        [Test]
        public void Tilt_BrakingFromCruise_NoseUp()
        {
            Assert.That(Tilt(new Vector3(0f, 0f, -5f), new Vector3(0f, 0f, 10f)).x, Is.LessThan(0f));
        }

        [Test]
        public void Tilt_SpeedingUpRight_BanksRight()
        {
            // Rolling right lowers the right side: negative angle about Z.
            Assert.That(Tilt(new Vector3(5f, 0f, 0f)).y, Is.LessThan(0f));
        }

        [Test]
        public void Tilt_TurningRight_BanksRight()
        {
            Assert.That(Tilt(Vector3.zero, Vector3.zero, 90f).y, Is.LessThan(0f));
        }

        [Test]
        public void Tilt_IsCapped()
        {
            Vector2 angles = Tilt(new Vector3(100f, 0f, 100f));
            Assert.That(angles.x, Is.EqualTo(15f));
            Assert.That(angles.y, Is.EqualTo(-15f));
        }

        [Test]
        public void Spring_WithoutInput_SettlesAtZero()
        {
            float angle = 10f;
            float velocity = 0f;

            for (int i = 0; i < 300; i++)
                HelicopterTiltMath.Spring(ref angle, ref velocity, 0f, 2f, 0.5f, 1f / 60f);

            Assert.That(angle, Is.EqualTo(0f).Within(0.05f));
        }
    }
}
