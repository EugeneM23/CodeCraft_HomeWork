using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class HelicopterFlightBehaviourTests
    {
        private static readonly Vector3 Origin = new(-4000f, 300f, 4000f);

        private const float STEP = 0.02f;
        private const float CRUISE = 10f;
        private const float ACCELERATION = 10f;
        private const float BRAKING = 4f;
        private const float CLIMB_GAIN = 1f;
        private const float MAX_CLIMB = 5f;
        private const float VERTICAL_ACCELERATION = 20f;
        private const float MAX_SPEED = 20f;
        private const float BOOST = 2f;

        private GameObject _gameObject;
        private Rigidbody _body;
        private IEntity _helicopter;
        private HelicopterFlightBehaviour _flight;
        private int _touchdowns;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("Helicopter");
            _gameObject.transform.position = Origin;
            _body = _gameObject.AddComponent<Rigidbody>();
            _body.useGravity = false;
            Physics.SyncTransforms();

            _helicopter = new Entity();
            _helicopter.AddRiggedBody(_body);
            _helicopter.AddDriveInput(new ReactiveVariable<Vector2>());
            _helicopter.AddDriveBoost(new ReactiveVariable<bool>());
            _helicopter.AddMoveSpeed(new ReactiveVariable<float>(MAX_SPEED));
            _helicopter.AddDriver(new ReactiveVariable<IEntity>());
            _helicopter.AddHealth(new Health(10, 10));
            _helicopter.AddFlightState(new ReactiveVariable<HelicopterFlightState>(HelicopterFlightState.Landed));

            _touchdowns = 0;
            _flight = new HelicopterFlightBehaviour(Origin.y, CRUISE, ACCELERATION, BRAKING, CLIMB_GAIN, MAX_CLIMB,
                VERTICAL_ACCELERATION, () => _touchdowns++, BOOST);
            _flight.Init(_helicopter);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_gameObject);

        private HelicopterFlightState State
        {
            get => _helicopter.GetFlightState().Value;
            set => _helicopter.GetFlightState().Value = value;
        }

        private void Tick(int times = 1)
        {
            for (int i = 0; i < times; i++)
                _flight.FixedTick(_helicopter, STEP);
        }

        private void SitAtHeight(float height)
        {
            _gameObject.transform.position = Origin + Vector3.up * height;
            Physics.SyncTransforms();
        }

        private void BoardPilot() => _helicopter.GetDriver().Value = new Entity();

        [Test]
        public void Landed_NoPilot_HeldInPlace_FallingIsKept()
        {
            _body.linearVelocity = new Vector3(3f, -2f, 1f);

            Tick();

            Assert.That(State, Is.EqualTo(HelicopterFlightState.Landed));
            Assert.That(_body.useGravity, Is.True);
            Assert.That(_body.linearVelocity, Is.EqualTo(new Vector3(0f, -2f, 0f)));
        }

        [Test]
        public void Landed_NoPilot_IsNotLiftedUpwards()
        {
            _body.linearVelocity = new Vector3(0f, 2f, 0f);

            Tick();

            Assert.That(_body.linearVelocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void PilotGetsIn_TakesOffAtOnce()
        {
            BoardPilot();

            Tick();

            Assert.That(State, Is.EqualTo(HelicopterFlightState.TakingOff));
            Assert.That(_body.useGravity, Is.False);
            // Climb wanted is capped at MAX_CLIMB; the vertical speed ramps towards it.
            Assert.That(_body.linearVelocity.y, Is.EqualTo(VERTICAL_ACCELERATION * STEP).Within(1e-4f));
        }

        [Test]
        public void PilotLeftInsideOnTheGround_StaysDownUntilTheStickMoves()
        {
            BoardPilot();
            Tick();
            State = HelicopterFlightState.Landed;
            _body.linearVelocity = Vector3.zero;

            Tick();
            Assert.That(State, Is.EqualTo(HelicopterFlightState.Landed), "idle stick");

            _helicopter.GetDriveInput().Value = new Vector2(0f, 1f);
            Tick();
            Assert.That(State, Is.EqualTo(HelicopterFlightState.TakingOff), "stick moved");
        }

        [Test]
        public void StickForward_SpeedsUpAlongWorldZ_AtAcceleration()
        {
            State = HelicopterFlightState.Flying;
            BoardPilot();
            _helicopter.GetDriveInput().Value = new Vector2(0f, 1f);

            Tick();

            Assert.That(_body.linearVelocity.x, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(_body.linearVelocity.z, Is.EqualTo(ACCELERATION * STEP).Within(1e-5f));
        }

        [Test]
        public void StickRight_FliesAlongWorldX()
        {
            State = HelicopterFlightState.Flying;
            BoardPilot();
            _helicopter.GetDriveInput().Value = new Vector2(1f, 0f);

            Tick();

            Assert.That(_body.linearVelocity.x, Is.EqualTo(ACCELERATION * STEP).Within(1e-5f));
            Assert.That(_body.linearVelocity.z, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void Boost_GetsUpToSpeedFaster()
        {
            State = HelicopterFlightState.Flying;
            BoardPilot();
            _helicopter.GetDriveInput().Value = new Vector2(0f, 1f);
            _helicopter.GetDriveBoost().Value = true;

            Tick();

            Assert.That(_body.linearVelocity.z, Is.EqualTo(ACCELERATION * BOOST * STEP).Within(1e-5f));
        }

        [Test]
        public void NosedIntoAWall_SpeedTheBodyLostIsDropped()
        {
            State = HelicopterFlightState.Flying;
            BoardPilot();
            _helicopter.GetDriveInput().Value = new Vector2(0f, 1f);
            Tick(5);

            _body.linearVelocity = Vector3.zero;
            Tick();

            Assert.That(_body.linearVelocity.z, Is.EqualTo(ACCELERATION * STEP).Within(1e-5f));
        }

        [Test]
        public void PilotGone_StartsLanding_AndBrakesIgnoringTheStick()
        {
            SitAtHeight(CRUISE);
            State = HelicopterFlightState.Flying;
            BoardPilot();
            _helicopter.GetDriveInput().Value = new Vector2(0f, 1f);
            Tick(5);
            float cruiseSpeed = _body.linearVelocity.z;

            _helicopter.GetDriver().Value = null;
            Tick();

            Assert.That(State, Is.EqualTo(HelicopterFlightState.Landing));
            Assert.That(_body.linearVelocity.z, Is.EqualTo(cruiseSpeed - BRAKING * STEP).Within(1e-5f));
        }

        [Test]
        public void Destroyed_PilotNoLongerFlies_ItComesDown()
        {
            SitAtHeight(CRUISE);
            State = HelicopterFlightState.Flying;
            BoardPilot();
            _helicopter.GetHealth().Reduce(10);

            Tick();

            Assert.That(State, Is.EqualTo(HelicopterFlightState.Landing));
        }

        [Test]
        public void TakingOff_ReachesCruiseHeight_IsFlying()
        {
            State = HelicopterFlightState.TakingOff;
            BoardPilot();
            SitAtHeight(CRUISE);

            Tick();

            Assert.That(State, Is.EqualTo(HelicopterFlightState.Flying));
        }

        [Test]
        public void TakingOff_StillLow_KeepsClimbing()
        {
            State = HelicopterFlightState.TakingOff;
            BoardPilot();
            SitAtHeight(2f);

            Tick();

            Assert.That(State, Is.EqualTo(HelicopterFlightState.TakingOff));
            Assert.That(_body.linearVelocity.y, Is.GreaterThan(0f));
        }

        [Test]
        public void Landing_HighUp_ComesDownWithoutTouchingDown()
        {
            State = HelicopterFlightState.Landing;
            SitAtHeight(5f);

            Tick();

            Assert.That(_body.linearVelocity.y, Is.LessThan(0f));
            Assert.That(State, Is.EqualTo(HelicopterFlightState.Landing));
            Assert.That(_touchdowns, Is.EqualTo(0));
        }

        [Test]
        public void Landing_SkidsAtTheGround_TouchesDownOnce()
        {
            State = HelicopterFlightState.Landing;
            SitAtHeight(0.03f);

            Tick();

            Assert.That(State, Is.EqualTo(HelicopterFlightState.Landed));
            Assert.That(_body.useGravity, Is.True);
            Assert.That(_touchdowns, Is.EqualTo(1));

            Tick();
            Assert.That(_touchdowns, Is.EqualTo(1), "a landed helicopter does not touch down again");
        }

        [Test]
        public void RequestLanding_InTheAir_StartsLanding()
        {
            State = HelicopterFlightState.Flying;

            _flight.RequestLanding();

            Assert.That(State, Is.EqualTo(HelicopterFlightState.Landing));
        }

        [Test]
        public void RequestLanding_OnTheGround_IsIgnored()
        {
            _flight.RequestLanding();

            Assert.That(State, Is.EqualTo(HelicopterFlightState.Landed));
        }
    }
}
