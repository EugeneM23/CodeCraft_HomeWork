using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class SkillOfferQueueTests
    {
        private static readonly SkillOffer[] None = new SkillOffer[0];

        private readonly List<SkillOffer> _offer = new()
        {
            new SkillOffer("turret", 0, 1, true),
            new SkillOffer("armor", 0, 1, false)
        };

        [Test]
        public void Arrive_OnTheBareHud_ShowsNow_AndKeepsNothing()
        {
            var queue = new SkillOfferQueue();

            Assert.IsTrue(queue.Arrive(_offer, true));
            Assert.IsFalse(queue.HasWaiting);
        }

        [Test]
        public void Arrive_Covered_Waits_ThenIsTakenOnce()
        {
            var queue = new SkillOfferQueue();

            Assert.IsFalse(queue.Arrive(_offer, false));
            Assert.IsTrue(queue.HasWaiting);

            Assert.IsTrue(queue.TryTake(_offer, out IReadOnlyList<SkillOffer> shown));
            Assert.AreSame(_offer, shown);
            Assert.IsFalse(queue.HasWaiting);
            Assert.IsFalse(queue.TryTake(_offer, out _), "a taken offer is not shown twice");
        }

        // The second level-up is raised from inside ChooseSkill while the first choice is still on top.
        [Test]
        public void SecondLevelUp_DuringTheChoice_IsShownAfterThePop()
        {
            var queue = new SkillOfferQueue();
            Assert.IsTrue(queue.Arrive(_offer, true));     // first level-up: shown

            Assert.IsFalse(queue.Arrive(_offer, false));   // second, from inside ChooseSkill: waits

            Assert.IsTrue(queue.TryTake(_offer, out IReadOnlyList<SkillOffer> next)); // the choice popped
            Assert.AreEqual(2, next.Count);
        }

        [Test]
        public void TryTake_TheSourcesPendingOfferWins()
        {
            var queue = new SkillOfferQueue();
            var older = new List<SkillOffer> { new("magnet", 0, 1, false) };
            queue.Arrive(older, false);

            Assert.IsTrue(queue.TryTake(_offer, out IReadOnlyList<SkillOffer> shown));
            Assert.AreSame(_offer, shown);
        }

        [Test]
        public void TryTake_NothingPendingAnyMore_DropsTheWaitingOffer()
        {
            var queue = new SkillOfferQueue();
            queue.Arrive(_offer, false);

            Assert.IsFalse(queue.TryTake(None, out IReadOnlyList<SkillOffer> shown));
            Assert.IsNull(shown);
            Assert.IsFalse(queue.HasWaiting);
        }

        [Test]
        public void TryTake_NothingWaited_ShowsNothing_EvenWithAPendingOffer()
        {
            var queue = new SkillOfferQueue();

            Assert.IsFalse(queue.TryTake(_offer, out _));
        }

        [Test]
        public void Arrive_EmptyOrNull_IsIgnored()
        {
            var queue = new SkillOfferQueue();

            Assert.IsFalse(queue.Arrive(None, true));
            Assert.IsFalse(queue.Arrive(null, false));
            Assert.IsFalse(queue.HasWaiting);
        }

        [Test]
        public void Clear_ForgetsTheWaitingOffer()
        {
            var queue = new SkillOfferQueue();
            queue.Arrive(_offer, false);

            queue.Clear();

            Assert.IsFalse(queue.HasWaiting);
            Assert.IsFalse(queue.TryTake(_offer, out _));
        }
    }
}
