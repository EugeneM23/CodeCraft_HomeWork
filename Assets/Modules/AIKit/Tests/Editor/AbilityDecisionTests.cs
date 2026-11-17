using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The ability decisions (step 90, C6): Use Ability — one ability, ready for the Brain's target (range, its pause from
    /// its end, its pool), holding while it plays, its pause the graph's when the Timer pin is wired; Use Any Ability —
    /// its own set wired in, holding when one is ready and drawing nothing to answer, picking one by weight when taken
    /// (the agent's seed replays it) and keeping it; the decision hands its ability to its chain (a Use Ability block with
    /// none of its own uses it) and takes it back when it is left. Each node says on itself what it does by itself.
    /// </summary>
    public sealed class AbilityDecisionTests
    {
        private const string TARGET = "Target";
        private const string TIMER = "Timer";
        private const float DT = 0.05f;

        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
            GraphRig.DestroyAll();
        }

        private AbilityAsset Ability(string name, float cooldown, float max = 5f, params AbilityPart[] more)
        {
            var parts = new List<AbilityPart>
            {
                new RangeCondition { Min = 0f, Max = max }, new CooldownCondition { Seconds = cooldown }, new SelfDelivery()
            };
            parts.AddRange(more);
            AbilityAsset ability = AbilityAsset.Create(name, parts.ToArray());
            ability.Timing = new AbilityTiming { WindUp = 0.1f, Recovery = 0.1f };
            ability.Queue.AtOnce = 0;
            _created.Add(ability);
            return ability;
        }

        // A character whose Brain thinks about Target (set by the test), with these decisions and their chains
        private static GraphRig Rig(BrainGraphBuilder g, int seed = 1234)
        {
            var rig = new GraphRig(seed) { ThinkInterval = 0f };
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            rig.SetGraph(g.Build());
            return rig;
        }

        private static Transform Target(GraphRig rig, Vector3 at)
        {
            Transform target = rig.CreateTargetAt(at);
            rig.Agent.Blackboard.Set(TARGET, target);
            return target;
        }

        private static bool Holds(ReasonerTask decision, GraphRig rig, Transform target, bool current, out string why) =>
            decision.Holds(new ReasonerQuery(rig.Context, TargetFacts.Of(rig.Context, target), current), out why);

        // ---------- Use Ability ----------

        [Test]
        public void UseAbility_ReadyInRange_Holds_OutOfRange_NotAndSaysWhy()
        {
            AbilityAsset bolt = Ability("Bolt", 2f, max: 5f);
            var decision = new UseAbilityReasoner { Ability = bolt };
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(decision), new UseAbilityTask());
            GraphRig rig = Rig(g);
            Transform target = Target(rig, new Vector3(3f, 0f, 0f));

            Assert.IsTrue(Holds(decision, rig, target, false, out _));
            rig.MoveTargetTo(new Vector3(8f, 0f, 0f));
            Assert.IsFalse(Holds(decision, rig, target, false, out string why));
            Assert.AreEqual("target 8.0 m, needs 0–5", why);
        }

        [Test]
        public void UseAbility_ItsOwnPause_FromItsEnd_ThenReadyAgain()
        {
            const float COOLDOWN = 1f;
            AbilityAsset bolt = Ability("Bolt", COOLDOWN);
            var decision = new UseAbilityReasoner { Ability = bolt };
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(decision), new UseAbilityTask());
            GraphRig rig = Rig(g);
            Transform target = Target(rig, new Vector3(2f, 0f, 0f));
            Assert.IsTrue(rig.StepUntil(() => rig.Agent.AbilityRunners[0].Uses == 1, 1f, DT), "Taken, its chain used it");

            Assert.IsTrue(rig.StepUntil(() => !rig.Abilities.IsRunning("Bolt"), 1f, DT));
            rig.Agent.IsThinking = false;                  // the Brain decides no more: only time passes
            Assert.IsFalse(Holds(decision, rig, target, false, out string why), "Resting");
            StringAssert.StartsWith("rests", why);

            rig.StepSeconds(COOLDOWN + DT, DT);
            Assert.IsTrue(Holds(decision, rig, target, false, out _), "Rested: ready again");
        }

        [Test]
        public void UseAbility_WhileItPlays_TheTakenDecisionHolds()
        {
            AbilityAsset slam = Ability("Slam", 5f);
            slam.Timing = new AbilityTiming { WindUp = 1f, Recovery = 1f };
            var decision = new UseAbilityReasoner { Ability = slam };
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(decision), new UseAbilityTask());
            GraphRig rig = Rig(g);
            Transform target = Target(rig, new Vector3(2f, 0f, 0f));
            Assert.IsTrue(rig.StepUntil(() => rig.Abilities.IsRunning("Slam"), 1f, DT));
            rig.MoveTargetTo(new Vector3(30f, 0f, 0f));                       // out of its range now

            Assert.IsTrue(Holds(decision, rig, target, true, out _), "It plays: the decision stays taken");
            Assert.IsFalse(Holds(decision, rig, target, false, out _), "…not taken anew");
        }

        [Test]
        public void UseAbility_ItsPoolFull_TwoCharactersOneAtATime()
        {
            var world = new AIWorld();
            AbilityAsset grenade = Ability("Grenade", 0f);
            grenade.Queue = new AbilityQueue { AtOnce = 1 };
            grenade.Timing = new AbilityTiming { WindUp = 1f, Recovery = 1f };
            var first = new GraphRig(world: world) { ThinkInterval = 0f };
            var second = new GraphRig(world: world) { ThinkInterval = 0f };
            var decision = new UseAbilityReasoner { Ability = grenade };
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(decision), new UseAbilityTask());
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            BehaviorGraph graph = g.Build();
            first.SetGraph(graph);
            second.SetGraph(graph);
            Transform target = Target(first, new Vector3(2f, 0f, 0f));
            second.Context.Target = target;
            second.Agent.Blackboard.Set(TARGET, target);
            first.Agent.gameObject.SetActive(true);
            second.Agent.gameObject.SetActive(true);

            for (int i = 0; i < 20 && !first.Abilities.IsRunning("Grenade"); i++)
            {
                world.BeginStep(DT);
                first.Step(DT);
            }

            Assert.IsTrue(first.Abilities.IsRunning("Grenade"), "The first throws");
            world.BeginStep(DT);
            Assert.IsFalse(Holds(decision, second, target, false, out string why), "One at a time (At Once 1)");
            Assert.AreEqual("no free place to use Grenade", why);
        }

        [Test]
        public void UseAbility_TheDecisionHandsItsAbilityToTheChain_AndTakesItBackWhenLeft()
        {
            AbilityAsset slam = Ability("Slam", 5f);
            var decision = new UseAbilityReasoner { Ability = slam };
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(decision), new UseAbilityTask(), new WaitTask(0.3f));
            g.Chain(g.Always(), new WaitTask(100f));
            GraphRig rig = Rig(g);
            Target(rig, new Vector3(2f, 0f, 0f));

            Assert.IsTrue(rig.StepUntil(() => rig.Context.ChosenAbility == slam, 1f, DT), "Taken: its ability for the chain");
            Assert.IsTrue(rig.StepUntil(() => rig.Agent.AbilityRunners[0].Uses == 1, 1f, DT),
                "The chain's block with no ability of its own used the decision's");
            Assert.IsTrue(rig.StepUntil(() => rig.Instance.Decision?.Reasoner is DailyRoutineReasoner, 2f, DT));
            Assert.IsNull(rig.Context.ChosenAbility, "Left: nothing handed any more");
        }

        [Test]
        public void UseAbility_SaysWhosePauseItWaitsFor()
        {
            AbilityAsset bolt = Ability("Bolt", 6f);

            Assert.AreEqual("pause: ability's 6 s", new UseAbilityReasoner { Ability = bolt }.Requirement);
            Assert.AreEqual("ability: decision's", new UseAbilityTask().Requirement);
            Assert.IsNull(new UseAbilityTask(bolt).Requirement, "Its own ability: nothing hidden to say");
        }

        // ---------- Use Any Ability ----------

        [Test]
        public void UseAnyAbility_HoldsOnlyForItsOwnSet()
        {
            AbilityAsset mine = Ability("Mine", 0f, max: 1f);
            AbilityAsset other = Ability("Other", 0f, max: 10f);
            var any = new UseAnyAbilityReasoner(mine);
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(any), new UseAbilityTask());
            g.Chain(g.Decision(new UseAbilityReasoner { Ability = other }), new UseAbilityTask());
            GraphRig rig = Rig(g);
            Transform target = Target(rig, new Vector3(5f, 0f, 0f));

            Assert.IsTrue(rig.Abilities.IsReady("Other", rig.Context), "Another ability of the character is ready…");
            Assert.IsFalse(Holds(any, rig, target, false, out string why), "…none of its own set is");
            StringAssert.Contains("Mine", why);

            rig.MoveTargetTo(new Vector3(0.5f, 0f, 0f));
            Assert.IsTrue(Holds(any, rig, target, false, out _));
        }

        [Test]
        public void UseAnyAbility_Holding_DrawsNothingFromTheAgentsRandom()
        {
            var any = new UseAnyAbilityReasoner(Ability("A", 0f), Ability("B", 0f));
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(any), new UseAbilityTask());
            GraphRig rig = Rig(g);
            Transform target = Target(rig, new Vector3(1f, 0f, 0f));
            ulong before = rig.Context.RandomState;

            for (int i = 0; i < 10; i++)
                Holds(any, rig, target, false, out _);

            Assert.AreEqual(before, rig.Context.RandomState, "A question makes no state (invariant 6)");
        }

        [Test]
        public void UseAnyAbility_Taken_PicksByWeight_TheSameSeedTheSamePicks()
        {
            List<string> Picks(int seed)
            {
                AbilityAsset light = Ability("Light", 0f);
                AbilityAsset heavy = Ability("Heavy", 0f);
                heavy.Queue.Weight = 4f;
                var any = new UseAnyAbilityReasoner(light, heavy);
                var g = new BrainGraphBuilder();
                g.Chain(g.Decision(any), new UseAbilityTask());
                GraphRig rig = Rig(g, seed);
                rig.Abilities.Strategy = new RandomWeightedSelection();
                Transform target = Target(rig, new Vector3(1f, 0f, 0f));
                var picks = new List<string>();
                for (int i = 0; i < 40; i++)
                {
                    any.Taken(rig.Context, TargetFacts.Of(rig.Context, target));
                    picks.Add(any.PickOf(rig.Context).Name);
                }

                return picks;
            }

            List<string> first = Picks(99);
            CollectionAssert.AreEqual(first, Picks(99), "The agent's seed replays the picks");
            Assert.Greater(first.FindAll(pick => pick == "Heavy").Count, first.FindAll(pick => pick == "Light").Count * 2,
                "The heavier one comes up far more often");
            Assert.Greater(first.FindAll(pick => pick == "Light").Count, 0, "…not always: a roulette, not the maximum");
        }

        [Test]
        public void UseAnyAbility_Taken_HandsItsPickToTheChain_KeepsIt_ClearedWhenLeft()
        {
            AbilityAsset first = Ability("First", 1f);
            AbilityAsset second = Ability("Second", 1f);
            var any = new UseAnyAbilityReasoner(first, second);
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(any), new UseAbilityTask(), new WaitTask(0.2f));
            g.Chain(g.Always(), new WaitTask(100f));
            GraphRig rig = Rig(g);
            Target(rig, new Vector3(1f, 0f, 0f));

            Assert.IsTrue(rig.StepUntil(() => rig.Context.ChosenAbility != null, 1f, DT));
            AbilityAsset pick = rig.Context.ChosenAbility;
            Assert.AreSame(pick, any.PickOf(rig.Context));
            Assert.IsTrue(rig.StepUntil(() => rig.Abilities.IsRunning(pick.Name), 1f, DT), "The chain used its pick");
            Assert.AreSame(pick, rig.Context.ChosenAbility, "Kept while in charge");

            rig.Agent.Blackboard.Set(TARGET, (Transform)null);                 // the target gone: the decision lets go
            Assert.IsTrue(rig.StepUntil(() => rig.Instance.Decision?.Reasoner is DailyRoutineReasoner, 3f, DT));
            Assert.IsNull(rig.Context.ChosenAbility, "Left: its pick is not handed on");
            Assert.IsNull(any.PickOf(rig.Context));
        }

        [Test]
        public void UseAnyAbility_NothingWiredIn_NeverHolds_AndTheGraphSaysSo()
        {
            var any = new UseAnyAbilityReasoner();
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(any), new UseAbilityTask());
            GraphRig rig = Rig(g);
            Transform target = Target(rig, new Vector3(1f, 0f, 0f));

            Assert.IsFalse(Holds(any, rig, target, false, out string why));
            Assert.AreEqual("no abilities wired in", why);
            var issues = new List<GraphIssue>();
            AbilityGraphChecks.Check(rig.Instance.Graph, issues);
            Assert.IsTrue(issues.Exists(issue => issue.Code == GraphIssueCode.EmptyAbilitySet));
        }
    }
}
