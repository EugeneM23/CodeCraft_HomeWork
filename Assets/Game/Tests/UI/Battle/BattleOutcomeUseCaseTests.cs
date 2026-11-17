using NUnit.Framework;

namespace Game.Tests
{
    public sealed class BattleOutcomeUseCaseTests
    {
        [Test]
        public void Decide_BelowTarget_Ongoing() =>
            Assert.AreEqual(BattleOutcome.Ongoing, BattleOutcomeUseCase.Decide(3, 10, false));

        [Test]
        public void Decide_TargetReached_Victory() =>
            Assert.AreEqual(BattleOutcome.Victory, BattleOutcomeUseCase.Decide(10, 10, false));

        [Test]
        public void Decide_PastTarget_Victory() =>
            Assert.AreEqual(BattleOutcome.Victory, BattleOutcomeUseCase.Decide(12, 10, false));

        [Test]
        public void Decide_NoTarget_NeverVictory() =>
            Assert.AreEqual(BattleOutcome.Ongoing, BattleOutcomeUseCase.Decide(50, 0, false));

        [Test]
        public void Decide_PlayerDestroyed_Defeat() =>
            Assert.AreEqual(BattleOutcome.Defeat, BattleOutcomeUseCase.Decide(0, 10, true));

        [Test]
        public void Decide_LastKillAndDeathTogether_Defeat() =>
            Assert.AreEqual(BattleOutcome.Defeat, BattleOutcomeUseCase.Decide(10, 10, true));

        [Test]
        public void Reward_Victory_LevelReward() =>
            Assert.AreEqual(300, BattleOutcomeUseCase.Reward(BattleOutcome.Victory, 300));

        [Test]
        public void Reward_Defeat_Nothing() =>
            Assert.AreEqual(0, BattleOutcomeUseCase.Reward(BattleOutcome.Defeat, 300));

        [Test]
        public void Reward_NegativeLevelReward_Nothing() =>
            Assert.AreEqual(0, BattleOutcomeUseCase.Reward(BattleOutcome.Victory, -5));
    }
}
