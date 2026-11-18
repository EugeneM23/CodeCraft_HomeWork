using NUnit.Framework;

namespace Game.AudioEngine.Adapters.Tests
{
    public sealed class MusicIntensityTests
    {
        private const float EPSILON = 1e-4f;

        [Test]
        public void Smoothing_ApproachesTheTarget_WithoutOvershoot()
        {
            var intensity = new MusicIntensity();
            intensity.SetTarget(0.8f);

            float previous = intensity.Value;
            for (int i = 0; i < 600; i++)
            {
                intensity.Tick(1f / 60f);
                Assert.GreaterOrEqual(intensity.Value, previous);
                Assert.LessOrEqual(intensity.Value, 0.8f + EPSILON);
                previous = intensity.Value;
            }

            Assert.AreEqual(0.8f, intensity.Value, 0.01f);
        }

        [Test]
        public void Smoothing_AfterOneTimeConstant_CoversAboutTwoThirds()
        {
            var intensity = new MusicIntensity();
            intensity.SetTarget(1f);
            intensity.Tick(MusicIntensity.SMOOTHING_TIME);
            Assert.AreEqual(1f - UnityEngine.Mathf.Exp(-1f), intensity.Value, EPSILON);
        }

        [Test]
        public void Smoothing_IsFrameRateIndependent()
        {
            var once = new MusicIntensity();
            var often = new MusicIntensity();
            once.SetTarget(0.7f);
            often.SetTarget(0.7f);

            once.Tick(1f);
            for (int i = 0; i < 10; i++)
                often.Tick(0.1f);

            Assert.AreEqual(once.Value, often.Value, EPSILON);
        }

        [Test]
        public void Smoothing_GoesDown_WhenTheHordeThins()
        {
            var intensity = new MusicIntensity();
            intensity.SetTarget(1f);
            intensity.Tick(30f);
            intensity.SetTarget(0.2f);
            intensity.Tick(30f);
            Assert.AreEqual(0.2f, intensity.Value, 0.01f);
        }

        [Test]
        public void Surge_RaisesAtOnce_ThenDecaysBack()
        {
            var intensity = new MusicIntensity();
            intensity.SetTarget(0.2f);
            intensity.Tick(30f);
            float settled = intensity.Value;

            intensity.Surge();
            Assert.AreEqual(settled + MusicIntensity.SURGE, intensity.Value, EPSILON);

            intensity.Tick(MusicIntensity.SURGE_TIME * 0.5f);
            Assert.Greater(intensity.Value, settled);
            Assert.Less(intensity.Value, settled + MusicIntensity.SURGE);

            intensity.Tick(MusicIntensity.SURGE_TIME * 0.5f);
            Assert.AreEqual(settled, intensity.Value, EPSILON);
        }

        [Test]
        public void Surge_NeverPushesAboveOne()
        {
            var intensity = new MusicIntensity();
            intensity.SetTarget(1f);
            intensity.Tick(30f);
            intensity.Surge();
            Assert.AreEqual(1f, intensity.Value, EPSILON);
        }

        [Test]
        public void Forced_SmoothsTowardOne_AndBackWhenReleased()
        {
            var intensity = new MusicIntensity();
            intensity.SetTarget(0.1f);
            intensity.Forced = true;
            intensity.Tick(30f);
            Assert.AreEqual(1f, intensity.Value, 0.01f);

            intensity.Forced = false;
            intensity.Tick(30f);
            Assert.AreEqual(0.1f, intensity.Value, 0.01f);
        }

        [Test]
        public void Reset_StartsCalm()
        {
            var intensity = new MusicIntensity();
            intensity.SetTarget(1f);
            intensity.Forced = true;
            intensity.Tick(5f);
            intensity.Surge();

            intensity.Reset();

            Assert.AreEqual(0f, intensity.Value);
            Assert.AreEqual(0f, intensity.Target);
            Assert.IsFalse(intensity.Forced);
        }

        [Test]
        public void Target_IsClamped()
        {
            var intensity = new MusicIntensity();
            intensity.SetTarget(5f);
            Assert.AreEqual(1f, intensity.Target);
            intensity.SetTarget(-1f);
            Assert.AreEqual(0f, intensity.Target);
            intensity.SetTarget(float.NaN);
            Assert.AreEqual(0f, intensity.Target);
        }

        [TestCase(0f, 1f, 0f, 0f)]
        [TestCase(0.15f, 1f, 0f, 0f)]
        [TestCase(0.5f, 1f, 1f, 0f)]
        [TestCase(0.9f, 1f, 1f, 1f)]
        [TestCase(1f, 1f, 1f, 1f)]
        public void LayerWeights(float intensity, float calm, float horde, float peak)
        {
            Assert.AreEqual(calm, MusicIntensity.CalmWeight(intensity), EPSILON);
            Assert.AreEqual(horde, MusicIntensity.HordeWeight(intensity), EPSILON);
            Assert.AreEqual(peak, MusicIntensity.PeakWeight(intensity), EPSILON);
        }

        [Test]
        public void LayerWeights_RampBetweenTheirEnds()
        {
            float hordeMiddle = (MusicIntensity.HORDE_FROM + MusicIntensity.HORDE_TO) * 0.5f;
            float peakMiddle = (MusicIntensity.PEAK_FROM + MusicIntensity.PEAK_TO) * 0.5f;
            Assert.AreEqual(0.5f, MusicIntensity.HordeWeight(hordeMiddle), EPSILON);
            Assert.AreEqual(0.5f, MusicIntensity.PeakWeight(peakMiddle), EPSILON);
        }
    }
}
