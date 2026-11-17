using NUnit.Framework;

namespace Game.Tests
{
    public sealed class ClockTextUseCaseTests
    {
        [TestCase(0f, "00:00")]
        [TestCase(59.99f, "00:59")]
        [TestCase(134.5f, "02:14")]
        [TestCase(-3f, "00:00")]
        [TestCase(100000f, "99:59")]
        public void Format(float seconds, string expected) =>
            Assert.AreEqual(expected, ClockTextUseCase.Format(seconds));

        [Test]
        public void Split_MinutesAndSeconds()
        {
            ClockTextUseCase.Split(134, out int minutes, out int seconds);

            Assert.AreEqual(2, minutes);
            Assert.AreEqual(14, seconds);
        }
    }
}
