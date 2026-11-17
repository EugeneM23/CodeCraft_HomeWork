using NUnit.Framework;

namespace Game.Tests
{
    public sealed class FootPlantTests
    {
        private const float DT = 0.02f;

        private static int Steps(FootPlant foot, params float[] heights)
        {
            int steps = 0;
            foreach (float h in heights)
                if (foot.Step(h, DT))
                    steps++;
            return steps;
        }

        [Test]
        public void StandingStill_NoStep()
        {
            FootPlant foot = new(1f);
            Assert.That(Steps(foot, 0.10f, 0.10f, 0.10f, 0.10f), Is.EqualTo(0));
        }

        [Test]
        public void Jitter_NoStep()
        {
            FootPlant foot = new(1f);
            Assert.That(Steps(foot, 0.10f, 0.11f, 0.095f, 0.12f, 0.10f, 0.115f), Is.EqualTo(0));
        }

        [Test]
        public void LiftAndPlant_OneStep()
        {
            FootPlant foot = new(1f);
            Assert.That(Steps(foot, 0.10f, 0.15f, 0.22f, 0.25f, 0.18f, 0.12f, 0.10f, 0.10f), Is.EqualTo(1));
        }

        [Test]
        public void TwoStrides_TwoSteps()
        {
            FootPlant foot = new(1f);
            Assert.That(Steps(foot,
                0.10f, 0.20f, 0.25f, 0.11f, 0.10f,
                0.20f, 0.25f, 0.11f, 0.10f), Is.EqualTo(2));
        }

        [Test]
        public void BiggerBody_NeedsABiggerLift()
        {
            FootPlant foot = new(3f);
            // 8 cm is a step for a person, a twitch for a body three times the size.
            Assert.That(Steps(foot, 0.30f, 0.38f, 0.30f), Is.EqualTo(0));
            Assert.That(Steps(foot, 0.30f, 0.60f, 0.31f), Is.EqualTo(1));
        }
    }
}
