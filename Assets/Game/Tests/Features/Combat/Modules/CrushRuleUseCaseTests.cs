using NUnit.Framework;

namespace Game.Tests
{
    public sealed class CrushRuleUseCaseTests
    {
        [Test]
        public void AHordeMonster_IsCrushedAtTheThresholdSpeedAndAbove()
        {
            Assert.That(CrushRuleUseCase.CanCrush(1.5f, 1.5f, 1f), Is.True);
            Assert.That(CrushRuleUseCase.CanCrush(6f, 1.5f, 1f), Is.True);
            Assert.That(CrushRuleUseCase.CanCrush(1.49f, 1.5f, 1f), Is.False);
        }

        [Test]
        public void WithoutAMark_AnyHealthIsCrushable()
        {
            Assert.That(CrushRuleUseCase.CanCrush(3f, 1.5f, 1f, CrushRuleUseCase.NO_MARK), Is.True);
            Assert.That(CrushRuleUseCase.CanCrush(3f, 1.5f, 0.6f), Is.True);
        }

        [TestCase(1f, false)]
        [TestCase(0.26f, false)]
        [TestCase(0.25f, true)]
        [TestCase(0.1f, true)]
        public void TheElite_IsCrushedOnlyAtOrBelowItsMark(float healthPercent, bool crushed)
        {
            Assert.That(CrushRuleUseCase.CanCrush(3f, 1.5f, healthPercent, 0.25f), Is.EqualTo(crushed));
        }

        [Test]
        public void TheElite_BelowItsMark_StillNeedsTheSpeed()
        {
            Assert.That(CrushRuleUseCase.CanCrush(1f, 1.5f, 0.1f, 0.25f), Is.False);
        }
    }
}
