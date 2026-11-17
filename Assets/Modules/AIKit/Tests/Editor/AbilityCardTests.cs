using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// An ability as an asset the graph names (AbilityAsset, step 90 — no list on the character): the agent takes its
    /// data from it, a canvas node uses it on the object of its variable, the blow lands when the swing is over, and the
    /// pause counts from the end.
    /// </summary>
    public sealed class AbilityCardTests
    {
        private const string VICTIM = "Victim";
        private const string THROW = "Throw";
        private const float STEP = 0.1f;

        // The ability of the tests: used from 3 to 8 metres; the times sit between two steps, so the summed dt cannot
        // shift a step
        private const float WIND_UP = 0.35f;
        private const float RECOVERY = 0.2f;
        private const float COOLDOWN = 1.05f;

        private GraphRig _rig;
        private GameObject _victim;
        private HitProbe _hits;
        private AbilityAsset _asset;

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig { ThinkInterval = 0f };
            _victim = new GameObject("Victim") { hideFlags = HideFlags.HideAndDontSave };
            _victim.transform.position = new Vector3(5f, 0f, 0f);
            _hits = _victim.AddComponent<HitProbe>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_victim);
            Object.DestroyImmediate(_asset);
            GraphRig.DestroyAll();
        }

        [Test]
        public void Card_NamedByTheGraph_GivesTheAgentItsAbility()
        {
            var g = new BrainGraphBuilder();
            AgentState routine = g.Always();
            g.Do(routine, g.Block(new UseAbilityTask(Asset()) { Target = VICTIM }, "Use"));
            g.Variable(VICTIM, VariableType.Transform);

            _rig.SetGraph(g.Build());

            Assert.IsTrue(_rig.Abilities.Has(THROW), "The ability the graph names is the agent's: no other list to fill");
            Assert.AreEqual(8f, _rig.Abilities.DefinitionOf(THROW).Range.Max, "…with the asset's own numbers");
            Assert.AreEqual(COOLDOWN, _rig.Abilities.DefinitionOf(THROW).Cooldown);
            Assert.AreEqual(1, _rig.Agent.AbilityRunners.Count, "The character's own runner of it");
        }

        [TestCase(5f, true)]
        [TestCase(1f, false)]       // nearer than its range
        [TestCase(12f, false)]      // farther
        public void Card_IsReady_ForAnObjectTheAgentIsPointedAt_InItsRange(float distance, bool expected)
        {
            Card();
            _victim.transform.position = new Vector3(distance, 0f, 0f);

            Assert.IsFalse(_rig.Abilities.IsReady(THROW, _rig.Context), "The agent tracks no target of its own");
            using (_rig.Context.AimAt(_victim.transform))
                Assert.AreEqual(expected, _rig.Abilities.IsReady(THROW, _rig.Context));

            Assert.IsFalse(_rig.Context.HasTarget, "The aim lasts for the ask only");
        }

        [Test]
        public void UseAbility_OnTheObjectOfItsVariable_StrikesWhenTheSwingIsOver_ThenGoesOn()
        {
            AbilityRunner card = Card();
            RunUseAbility();

            _rig.Step(STEP, 2);
            Assert.AreEqual("Use", _rig.StateName, "The swing takes time");
            Assert.AreEqual(1, card.Uses);
            Assert.AreEqual(AbilityPhase.WindUp, _rig.Abilities.PhaseOf(THROW));
            Assert.AreEqual(0, _hits.Hits, "No blow before the swing is over");

            Assert.IsTrue(_rig.StepUntil(() => _hits.Hits > 0, WIND_UP + 3 * STEP, STEP), "The blow lands after the swing");
            Assert.AreEqual(2f, _hits.LastDamage);
            Assert.AreEqual("Use", _rig.StateName, "The node waits out the pause after the blow");

            Assert.IsTrue(_rig.StepUntil(() => _rig.StateName == "After", RECOVERY + 4 * STEP, STEP),
                "The ability is over: the arrow from the node's end is taken");
            Assert.AreEqual(1, _hits.Hits, "One use of the node is one use of the ability");
        }

        [Test]
        public void UseAbility_TheTargetStepsAwayDuringTheSwing_TheBlowMisses()
        {
            Card();
            RunUseAbility();

            _rig.Step(STEP, 2);
            _victim.transform.position = new Vector3(20f, 0f, 0f);

            Assert.IsTrue(_rig.StepUntil(() => _rig.StateName == "After", 2f, STEP));
            Assert.AreEqual(0, _hits.Hits, "Out of reach when the swing landed");
        }

        [Test]
        public void UseAbility_WithNothingReady_IsDoneAtOnce()
        {
            Card();
            _victim.transform.position = new Vector3(40f, 0f, 0f);      // far out of the ability's range
            RunUseAbility();

            _rig.Step(STEP, 3);

            Assert.AreEqual("After", _rig.StateName, "Used or not, the arrow from the node's end leads on");
            Assert.AreEqual(0, _hits.Hits);
        }

        [Test]
        public void Card_IsNotReady_DuringItsUseAndThePause_ThenReadyAgain()
        {
            Card();
            RunUseAbility();
            Assert.IsTrue(IsReadyForTheVictim(), "In range and rested: ready");

            _rig.Step(STEP, 2);
            Assert.IsTrue(_rig.Abilities.IsRunning(THROW), "The node used it");
            Assert.IsFalse(IsReadyForTheVictim(), "It plays");

            Assert.IsTrue(_rig.StepUntil(() => _rig.StateName == "After", 2f, STEP));
            Assert.IsFalse(IsReadyForTheVictim(), "The pause after the ability has just started");

            _rig.Step(STEP, Mathf.CeilToInt(COOLDOWN / STEP) - 2);
            Assert.IsFalse(IsReadyForTheVictim(), "The pause counts from the END of the ability");

            _rig.Step(STEP, 4);
            Assert.IsTrue(IsReadyForTheVictim(), "Rested: ready again");
        }

        // The rig's agent gets the ability (its runner handed over as the character does for an ability its graph names)
        private AbilityRunner Card()
        {
            var card = new AbilityRunner(Asset(), _rig.Agent.transform);
            _rig.Abilities.Add(card.Definition);
            _rig.Abilities.Register(card);
            return card;
        }

        // Used from 3 to 8 metres, a blow that reaches 8 m for 2, rests COOLDOWN from its end
        private AbilityAsset Asset()
        {
            if (_asset != null)
                return _asset;

            _asset = AbilityAsset.Create(THROW, new RangeCondition { Min = 3f, Max = 8f },
                new CooldownCondition { Seconds = COOLDOWN }, new StrikeDelivery { Reach = 8f },
                new DamageEffect { Damage = 2f });
            _asset.Timing = new AbilityTiming { WindUp = WIND_UP, Recovery = RECOVERY };
            _asset.Queue.AtOnce = 0;
            return _asset;
        }

        // The card is ready for the object of the variable (the agent tracks no target of its own)
        private bool IsReadyForTheVictim()
        {
            using (_rig.Context.AimAt(_victim.transform))
                return _rig.Abilities.IsReady(THROW, _rig.Context);
        }

        // Daily Routine → Do → Use (Use Ability on the variable Victim) → its end, Used or Not Used → After. The Brain
        // takes the decision on its second think — the second step (it thinks every step)
        private void RunUseAbility()
        {
            var g = new BrainGraphBuilder();
            AgentState routine = g.Always();
            AgentState use = g.Block(new UseAbilityTask(Asset()) { Target = VICTIM }, "Use");
            g.Do(routine, use);
            g.OnEnd(use, g.Block(new WaitTask(100f), "After"));
            g.Variable(VICTIM, VariableType.Transform);
            _rig.SetGraph(g.Build());
            _rig.Agent.Blackboard.Set(VICTIM, _victim.transform);
        }
    }

    /// <summary>A target that counts the blows it takes.</summary>
    internal sealed class HitProbe : MonoBehaviour, IHitReceiver
    {
        public int Hits { get; private set; }

        public float LastDamage { get; private set; }

        public void ReceiveHit(in HitInfo hit)
        {
            Hits++;
            LastDamage = hit.Damage;
        }
    }
}
