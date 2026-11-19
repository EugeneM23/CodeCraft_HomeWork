using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class SkillOfferUseCaseTests
    {
        private static readonly string[] Pool = { "turret", "air_strike", "ricochet", "rate_of_fire", "armor", "magnet" };
        private static readonly bool[] IsWeapon = { true, true, false, false, false, false };

        private readonly List<SkillOffer> _offers = new();
        private readonly List<int> _scratch = new();

        [Test]
        public void Draw_NothingTaken_ThreeDistinctNewSkills()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                SkillOfferUseCase.Draw(new List<RunSkill>(), Pool, IsWeapon, 3, new System.Random(seed), _offers, _scratch);

                Assert.AreEqual(3, _offers.Count);
                var ids = new HashSet<string>();
                foreach (SkillOffer offer in _offers)
                {
                    Assert.IsTrue(ids.Add(offer.Id), $"{offer.Id} offered twice (seed {seed})");
                    Assert.AreEqual(0, offer.FromLevel);
                    Assert.AreEqual(1, offer.ToLevel);
                }
            }
        }

        [Test]
        public void Draw_TakenSkill_IsOfferedAtItsNextLevel()
        {
            var taken = new List<RunSkill> { new("ricochet", 2, false) };

            SkillOfferUseCase.Draw(taken, new[] { "ricochet" }, new[] { false }, 3, new System.Random(1), _offers, _scratch);

            Assert.AreEqual(1, _offers.Count);
            Assert.AreEqual(2, _offers[0].FromLevel);
            Assert.AreEqual(3, _offers[0].ToLevel);
        }

        [Test]
        public void Draw_SkillAtTopLevel_IsNotOffered()
        {
            var taken = new List<RunSkill> { new("armor", SkillOfferUseCase.MAX_LEVEL, false) };

            for (int seed = 0; seed < 50; seed++)
            {
                SkillOfferUseCase.Draw(taken, Pool, IsWeapon, 3, new System.Random(seed), _offers, _scratch);
                foreach (SkillOffer offer in _offers)
                    Assert.AreNotEqual("armor", offer.Id);
            }
        }

        [Test]
        public void Draw_FewerCandidatesThanCards_OffersWhatIsLeft()
        {
            var taken = new List<RunSkill>();
            foreach (string id in Pool)
                taken.Add(new RunSkill(id, SkillOfferUseCase.MAX_LEVEL, false));
            taken[0] = new RunSkill("turret", 4, true);

            SkillOfferUseCase.Draw(taken, Pool, IsWeapon, 3, new System.Random(3), _offers, _scratch);

            Assert.AreEqual(1, _offers.Count);
            Assert.AreEqual("turret", _offers[0].Id);
        }

        [Test]
        public void Draw_WeaponSlotsFull_NoNewWeapon()
        {
            var taken = new List<RunSkill>
            {
                new("w1", 1, true), new("w2", 1, true), new("w3", 1, true), new("w4", 1, true)
            };
            string[] pool = { "turret", "ricochet" };
            bool[] isWeapon = { true, false };

            SkillOfferUseCase.Draw(taken, pool, isWeapon, 3, new System.Random(5), _offers, _scratch);

            Assert.AreEqual(1, _offers.Count);
            Assert.AreEqual("ricochet", _offers[0].Id);
        }

        [Test]
        public void Apply_NewWeapon_GoesBeforeUpgrades()
        {
            var taken = new List<RunSkill> { new("turret", 1, true), new("armor", 1, false) };

            SkillOfferUseCase.Apply(taken, new SkillOffer("air_strike", 0, 1, true));

            Assert.AreEqual("air_strike", taken[1].Id);
            Assert.AreEqual("armor", taken[2].Id);
        }

        [Test]
        public void Apply_TakenSkill_RaisesItsLevelInPlace()
        {
            var taken = new List<RunSkill> { new("turret", 1, true), new("armor", 2, false) };

            SkillOfferUseCase.Apply(taken, new SkillOffer("armor", 2, 3, false));

            Assert.AreEqual(2, taken.Count);
            Assert.AreEqual(3, taken[1].Level);
        }
    }
}
