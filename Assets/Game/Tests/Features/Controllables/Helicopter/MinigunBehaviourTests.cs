using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MinigunBehaviourTests
    {
        private const int BARRELS = 8;
        private const float MAX_SPIN = 720f;   // 2 turns a second: 16 rounds a second at full speed
        private const float SPIN_UP = 1f;
        private const float SPIN_DOWN = 2f;
        private const float FIRE_THROTTLE = 0.5f;
        private const float DT = 0.01f;

        private GameObject _barrels;
        private IEntity _helicopter;
        private int _rounds;
        private MinigunBehaviour _minigun;

        [SetUp]
        public void SetUp()
        {
            _barrels = new GameObject("Barrels");
            _helicopter = new Entity();
            _rounds = 0;

            IEntity gun = new Entity();
            gun.AddFireAction(new InlineAction(() => _rounds++));

            _minigun = new MinigunBehaviour(_barrels.transform, new[] { gun }, BARRELS, MAX_SPIN, SPIN_UP, SPIN_DOWN,
                FIRE_THROTTLE);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_barrels);

        private void Hold(float seconds)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += DT)
            {
                _minigun.Pull();
                _minigun.Tick(_helicopter, DT);
            }
        }

        private void Release(float seconds)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += DT)
                _minigun.Tick(_helicopter, DT);
        }

        [Test]
        public void Idle_StandsStill_AndFiresNothing()
        {
            Release(1f);

            Assert.That(_minigun.Throttle, Is.EqualTo(0f));
            Assert.That(_barrels.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(_rounds, Is.Zero);
        }

        [Test]
        public void TriggerHeld_SpinsAboutForward_BeforeFiring()
        {
            Hold(0.3f);

            Assert.That(_minigun.Throttle, Is.EqualTo(0.3f).Within(1e-3f));
            Assert.That(_rounds, Is.Zero, "below the fire throttle it only spins");

            Vector3 axis = _barrels.transform.localRotation * Vector3.forward;
            Assert.That(Vector3.Angle(axis, Vector3.forward), Is.LessThan(0.01f), "turns about its own forward");
            Assert.That(Quaternion.Angle(_barrels.transform.localRotation, Quaternion.identity), Is.GreaterThan(1f));
        }

        [Test]
        public void ReachingFireThrottle_FiresAtOnce()
        {
            // Crosses 0.5 at about 0.5 s; the next 5 ticks turn the barrels 18°, short of the next one.
            Hold(0.55f);

            Assert.That(_rounds, Is.EqualTo(1));
        }

        [Test]
        public void FullSpeed_FiresOneRoundPerBarrelComingRound()
        {
            Hold(SPIN_UP);
            _rounds = 0;

            Hold(2f);

            float expected = MAX_SPIN / (360f / BARRELS) * 2f;
            Assert.That(_rounds, Is.EqualTo(expected).Within(1));
        }

        [Test]
        public void Released_StopsFiringAtOnce_AndWindsDown()
        {
            Hold(SPIN_UP);
            _rounds = 0;

            Release(1f);

            Assert.That(_rounds, Is.Zero);
            Assert.That(_minigun.Throttle, Is.EqualTo(0.5f).Within(1e-3f));

            Release(1.1f);
            Assert.That(_minigun.Throttle, Is.EqualTo(0f));
        }

        [Test]
        public void LongFrame_FiresNoMoreThanTheCap()
        {
            Hold(SPIN_UP);
            _rounds = 0;

            _minigun.Pull();
            _minigun.Tick(_helicopter, 5f);

            Assert.That(_rounds, Is.EqualTo(8));
        }
    }
}
