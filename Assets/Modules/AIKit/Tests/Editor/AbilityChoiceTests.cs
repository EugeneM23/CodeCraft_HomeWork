using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The choice among attacks as a game's weighted attack chooser needs it: conditions asked on every call, zero weight never chosen, cooldowns, shares that follow
    /// the weights, a penalty for the previous choice, the same seed — the same sequence. Strategy — the weighted
    /// roulette among all options.
    /// </summary>
    public class AbilityChoiceTests
    {
        private GraphRig _rig;

        [SetUp]
        public void SetUp() => _rig = CreateRig(1);

        [TearDown]
        public void TearDown() => _rig.DestroyTarget();

        private static GraphRig CreateRig(int seed)
        {
            var rig = new GraphRig(seed, trace: false);
            rig.CreateTargetAt(new Vector3(1f, 0f, 0f));
            rig.Abilities.Strategy = new RandomWeightedSelection();
            return rig;
        }

        private static TestAbilityRunner Add(GraphRig rig, string id, float weight, float cooldown = 0f,
            float repeatPenalty = 0f)
        {
            rig.Abilities.Add(new AbilityDefinition
            {
                Id = id,
                Weight = weight,
                Cooldown = cooldown,
                MaxRange = 10f,
                RequiresGround = false,
                RepeatPenalty = repeatPenalty
            });

            var runner = new TestAbilityRunner(id);
            rig.Abilities.Register(runner);
            return runner;
        }

        /// <summary>Choose and use, draws times: the share of id among the choices.</summary>
        private static float ShareOf(GraphRig rig, string id, int draws)
        {
            int hits = 0;
            for (int i = 0; i < draws; i++)
            {
                string chosen = rig.Abilities.Select(rig.Context);
                Assert.IsNotNull(chosen);
                Assert.IsTrue(rig.Abilities.TryUse(chosen, rig.Context));
                if (chosen == id)
                    hits++;
            }

            return hits / (float)draws;
        }

        [Test]
        public void Select_NoAbilities_Null()
        {
            Assert.IsFalse(_rig.Abilities.AnyAvailable(_rig.Context));
            Assert.IsNull(_rig.Abilities.Select(_rig.Context));
        }

        [Test]
        public void Select_OnlyAbilityWhoseRunnerCanStart()
        {
            Add(_rig, "Combo", 1f).Blocked = true;
            Add(_rig, "Smash", 1f);

            Assert.AreEqual(1f, ShareOf(_rig, "Smash", 50));
        }

        [Test]
        public void Select_ZeroWeight_NeverChosen()
        {
            Add(_rig, "Combo", 0f);

            Assert.IsFalse(_rig.Abilities.AnyAvailable(_rig.Context));

            Add(_rig, "Smash", 1f);
            Assert.AreEqual(1f, ShareOf(_rig, "Smash", 50));
        }

        [Test]
        public void Conditions_AskedOnEveryCall()
        {
            TestAbilityRunner combo = Add(_rig, "Combo", 1f);
            combo.Blocked = true;

            Assert.IsFalse(_rig.Abilities.AnyAvailable(_rig.Context));
            combo.Blocked = false;
            Assert.IsTrue(_rig.Abilities.AnyAvailable(_rig.Context));
        }

        [Test]
        public void Use_StartsCooldown_TickEndsIt()
        {
            Add(_rig, "Combo", 1f, cooldown: 1f);

            Assert.IsTrue(_rig.Abilities.TryUse("Combo", _rig.Context));
            Assert.IsFalse(_rig.Abilities.AnyAvailable(_rig.Context));

            _rig.Tick(0.6f);
            Assert.IsFalse(_rig.Abilities.AnyAvailable(_rig.Context));
            Assert.AreEqual(0.4f, _rig.Abilities.CooldownLeft("Combo"), 1e-5f);

            _rig.Tick(0.6f);
            Assert.IsTrue(_rig.Abilities.AnyAvailable(_rig.Context));
            Assert.AreEqual(0f, _rig.Abilities.CooldownLeft("Combo"));
        }

        [Test]
        public void Distribution_FollowsWeights()
        {
            Add(_rig, "Combo", 3f);
            Add(_rig, "Smash", 1f);

            Assert.AreEqual(0.75f, ShareOf(_rig, "Combo", 4000), 0.03f);
        }

        [Test]
        public void RepeatPenalty_LowersShareOfPreviousChoice()
        {
            const float KEPT = 0.25f;                      // the previous choice keeps this share of its score
            Add(_rig, "Combo", 1f, repeatPenalty: 1f - KEPT);
            Add(_rig, "Smash", 1f, repeatPenalty: 1f - KEPT);
            Assert.IsTrue(_rig.Abilities.TryUse("Combo", _rig.Context));

            // Two equal options: whichever came last is chosen again with KEPT / (1 + KEPT)
            int repeats = 0;
            int draws = 4000;
            string previous = "Combo";
            for (int i = 0; i < draws; i++)
            {
                string chosen = _rig.Abilities.Select(_rig.Context);
                _rig.Abilities.TryUse(chosen, _rig.Context);
                if (chosen == previous)
                    repeats++;
                previous = chosen;
            }

            Assert.AreEqual(KEPT / (1f + KEPT), repeats / (float)draws, 0.03f);
        }

        [Test]
        public void SameSeed_SameSequence()
        {
            GraphRig first = CreateRig(9);
            GraphRig second = CreateRig(9);
            try
            {
                foreach (GraphRig rig in new[] { first, second })
                {
                    Add(rig, "Combo", 3f, repeatPenalty: 0.5f);
                    Add(rig, "AirSlash", 2f, repeatPenalty: 0.5f);
                    Add(rig, "Smash", 1f, repeatPenalty: 0.5f);
                }

                for (int i = 0; i < 50; i++)
                {
                    string a = first.Abilities.Select(first.Context);
                    string b = second.Abilities.Select(second.Context);
                    Assert.AreEqual(a, b);
                    first.Abilities.TryUse(a, first.Context);
                    second.Abilities.TryUse(b, second.Context);
                }
            }
            finally
            {
                first.DestroyTarget();
                second.DestroyTarget();
            }
        }

        [Test]
        public void Add_SameIdTwice_ReplacesTheDefinition()
        {
            // Replaces on purpose (not an error) — an archetype overrides a preset's ability
            Add(_rig, "Combo", 1f);
            Add(_rig, "Combo", 5f);

            Assert.AreEqual(5f, _rig.Abilities.DefinitionOf("Combo").Weight);
            Assert.IsTrue(_rig.Abilities.Has("Combo"));
        }

        [Test]
        public void Register_NullRunner_IsIgnored()
        {
            // The condition is the runner; a null one is skipped, not an error
            Assert.DoesNotThrow(() => _rig.Abilities.Register(null));
            Assert.IsNull(_rig.Abilities.Select(_rig.Context));
        }
    }
}
