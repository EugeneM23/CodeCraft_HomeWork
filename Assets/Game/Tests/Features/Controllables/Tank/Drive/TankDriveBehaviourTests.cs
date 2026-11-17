using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class TankDriveBehaviourTests
    {
        private static readonly Vector3 Origin = new(-4400f, 300f, 4400f);

        private const float STEP = 0.02f;
        private const float MASS = 2f;
        private const float ACCELERATION = 5f;
        private const float BRAKING = 8f;
        private const float TURN_ACCELERATION = 1000f;
        private const float MAX_SPEED = 10f;
        private const float TURN_SPEED = 90f;

        private GameObject _hull;
        private Rigidbody _body;
        private TankSuspension _suspension;
        private IEntity _tank;
        private TankDriveBehaviour _drive;

        [SetUp]
        public void SetUp()
        {
            _hull = new GameObject("Tank");
            _hull.transform.position = Origin;
            _body = _hull.AddComponent<Rigidbody>();
            _body.useGravity = false;
            _body.mass = MASS;

            Transform left = Wheel("Left", -1f);
            Transform right = Wheel("Right", 1f);
            _suspension = new TankSuspension(new[] { left, right }, new[] { left, right }, 0.3f, 1f, 0.2f, 0.5f, 0.5f)
            {
                GroundedCount = 2,
                ContactCentre = Origin + Vector3.down * 0.8f,
                GroundNormal = Vector3.up
            };
            Physics.SyncTransforms();

            _tank = new Entity();
            _tank.AddRiggedBody(_body);
            _tank.AddSuspension(_suspension);
            _tank.AddDriveInput(new ReactiveVariable<Vector2>());
            _tank.AddMoveSpeed(new ReactiveVariable<float>(MAX_SPEED));
            _tank.AddRotationSpeed(new ReactiveVariable<float>(TURN_SPEED));
            _tank.AddDriver(new ReactiveVariable<IEntity>(new Entity()));
            _tank.AddHealth(new Health(10, 10));

            _drive = new TankDriveBehaviour(ACCELERATION, BRAKING, TURN_ACCELERATION);
            _drive.Init(_tank);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_hull);

        private Transform Wheel(string name, float x)
        {
            Transform mount = new GameObject(name).transform;
            mount.SetParent(_hull.transform, false);
            mount.localPosition = new Vector3(x, 0f, 0f);
            return mount;
        }

        private void Steer(float x, float y) => _tank.GetDriveInput().Value = new Vector2(x, y);

        private void Tick() => _drive.FixedTick(_tank, STEP);

        /// <summary>The first step's turn rate: the stick's wanted rate is reached at TURN_ACCELERATION.</summary>
        private const float FIRST_TURN_RATE = 20f * Mathf.Deg2Rad;

        [Test]
        public void TurnStick_SpinsTheHullAboutItsUp()
        {
            Steer(1f, 0f);

            Tick();

            Assert.That(_body.angularVelocity.y, Is.EqualTo(FIRST_TURN_RATE).Within(1e-5f));
        }

        [Test]
        public void HalfTheWheelsDown_HalfTheGrip()
        {
            _suspension.GroundedCount = 1;
            Steer(1f, 0f);

            Tick();

            Assert.That(_body.angularVelocity.y, Is.EqualTo(FIRST_TURN_RATE * 0.5f).Within(1e-5f));
        }

        [Test]
        public void Turning_LeavesPitchAndRollToTheSprings()
        {
            _body.angularVelocity = new Vector3(0.5f, 0f, -0.3f);
            Steer(1f, 0f);

            Tick();

            Assert.That(_body.angularVelocity.x, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(_body.angularVelocity.z, Is.EqualTo(-0.3f).Within(1e-5f));
        }

        [Test]
        public void InTheAir_NothingIsDriven()
        {
            _suspension.GroundedCount = 0;
            _body.angularVelocity = new Vector3(0f, 1f, 0f);
            Steer(1f, 1f);

            Tick();

            Assert.That(_body.angularVelocity, Is.EqualTo(new Vector3(0f, 1f, 0f)));
            Assert.That(_body.GetAccumulatedForce(), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void NobodyInside_TracksHoldTheHeading()
        {
            _tank.GetDriver().Value = null;
            _body.angularVelocity = new Vector3(0f, 1f, 0f);
            Steer(1f, 0f);

            Tick();

            Assert.That(_body.angularVelocity.y, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void Destroyed_TracksHoldTheHeading()
        {
            _tank.GetHealth().Reduce(10);
            _body.angularVelocity = new Vector3(0f, 1f, 0f);
            Steer(1f, 0f);

            Tick();

            Assert.That(_body.angularVelocity.y, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void Throttle_PushesAlongTheHull_AtAcceleration()
        {
            Steer(0f, 1f);

            Tick();

            // One step of speed (ACCELERATION·STEP) given to MASS within STEP.
            Vector3 force = _body.GetAccumulatedForce();
            Assert.That(Vector3.Distance(force, Vector3.forward * (ACCELERATION * MASS)), Is.LessThan(1e-3f));
        }

        [Test]
        public void SlidingSideways_TracksTakeTheSlideOut()
        {
            _body.linearVelocity = new Vector3(3f, 0f, 0f);

            Tick();

            Vector3 force = _body.GetAccumulatedForce();
            Assert.That(Vector3.Distance(force, Vector3.left * (3f * MASS / STEP)), Is.LessThan(1e-2f));
        }

        private const float BOOST = 2f;

        [Test]
        public void Shift_TurnsFaster()
        {
            Boosted();
            Steer(1f, 0f);

            Tick();

            Assert.That(_body.angularVelocity.y, Is.EqualTo(FIRST_TURN_RATE * BOOST).Within(1e-5f));
        }

        [Test]
        public void Shift_TurnsToAHigherRate()
        {
            Boosted();
            Steer(1f, 0f);

            for (int i = 0; i < 50; i++)
                Tick();

            Assert.That(_body.angularVelocity.y, Is.EqualTo(TURN_SPEED * BOOST * Mathf.Deg2Rad).Within(1e-4f));
        }

        [Test]
        public void Shift_PicksUpSpeedFaster()
        {
            Boosted();
            Steer(0f, 1f);

            Tick();

            Vector3 force = _body.GetAccumulatedForce();
            Assert.That(Vector3.Distance(force, Vector3.forward * (ACCELERATION * BOOST * MASS)), Is.LessThan(1e-3f));
        }

        [Test]
        public void Shift_StickCentred_DoesNotPush()
        {
            Boosted();
            Steer(0f, 0f);

            Tick();

            Assert.That(_body.GetAccumulatedForce(), Is.EqualTo(Vector3.zero));
            Assert.That(_body.angularVelocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Shift_BrakesNoHarder()
        {
            Boosted();
            Steer(0f, 1f);
            Tick();
            _body.linearVelocity = Vector3.forward * (ACCELERATION * BOOST * STEP);
            Vector3 before = _body.GetAccumulatedForce();
            Steer(0f, -1f);

            Tick();

            // Reversing sheds the speed at BRAKING, not BRAKING × BOOST: braking is the brakes', not the engine's.
            float force = (_body.GetAccumulatedForce() - before).z;
            Assert.That(force, Is.EqualTo(-BRAKING * STEP * MASS / STEP).Within(1e-2f));
        }

        private void Boosted()
        {
            _tank.AddDriveBoost(new ReactiveVariable<bool>(true));
            _drive = new TankDriveBehaviour(ACCELERATION, BRAKING, TURN_ACCELERATION, boostMultiplier: BOOST);
            _drive.Init(_tank);
        }

        // Drift tuning for the tests: from 7 m/s along the hull, at most 8 m/s² of slide taken out.
        private const float DRIFT_SPEED = 7f;
        private const float SIDE_GRIP = 8f;

        [Test]
        public void HighSpeed_SmallSlip_TakenOutAtOnce()
        {
            Drifty();
            _body.linearVelocity = new Vector3(0.1f, 0f, MAX_SPEED);
            Steer(0f, 1f);

            Tick();

            Assert.That(_body.GetAccumulatedForce().x, Is.EqualTo(-0.1f * MASS / STEP).Within(1e-3f));
            Assert.That(_drive.Drifting, Is.False);
        }

        [Test]
        public void HighSpeed_SlipPastTheSideGrip_SlidesOut_TracksTakeOnlyTheSideGrip()
        {
            Drifty();
            _body.linearVelocity = new Vector3(3f, 0f, MAX_SPEED);
            Steer(0f, 1f);

            Tick();

            Assert.That(_body.GetAccumulatedForce().x, Is.EqualTo(-SIDE_GRIP * MASS).Within(1e-3f));
            Assert.That(_drive.Drifting, Is.True);
        }

        [Test]
        public void LowSpeed_BigSlip_TakenOutAtOnce()
        {
            Drifty();
            _tank.GetMoveSpeed().Value = 3f;
            _body.linearVelocity = new Vector3(3f, 0f, 3f);
            Steer(0f, 1f);

            Tick();

            Assert.That(_body.GetAccumulatedForce().x, Is.EqualTo(-3f * MASS / STEP).Within(1e-2f));
            Assert.That(_drive.Drifting, Is.False);
        }

        [Test]
        public void JustBelowTheDriftSpeed_GripClimbsWithTheSquareOfTheShortfall()
        {
            Drifty();
            _tank.GetMoveSpeed().Value = 5f;
            _body.linearVelocity = new Vector3(3f, 0f, 5f);
            Steer(0f, 1f);

            Tick();

            float ratio = DRIFT_SPEED / 5f;
            Assert.That(_body.GetAccumulatedForce().x, Is.EqualTo(-SIDE_GRIP * ratio * ratio * MASS).Within(1e-3f));
        }

        private void Drifty()
        {
            _drive = new TankDriveBehaviour(ACCELERATION, BRAKING, TURN_ACCELERATION, driftSpeed: DRIFT_SPEED,
                sideGrip: SIDE_GRIP);
            _drive.Init(_tank);
        }
    }
}
