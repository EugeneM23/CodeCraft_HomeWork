using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class RotorSpinBehaviourTests
    {
        private const float MAIN_SPEED = 360f;
        private const float TAIL_SPEED = 720f;
        private const float SPIN_UP = 1f;
        private const float SPIN_DOWN = 2f;

        private GameObject _root;
        private Transform _main;
        private Transform _tail;
        private IEntity _helicopter;
        private RotorSpinBehaviour _rotors;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Helicopter");
            _main = new GameObject("MainRotor").transform;
            _main.SetParent(_root.transform, false);
            _tail = new GameObject("TailRotor").transform;
            _tail.SetParent(_root.transform, false);

            _helicopter = new Entity();
            _helicopter.AddDriver(new ReactiveVariable<IEntity>());
            _helicopter.AddFlightState(new ReactiveVariable<HelicopterFlightState>(HelicopterFlightState.Landed));

            _rotors = new RotorSpinBehaviour(_main, _tail, MAIN_SPEED, TAIL_SPEED, SPIN_UP, SPIN_DOWN);
            _rotors.Init(_helicopter);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        /// <summary>Degrees the main rotor turns over one more tick.</summary>
        private float MainStep(float deltaTime)
        {
            Quaternion before = _main.localRotation;
            _rotors.Tick(_helicopter, deltaTime);
            return Quaternion.Angle(before, _main.localRotation);
        }

        [Test]
        public void EmptyOnTheGround_RotorsStandStill()
        {
            _rotors.Tick(_helicopter, 0.5f);

            Assert.That(_main.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(_tail.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void PilotInside_SpinsUpGradually_MainAboutUp_TailAboutRight()
        {
            _helicopter.GetDriver().Value = new Entity();

            _rotors.Tick(_helicopter, 0.1f);

            // A tenth of the way to full throttle, for a tenth of a second.
            Assert.That(Quaternion.Angle(Quaternion.identity, _main.localRotation),
                Is.EqualTo(MAIN_SPEED * 0.1f * 0.1f).Within(0.01f));
            Assert.That(Quaternion.Angle(Quaternion.identity, _tail.localRotation),
                Is.EqualTo(TAIL_SPEED * 0.1f * 0.1f).Within(0.01f));
            Assert.That(Vector3.Angle(_main.localRotation * Vector3.up, Vector3.up), Is.LessThan(0.01f));
            Assert.That(Vector3.Angle(_tail.localRotation * Vector3.right, Vector3.right), Is.LessThan(0.01f));
        }

        [Test]
        public void SpunUp_TurnsAtFullSpeed()
        {
            _helicopter.GetDriver().Value = new Entity();
            _rotors.Tick(_helicopter, SPIN_UP);

            Assert.That(MainStep(0.01f), Is.EqualTo(MAIN_SPEED * 0.01f).Within(0.01f));
        }

        [Test]
        public void InTheAirWithNobodyInside_KeepsSpinning()
        {
            _helicopter.GetFlightState().Value = HelicopterFlightState.Landing;

            _rotors.Tick(_helicopter, SPIN_UP);

            Assert.That(MainStep(0.01f), Is.EqualTo(MAIN_SPEED * 0.01f).Within(0.01f));
        }

        [Test]
        public void LeftEmptyOnTheGround_WindsDownThenStops()
        {
            _helicopter.GetDriver().Value = new Entity();
            _rotors.Tick(_helicopter, SPIN_UP);
            _helicopter.GetDriver().Value = null;

            // A quarter of the throttle goes in half a second of a two-second spin-down.
            Assert.That(MainStep(0.5f), Is.EqualTo(MAIN_SPEED * 0.75f * 0.5f).Within(0.01f));

            _rotors.Tick(_helicopter, SPIN_DOWN);
            Assert.That(MainStep(0.1f), Is.EqualTo(0f));
        }
    }
}
