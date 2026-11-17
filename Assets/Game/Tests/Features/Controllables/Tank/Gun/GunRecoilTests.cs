using NUnit.Framework;

namespace Game.Tests
{
    public sealed class GunRecoilTests
    {
        private const float DISTANCE = 0.6f;
        private const float KICK = 0.05f;
        private const float HOLD = 0.12f;
        private const float RETURN = 0.8f;

        private const float RETURN_START = KICK + HOLD;

        private static float At(float elapsed) => GunRecoilBehaviour.Offset(elapsed, DISTANCE, KICK, HOLD, RETURN);

        [Test]
        public void AtTheShot_InBattery()
        {
            Assert.That(At(0f), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void AfterTheKick_FullyBack()
        {
            Assert.That(At(KICK), Is.EqualTo(-DISTANCE).Within(1e-5f));
        }

        [Test]
        public void ThroughTheHold_StaysFullyBack()
        {
            Assert.That(At(KICK + HOLD * 0.5f), Is.EqualTo(-DISTANCE).Within(1e-5f));
            Assert.That(At(RETURN_START), Is.EqualTo(-DISTANCE).Within(1e-5f));
        }

        [Test]
        public void Return_EasesOutOfTheBackAndIntoBattery()
        {
            float firstQuarter = At(RETURN_START + RETURN * 0.25f) - At(RETURN_START);
            float middleHalf = At(RETURN_START + RETURN * 0.75f) - At(RETURN_START + RETURN * 0.25f);
            float lastQuarter = At(RETURN_START + RETURN) - At(RETURN_START + RETURN * 0.75f);

            Assert.That(middleHalf, Is.GreaterThan(firstQuarter * 2f));
            Assert.That(middleHalf, Is.GreaterThan(lastQuarter * 2f));
            Assert.That(At(RETURN_START + RETURN * 0.5f), Is.EqualTo(-DISTANCE * 0.5f).Within(1e-5f));
        }

        [Test]
        public void OnceReturned_StaysInBattery()
        {
            Assert.That(At(RETURN_START + RETURN), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(At(5f), Is.EqualTo(0f).Within(1e-5f));
        }
    }
}
