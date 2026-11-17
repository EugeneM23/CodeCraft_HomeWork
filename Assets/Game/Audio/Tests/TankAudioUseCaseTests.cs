using NUnit.Framework;

namespace Game.AudioEngine.Adapters.Tests
{
    public sealed class TankAudioUseCaseTests
    {
        private const float MAX_SPEED = 10f;
        private const float EPSILON = 0.0001f;

        [Test]
        public void AtRest_TheEngineIdles() =>
            Assert.AreEqual(TankAudioUseCase.IDLE_RPM, TankAudioUseCase.TargetRpm(0f, MAX_SPEED, 0f, false), EPSILON);

        [Test]
        public void Rpm_RisesWithSpeed()
        {
            float slow = TankAudioUseCase.TargetRpm(2f, MAX_SPEED, 1f, false);
            float fast = TankAudioUseCase.TargetRpm(8f, MAX_SPEED, 1f, false);
            Assert.Greater(fast, slow);
        }

        [Test]
        public void Rpm_RisesWithThrottle_BeforeTheHullMoves()
        {
            float idle = TankAudioUseCase.TargetRpm(0f, MAX_SPEED, 0f, false);
            float gas = TankAudioUseCase.TargetRpm(0f, MAX_SPEED, 1f, false);
            Assert.Greater(gas, idle);
        }

        [Test]
        public void ReverseThrottleAndSpeed_CountTheSameAsForward() =>
            Assert.AreEqual(TankAudioUseCase.TargetRpm(5f, MAX_SPEED, 1f, false),
                TankAudioUseCase.TargetRpm(-5f, MAX_SPEED, -1f, false), EPSILON);

        [Test]
        public void TopSpeedWithoutBoost_StopsAtNormalMax() =>
            Assert.AreEqual(TankAudioUseCase.NORMAL_MAX_RPM, TankAudioUseCase.TargetRpm(MAX_SPEED, MAX_SPEED, 1f, false), EPSILON);

        [Test]
        public void Boost_RaisesTheRpm_ToTheTop()
        {
            float normal = TankAudioUseCase.TargetRpm(MAX_SPEED, MAX_SPEED, 1f, false);
            float boost = TankAudioUseCase.TargetRpm(MAX_SPEED, MAX_SPEED, 1f, true);
            Assert.Greater(boost, normal);
            Assert.AreEqual(1f, boost, EPSILON);
        }

        [Test]
        public void Rpm_IsClampedTo01_WhateverTheInput()
        {
            Assert.That(TankAudioUseCase.TargetRpm(1000f, MAX_SPEED, 50f, true), Is.InRange(0f, 1f));
            Assert.That(TankAudioUseCase.TargetRpm(5f, 0f, -50f, true), Is.InRange(0f, 1f));
            Assert.That(TankAudioUseCase.SmoothRpm(0.5f, 1f, 100f), Is.InRange(0f, 1f));
        }

        [Test]
        public void Smoothing_MovesTowardTheTarget_WithoutOvershoot()
        {
            float up = TankAudioUseCase.SmoothRpm(0.2f, 1f, 0.016f);
            Assert.Greater(up, 0.2f);
            Assert.Less(up, 1f);

            float down = TankAudioUseCase.SmoothRpm(1f, 0.2f, 0.016f);
            Assert.Less(down, 1f);
            Assert.Greater(down, 0.2f);
        }

        [Test]
        public void Smoothing_RisesFasterThanItFalls()
        {
            float rise = TankAudioUseCase.SmoothRpm(0.2f, 0.8f, 0.1f) - 0.2f;
            float fall = 0.8f - TankAudioUseCase.SmoothRpm(0.8f, 0.2f, 0.1f);
            Assert.Greater(rise, fall);
        }

        [Test]
        public void Smoothing_ReachesTheTarget_AfterLongEnough() =>
            Assert.AreEqual(0.7f, TankAudioUseCase.SmoothRpm(0.2f, 0.7f, 10f), 0.001f);
    }
}
