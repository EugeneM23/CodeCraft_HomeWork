using NUnit.Framework;

namespace Game.Tests
{
    public sealed class CountUpUseCaseTests
    {
        [Test]
        public void Value_AtStart_IsZero() => Assert.AreEqual(0, CountUpUseCase.Value(500, 0f, 1f));

        [Test]
        public void Value_AtAndAfterTheEnd_IsTheTarget()
        {
            Assert.AreEqual(500, CountUpUseCase.Value(500, 1f, 1f));
            Assert.AreEqual(500, CountUpUseCase.Value(500, 3f, 1f));
        }

        [Test]
        public void Value_EasesOut_MoreThanLinearAtHalfTime()
        {
            int half = CountUpUseCase.Value(1000, 0.5f, 1f);

            Assert.Greater(half, 500);
            Assert.Less(half, 1000);
        }

        [Test]
        public void Value_NeverDecreasesAndNeverPassesTheTarget()
        {
            int last = 0;
            for (int i = 0; i <= 100; i++)
            {
                int value = CountUpUseCase.Value(37, i / 100f, 1f);
                Assert.GreaterOrEqual(value, last);
                Assert.LessOrEqual(value, 37);
                last = value;
            }
            Assert.AreEqual(37, last);
        }

        [TestCase(0)]
        [TestCase(-4)]
        public void Value_NothingToCount_IsZero(int target) => Assert.AreEqual(0, CountUpUseCase.Value(target, 0.5f, 1f));

        [Test]
        public void Value_NoDuration_IsTheTargetAtOnce() => Assert.AreEqual(12, CountUpUseCase.Value(12, 0f, 0f));
    }
}
