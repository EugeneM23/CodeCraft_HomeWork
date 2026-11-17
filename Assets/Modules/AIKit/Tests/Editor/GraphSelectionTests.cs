using System.Collections.Generic;
using NUnit.Framework;

namespace AIKit.Tests
{
    /// <summary>
    /// How the agent goes from node to node in the brain model: a block's outputs lead on by how its work ended, an arrow
    /// into the node the agent is in starts its block again, an arrow into the Brain or a decision ends the chain (the
    /// Brain chooses), a more important decision takes over from any block of a chain, and a block left that way is
    /// told why.
    /// </summary>
    public class GraphSelectionTests
    {
        private const float THINK = 0.1f;
        private const float DT = 0.02f;
        private const string ALARM = "Alarm";

        private readonly List<string> _entered = new();
        private GraphRig _rig;

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig { ThinkInterval = THINK };
            _entered.Clear();
        }

        /// <summary>A block that answers what the test sets, counts its starts and remembers how it was left.</summary>
        private sealed class ProbeTask : AgentTask
        {
            public TaskStatus Answer = TaskStatus.Running;
            public int Enters;
            public ExitReason? LastExit;

            protected override void OnEnter(AgentContext context) => Enters++;

            protected override TaskStatus OnTick(AgentContext context, float dt) => Answer;

            protected override void OnExit(AgentContext context, ExitReason reason) => LastExit = reason;
        }

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

        // Alarm — the most important, while the flag is on — leads to Alarm Block; the rest of the time: First → Deep
        private static BrainGraphBuilder AlarmGraph(AgentTask first, AgentTask deep, AgentTask alarm)
        {
            var g = new BrainGraphBuilder();
            AgentState alarmDecision = g.Decision(new FlagReasoner { Flag = ALARM }, 1, "Alarm");
            g.Do(alarmDecision, g.Block(alarm, "Alarm Block"));
            AgentState work = g.Always("Work");
            AgentState firstNode = g.Block(first, "First");
            g.Do(work, firstNode);
            g.Then(firstNode, g.Block(deep, "Deep"));
            return g;
        }

        private void Run(BrainGraphBuilder g)
        {
            _rig.SetGraph(g.Build());
            _rig.Instance.StateChanged += (_, to) => _entered.Add(to?.DisplayName);
        }

        private bool Reaches(string node, float seconds = 1f) => _rig.StepUntil(() => _rig.StateName == node, seconds, DT);

        private int Entries(string node) => _entered.FindAll(name => name == node).Count;

        [TestCase(TaskStatus.Success, "Done Block")]
        [TestCase(TaskStatus.Failure, "Failed Block")]
        public void Outputs_LeadOnByHowTheBlockEnded_NotBefore(TaskStatus end, string expected)
        {
            var work = new ProbeTask();
            var g = new BrainGraphBuilder();
            AgentState always = g.Always();
            AgentState block = g.Block(work, "Work");
            g.Do(always, block);
            g.Then(block, g.Block(new ProbeTask(), "Done Block"));
            g.OnFail(block, g.Block(new ProbeTask(), "Failed Block"));
            Run(g);
            Assert.IsTrue(Reaches("Work"));

            _rig.StepSeconds(5f * THINK);
            Assert.AreEqual("Work", _rig.StateName, "A block at work is not left by its outputs");

            work.Answer = end;

            Assert.IsTrue(Reaches(expected, 2f * THINK), "The next think the output it ended through leads on");
        }

        [Test]
        public void ArrowIntoItsOwnNode_StartsTheBlockAgain_TheTimeInItKeepsCounting()
        {
            var work = new ProbeTask { Answer = TaskStatus.Success };
            var g = new BrainGraphBuilder();
            AgentState always = g.Always();
            AgentState block = g.Block(work, "Work");
            g.Do(always, block);
            g.Then(block, block);
            Run(g);
            Assert.IsTrue(Reaches("Work"));
            int enters = work.Enters;
            int thinks = _rig.Agent.DecisionCount;
            int changes = _entered.Count;
            float time = _rig.Instance.Current.TimeIn(_rig.Context);

            _rig.StepSeconds(1f);

            Assert.AreEqual("Work", _rig.StateName);
            Assert.AreEqual(_rig.Agent.DecisionCount - thinks, work.Enters - enters,
                "Every think its Done leads back into it, and its block starts anew");
            Assert.AreEqual(changes, _entered.Count, "The agent never left the node");
            Assert.That(_rig.Instance.Current.TimeIn(_rig.Context), Is.GreaterThan(time + 1f - DT),
                "Started again, not entered anew: its time keeps counting");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ArrowIntoTheBrainOrADecision_EndsTheChain_TheBrainChooses(bool intoTheBrain)
        {
            var g = new BrainGraphBuilder();
            AgentState first = g.Decision(new DailyRoutineReasoner(), 1, "First");
            AgentState firstBlock = g.Block(new WaitTask(2f * THINK), "First Block");
            g.Do(first, firstBlock);
            AgentState second = g.Decision(new DailyRoutineReasoner(), 2, "Second");
            g.Do(second, g.Block(new ProbeTask(), "Second Block"));
            g.Then(firstBlock, intoTheBrain ? g.Brain : second);
            Run(g);

            _rig.StepSeconds(1f);

            Assert.Greater(Entries("First Block"), 1, "The Brain took the most important decision anew: its chain ran again");
            Assert.AreEqual(0, Entries("Second Block"), "An arrow into a decision does not take it: only the Brain takes decisions");
            Assert.AreEqual(1, Entries(g.Brain.DisplayName),
                "Only the start: the Brain chose at once, the agent never stood in it");
        }

        [Test]
        public void MoreImportantDecision_TakesOverFromAnyBlockOfTheChain()
        {
            Run(AlarmGraph(new WaitTask(2f * THINK), new ProbeTask(), new ProbeTask()));
            Assert.IsTrue(Reaches("Deep"), "Two blocks down the chain");
            _rig.StepSeconds(BehaviorGraphInstance.MIN_DECISION_TIME);

            _rig.Context.Blackboard.Set(ALARM, true);

            Assert.IsTrue(Reaches("Alarm Block", 2f * THINK), "The Brain looks again every think, whatever block runs");
            Assert.AreEqual(2, _rig.Instance.ActivePath.Count, "The root and the one node the agent is in");
        }

        [Test]
        public void Interrupted_Block_IsToldWhy_AndSoIsABlockOfAGraphTakenAway()
        {
            var work = new ProbeTask();
            var alarm = new ProbeTask();
            Run(AlarmGraph(work, new ProbeTask(), alarm));
            Assert.IsTrue(Reaches("First"));
            _rig.StepSeconds(BehaviorGraphInstance.MIN_DECISION_TIME);

            _rig.Context.Blackboard.Set(ALARM, true);
            Assert.IsTrue(Reaches("Alarm Block", 2f * THINK));

            Assert.AreEqual(ExitReason.Interrupted, work.LastExit, "A more important decision took the block away");

            _rig.SetGraph(null);

            Assert.AreEqual(ExitReason.Stopped, alarm.LastExit, "The whole graph stopped");
        }
    }
}
