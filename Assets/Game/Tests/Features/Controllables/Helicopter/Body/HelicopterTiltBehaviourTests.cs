using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class HelicopterTiltBehaviourTests
    {
        private static readonly Vector3 Origin = new(-4100f, 300f, 4100f);

        private const float FIXED_STEP = 0.02f;
        private const float FRAME = 1f / 60f;

        private GameObject _root;
        private Rigidbody _body;
        private Transform _visual;
        private IEntity _helicopter;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Helicopter");
            _root.transform.position = Origin;
            _body = _root.AddComponent<Rigidbody>();
            _body.useGravity = false;

            _visual = new GameObject("Body").transform;
            _visual.SetParent(_root.transform, false);

            _helicopter = new Entity();
            _helicopter.AddRiggedBody(_body);
            _helicopter.AddHullVisual(_visual);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        private HelicopterTiltBehaviour CreateTilt(float heading = 0f)
        {
            _root.transform.rotation = Quaternion.Euler(0f, heading, 0f);
            Physics.SyncTransforms();

            var tilt = new HelicopterTiltBehaviour(2f, 0.4f, 0.05f, 15f, 2f, 0.7f);
            tilt.Init(_helicopter);
            return tilt;
        }

        private void Frames(HelicopterTiltBehaviour tilt, int count)
        {
            for (int i = 0; i < count; i++)
                tilt.Tick(_helicopter, FRAME);
        }

        private float Pitch => Mathf.DeltaAngle(0f, _visual.localEulerAngles.x);
        private float Roll => Mathf.DeltaAngle(0f, _visual.localEulerAngles.z);

        [Test]
        public void Hovering_StaysLevel()
        {
            HelicopterTiltBehaviour tilt = CreateTilt();

            tilt.FixedTick(_helicopter, FIXED_STEP);
            Frames(tilt, 30);

            Assert.That(Quaternion.Angle(_visual.localRotation, Quaternion.identity), Is.LessThan(1e-3f));
        }

        [Test]
        public void SpeedingUpForward_NoseDips()
        {
            HelicopterTiltBehaviour tilt = CreateTilt();

            _body.linearVelocity = new Vector3(0f, 0f, 5f);
            tilt.FixedTick(_helicopter, FIXED_STEP);
            Frames(tilt, 10);

            Assert.That(Pitch, Is.GreaterThan(1f));
            Assert.That(Roll, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void SpeedingUpAlongTheTurnedHeading_StillNoseDown_NotABank()
        {
            HelicopterTiltBehaviour tilt = CreateTilt(heading: 90f);

            // World +X is the helicopter's own forward once it faces 90°.
            _body.linearVelocity = new Vector3(5f, 0f, 0f);
            tilt.FixedTick(_helicopter, FIXED_STEP);
            Frames(tilt, 10);

            Assert.That(Pitch, Is.GreaterThan(1f));
            Assert.That(Roll, Is.EqualTo(0f).Within(1e-2f));
        }

        [Test]
        public void SpeedingUpToTheRight_BanksRight()
        {
            HelicopterTiltBehaviour tilt = CreateTilt();

            _body.linearVelocity = new Vector3(5f, 0f, 0f);
            tilt.FixedTick(_helicopter, FIXED_STEP);
            Frames(tilt, 10);

            Assert.That(Roll, Is.LessThan(-1f));
        }

        [Test]
        public void LeanNeverPassesTheCapByMuch()
        {
            HelicopterTiltBehaviour tilt = CreateTilt();

            _body.linearVelocity = new Vector3(0f, 0f, 50f);
            tilt.FixedTick(_helicopter, FIXED_STEP);

            float most = 0f;
            for (int i = 0; i < 120; i++)
            {
                tilt.Tick(_helicopter, FRAME);
                most = Mathf.Max(most, Pitch);
            }

            // Capped at 15°; the spring at 0.7 damping overshoots it by a few percent at most.
            Assert.That(most, Is.LessThan(15f * 1.1f));
            Assert.That(Pitch, Is.EqualTo(15f).Within(0.5f));
        }

        [Test]
        public void LongFrame_StepsNoFurtherThanAThirtiethOfASecond()
        {
            HelicopterTiltBehaviour slow = CreateTilt();
            _body.linearVelocity = new Vector3(0f, 0f, 5f);
            slow.FixedTick(_helicopter, FIXED_STEP);
            slow.Tick(_helicopter, 1f);
            Quaternion afterLongFrame = _visual.localRotation;

            HelicopterTiltBehaviour capped = CreateTilt();
            capped.FixedTick(_helicopter, FIXED_STEP);
            capped.Tick(_helicopter, 1f / 30f);

            Assert.That(Quaternion.Angle(afterLongFrame, _visual.localRotation), Is.LessThan(1e-3f));
        }
    }
}
