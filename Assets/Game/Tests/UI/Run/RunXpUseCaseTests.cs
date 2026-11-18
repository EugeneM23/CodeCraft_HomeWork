using NUnit.Framework;

namespace Game.Tests
{
    public sealed class RunXpUseCaseTests
    {
        [TestCase(1, 10)]
        [TestCase(2, 22)]
        [TestCase(5, 58)]
        [TestCase(0, 10)]
        public void ToNextLevel_FollowsTheGddCurve(int level, int expected) =>
            Assert.AreEqual(expected, RunXpUseCase.ToNextLevel(level));

        [Test]
        public void Add_BelowTheThreshold_KeepsTheLevel()
        {
            int level = 1, xp = 0;

            int levels = RunXpUseCase.Add(ref level, ref xp, 9);

            Assert.AreEqual(0, levels);
            Assert.AreEqual(1, level);
            Assert.AreEqual(9, xp);
        }

        [Test]
        public void Add_ExactlyTheThreshold_LevelsUpWithZeroLeft()
        {
            int level = 1, xp = 4;

            int levels = RunXpUseCase.Add(ref level, ref xp, 6);

            Assert.AreEqual(1, levels);
            Assert.AreEqual(2, level);
            Assert.AreEqual(0, xp);
        }

        [Test]
        public void Add_ALot_GivesSeveralLevelsAndKeepsTheRemainder()
        {
            int level = 1, xp = 0;

            int levels = RunXpUseCase.Add(ref level, ref xp, 10 + 22 + 5);

            Assert.AreEqual(2, levels);
            Assert.AreEqual(3, level);
            Assert.AreEqual(5, xp);
        }

        [Test]
        public void Add_NothingOrNegative_ChangesNothing()
        {
            int level = 2, xp = 3;

            Assert.AreEqual(0, RunXpUseCase.Add(ref level, ref xp, 0));
            Assert.AreEqual(0, RunXpUseCase.Add(ref level, ref xp, -5));
            Assert.AreEqual(2, level);
            Assert.AreEqual(3, xp);
        }
    }
}
