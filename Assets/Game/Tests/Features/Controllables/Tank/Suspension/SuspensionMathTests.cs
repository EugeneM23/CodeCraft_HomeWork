using NUnit.Framework;

namespace Game.Tests
{
    public sealed class SuspensionMathTests
    {
        private const float MASS = 3000f;
        private const float GRAVITY = 9.81f;
        private const int WHEELS = 10;
        private const float MAX_LENGTH = 0.4f;
        private const float MIN_LENGTH = 0.2f;
        private const float SAG = 0.1f;
        private const float STEP = 0.02f;

        private static readonly float Stiffness = SuspensionMath.Stiffness(MASS, GRAVITY, WHEELS, SAG);
        private static readonly float Damping = SuspensionMath.Damping(Stiffness, MASS, WHEELS, 0.4f);
        private static readonly float LoadPerWheel = MASS * GRAVITY / WHEELS;

        private static float Force(float length, float previousLength) =>
            SuspensionMath.Force(length, previousLength, STEP, MAX_LENGTH, MIN_LENGTH, Stiffness, Damping);

        [Test]
        public void Stiffness_HoldsTheLoadAtTheSag()
        {
            Assert.That(Stiffness * SAG, Is.EqualTo(LoadPerWheel).Within(1e-2f));
        }

        [Test]
        public void FullyExtended_NoForce()
        {
            Assert.That(Force(MAX_LENGTH, MAX_LENGTH), Is.EqualTo(0f));
            Assert.That(Force(MAX_LENGTH + 0.1f, MAX_LENGTH), Is.EqualTo(0f));
        }

        [Test]
        public void AtRest_CarriesItsShareOfTheWeight()
        {
            float rest = MAX_LENGTH - SAG;

            Assert.That(Force(rest, rest), Is.EqualTo(LoadPerWheel).Within(1e-2f));
        }

        [Test]
        public void MoreCompressed_PushesHarder()
        {
            float rest = MAX_LENGTH - SAG;

            Assert.That(Force(rest - 0.05f, rest - 0.05f), Is.GreaterThan(Force(rest, rest)));
        }

        [Test]
        public void Damper_ResistsBothWays_NeverPulls()
        {
            float rest = MAX_LENGTH - SAG;
            float still = Force(rest, rest);

            Assert.That(Force(rest, rest + 0.01f), Is.GreaterThan(still), "compressing");
            Assert.That(Force(rest, rest - 0.01f), Is.LessThan(still), "extending");
            Assert.That(Force(MAX_LENGTH - 0.001f, MIN_LENGTH), Is.EqualTo(0f), "extending fast must not pull");
        }

        [Test]
        public void PastTheBumpStop_StiffensSharply()
        {
            float atStop = Force(MIN_LENGTH, MIN_LENGTH);
            float past = Force(MIN_LENGTH - 0.02f, MIN_LENGTH - 0.02f);
            float springOnly = Stiffness * 0.02f;

            Assert.That(past - atStop, Is.GreaterThan(springOnly * 5f));
        }
    }
}
