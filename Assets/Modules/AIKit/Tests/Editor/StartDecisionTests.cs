using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace AIKit.Tests
{
    /// <summary>
    /// The Start decision (step 92; the user's word: "a reasoner that works as a start — for a primitive AI of nodes in a
    /// line"): taken the first time the Brain can take it, its line runs once; once the Brain lets go of it — the line
    /// ended, a more important decision cut it short, the graph stopped — it is never taken again in this life of the
    /// character. A behavior given anew (the agent's memory starts from nothing) starts it again.
    /// </summary>
    public sealed class StartDecisionTests
    {
        private const string ALARM = "Alarm";
        private const float DT = 0.05f;

        private readonly List<(string Name, float Time)> _entered = new();

        [SetUp]
        public void SetUp() => _entered.Clear();

        [TearDown]
        public void TearDown() => GraphRig.DestroyAll();

        [Test]
        public void Start_AtTheStart_TheBrainTakesIt_ItsLineRunsInOrderOnce_ThenTheCharacterStands()
        {
            var g = new BrainGraphBuilder();
            AgentState start = g.Decision(new StartReasoner(), 1, "Start");
            AgentState first = g.Block(new WaitTask(0.3f), "First");
            g.Do(start, first);
            g.Then(first, g.Block(new WaitTask(0.3f), "Second"));
            GraphRig rig = Rig(g.Build());

            rig.StepSeconds(3f, DT);

            CollectionAssert.AreEqual(new[] { "Start", "First", "Second" },
                Names().Where(name => name is "Start" or "First" or "Second").ToList(), Path());
            Assert.IsNull(rig.Instance.Decision, "The line ran once: nothing holds — " + Path());
            Assert.AreSame(rig.Instance.Graph.Brain, rig.Instance.Current, "The character stands in the Brain");
        }

        [Test]
        public void Start_CutShortByAMoreImportantDecision_IsSpent_TheRestOfItsLineNeverRuns()
        {
            var g = new BrainGraphBuilder();
            AgentState alarm = g.Decision(new FlagReasoner { Flag = ALARM }, 1, "Alarm");
            g.Do(alarm, g.Block(new WaitTask(0.2f), "Alarm Block"));
            AgentState start = g.Decision(new StartReasoner(), 2, "Start");
            AgentState first = g.Block(new WaitTask(1f), "First");
            g.Do(start, first);
            g.Then(first, g.Block(new WaitTask(1f), "Second"));
            GraphRig rig = Rig(g.Build());

            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "First", 1f, DT), Path());
            rig.StepSeconds(BehaviorGraphInstance.MIN_DECISION_TIME + 0.1f, DT);
            rig.Context.Blackboard.Set(ALARM, true);
            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "Alarm Block", 1f, DT), Path());
            rig.Context.Blackboard.Set(ALARM, false);
            rig.StepSeconds(3f, DT);

            Assert.IsFalse(Names().Contains("Second"), "The rest of Start's line never runs: " + Path());
            Assert.AreEqual(1, Names().Count(name => name == "Start"), "Taken once: " + Path());
            Assert.IsNull(rig.Instance.Decision, "Nothing holds after the alarm: " + Path());
        }

        [Test]
        public void Start_AMoreImportantDecisionHoldsAtTheStart_StartWaitsItsTurn_ThenRunsItsLineOnce()
        {
            var g = new BrainGraphBuilder();
            AgentState alarm = g.Decision(new FlagReasoner { Flag = ALARM }, 1, "Alarm");
            g.Do(alarm, g.Block(new WaitTask(100f), "Alarm Block"));
            AgentState start = g.Decision(new StartReasoner(), 2, "Start");
            g.Do(start, g.Block(new WaitTask(0.3f), "First"));
            GraphRig rig = Rig(g.Build());
            rig.Context.Blackboard.Set(ALARM, true);

            rig.StepSeconds(1f, DT);
            Assert.AreEqual("Alarm Block", rig.StateName, Path());
            Assert.IsFalse(Names().Contains("Start"), "Not taken while the alarm holds: " + Path());

            rig.Context.Blackboard.Set(ALARM, false);
            rig.StepSeconds(3f, DT);

            Assert.AreEqual(1, Names().Count(name => name == "First"), "Its line ran once, when its turn came: " + Path());
            Assert.IsNull(rig.Instance.Decision, Path());
        }

        [Test]
        public void Start_TheGraphStoppedAndStartedAgain_StartIsNotTakenAgain()
        {
            GraphRig rig = Rig(EndlessLine());
            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "First", 1f, DT), Path());

            rig.Instance.Stop(rig.Context);         // what switching the character off does (AICharacter.OnDisable)
            rig.StepSeconds(1f, DT);                // on again: the graph starts anew in its Brain

            Assert.AreEqual(1, Names().Count(name => name == "Start"), Path());
            Assert.IsNull(rig.Instance.Decision, "Let go when the graph stopped: spent — " + Path());
        }

        [Test]
        public void Start_TheGraphGivenAnew_RunsItsLineAgain()
        {
            BehaviorGraph graph = EndlessLine();
            GraphRig rig = Rig(graph);
            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "First", 1f, DT), Path());

            rig.SetGraph(graph);                    // a new behavior: the character's memory starts from nothing
            Track(rig);
            rig.StepSeconds(1f, DT);

            Assert.AreEqual(2, Names().Count(name => name == "First"), Path());
            Assert.AreEqual("First", rig.StateName, Path());
        }

        [Test]
        public void Start_Spent_TheBrainSaysWhy()
        {
            var g = new BrainGraphBuilder();
            AgentState start = g.Decision(new StartReasoner(), 1, "Start");
            g.Do(start, g.Block(new WaitTask(0.2f), "First"));
            GraphRig rig = Rig(g.Build());
            rig.Context.ExplainsDecisions = true;   // as the graph window does for the agent it shows

            rig.StepSeconds(1f, DT);

            ReasonerVerdict verdict = CombatBrainTask.VerdictsOf(rig.Instance.Graph.Brain, rig.Context)
                .Single(each => each.State == start);
            Assert.IsFalse(verdict.Holds);
            Assert.AreEqual(StartReasoner.SPENT, verdict.WhyNot);
        }

        // ---------- helpers ----------

        // Start → Do → First, a block that runs on and on
        private static BehaviorGraph EndlessLine()
        {
            var g = new BrainGraphBuilder();
            g.Do(g.Decision(new StartReasoner(), 1, "Start"), g.Block(new WaitTask(100f), "First"));
            return g.Build();
        }

        private GraphRig Rig(BehaviorGraph graph)
        {
            var rig = new GraphRig(trace: false) { ThinkInterval = 0f };
            rig.SetGraph(graph);
            Track(rig);
            return rig;
        }

        private void Track(GraphRig rig) =>
            rig.Instance.StateChanged += (_, to) =>
            {
                if (to != null)
                    _entered.Add((to.DisplayName, rig.Context.LocalTime));
            };

        private List<string> Names() => _entered.Select(entry => entry.Name).ToList();

        private string Path() => string.Join(" → ", _entered.Select(entry => $"{entry.Name}@{entry.Time:0.00}"));

        /// <summary>A decision that holds while a flag of the blackboard is on (it needs no target).</summary>
        private sealed class FlagReasoner : ReasonerTask
        {
            public string Flag;

            public override bool NeedsTarget => false;

            public override bool Holds(in ReasonerQuery query, out string whyNot)
            {
                bool on = query.Context != null && query.Context.Blackboard.Get(Flag, false);
                whyNot = on ? null : "the flag is off";
                return on;
            }
        }
    }
}
