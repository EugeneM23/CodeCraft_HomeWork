using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Graph semantics in the brain model: a block that runs holds the agent and is not started again every think, the
    /// sensors (the root's background tasks) run every step, a condition is answered once per think and asks its cheap
    /// gate first, and one graph serves any number of agents, each with its own memory of it.
    /// </summary>
    public class GraphSemanticsTests
    {
        private const float THINK = 0.1f;
        private const float DT = 0.02f;

        private GraphRig _rig;

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig();
            _rig.ThinkInterval = THINK;
        }

        /// <summary>A block that answers what the test sets and counts how often it was started.</summary>
        private sealed class ProbeTask : AgentTask
        {
            public TaskStatus Answer = TaskStatus.Running;
            public int Enters;

            protected override void OnEnter(AgentContext context) => Enters++;

            protected override TaskStatus OnTick(AgentContext context, float dt) => Answer;
        }

        /// <summary>A background task of the root — what a sensor is to the graph — done on every tick.</summary>
        private sealed class PulseTask : AgentTask
        {
            public int Ticks;

            public PulseTask() => IsBackground = true;

            protected override TaskStatus OnTick(AgentContext context, float dt)
            {
                Ticks++;
                return TaskStatus.Success;
            }
        }

        /// <summary>A condition that counts how often it is really evaluated; gated on a target, its gate is that.</summary>
        private sealed class CountingCondition : AgentCondition
        {
            public bool GatedOnTarget;
            public int Evaluations;

            protected override bool Gate(AgentContext context) => !GatedOnTarget || (context != null && context.HasTarget);

            protected override bool Evaluate(AgentContext context)
            {
                Evaluations++;
                return true;
            }
        }

        private int CountRecords(DecisionKind kind)
        {
            int count = 0;
            DecisionTrace trace = _rig.Context.Trace;
            for (int i = 0; i < trace.Count; i++)
            {
                if (trace[i].Kind == kind)
                    count++;
            }

            return count;
        }

        [Test]
        public void RunningBlock_IsNotStartedAgainEveryThink_ItsTimeKeepsCounting()
        {
            var work = new ProbeTask();
            _rig.SetGraph(GraphRig.OneState(work, "Work"));

            _rig.StepSeconds(2f);

            Assert.AreEqual("Work", _rig.StateName);
            Assert.AreEqual(1, work.Enters, "A block at work is left to work");
            Assert.AreEqual(1, CountRecords(DecisionKind.Start));
            Assert.AreEqual(2, CountRecords(DecisionKind.Transition),
                "The Brain took the decision and its Do led to the block — nothing since");
            // Entered at the second think (the first one only starts the graph in its Brain)
            Assert.That(_rig.Instance.Current.TimeIn(_rig.Context), Is.GreaterThan(2f - 3f * THINK));
        }

        [Test]
        public void Sensor_RunsEveryStep_EvenAfterItSucceeds()
        {
            const int STEPS = 10;
            var pulse = new PulseTask();
            var g = new BrainGraphBuilder();
            g.Root.AddTask(pulse);
            g.Do(g.Always(), g.Block(new ProbeTask(), "Work"));
            _rig.SetGraph(g.Build());

            _rig.Step(DT, STEPS);

            Assert.AreEqual(STEPS, pulse.Ticks, "Done on its tick, a sensor is started again on the next step");
        }

        [Test]
        public void Condition_AskedTwiceInOneThink_IsEvaluatedOnce()
        {
            var condition = new CountingCondition();
            var g = new BrainGraphBuilder();
            AgentState always = g.Always();
            AgentState first = g.Block(new BranchTask(condition), "First");
            AgentState second = g.Block(new BranchTask(condition), "Second");
            g.Do(always, first);
            g.OnEnd(first, second);
            g.OnEnd(second, g.Block(new WaitTask(2f * THINK), "Pause"));
            _rig.SetGraph(g.Build());
            int runs = 0;
            _rig.Instance.StateChanged += (_, to) => runs += to == second ? 1 : 0;

            _rig.StepSeconds(2f);

            Assert.Greater(runs, 1, "The chain ran again and again");
            Assert.AreEqual(runs, condition.Evaluations,
                "Two Branches ask one condition within one think (instant blocks pass in it): one answer per think");
        }

        [Test]
        public void ClosedGate_TheExpensiveTestIsNotRun()
        {
            var lineOfSight = new CountingCondition { GatedOnTarget = true };
            var g = new BrainGraphBuilder();
            AgentState always = g.Always();
            AgentState ask = g.Block(new BranchTask(lineOfSight), "Ask");
            g.Do(always, ask);
            g.Then(ask, g.Block(new ProbeTask(), "Attack"));
            g.OnFail(ask, g.Block(new WaitTask(2f * THINK), "Idle"));
            _rig.SetGraph(g.Build());

            _rig.StepSeconds(1f);

            Assert.AreEqual("Idle", _rig.StateName, "No target: the answer is no, asked again and again");
            Assert.AreEqual(0, lineOfSight.Evaluations, "The gate is closed: the expensive test is never run");

            _rig.CreateTargetAt(Vector3.right);

            Assert.IsTrue(_rig.StepUntil(() => _rig.StateName == "Attack", 1f));
            Assert.AreEqual(1, lineOfSight.Evaluations);
        }

        [Test]
        public void Condition_OutsideAThink_IsEvaluatedEveryCall()
        {
            var condition = new CountingCondition();

            condition.Check(_rig.Context);
            condition.Check(_rig.Context);

            Assert.AreEqual(2, condition.Evaluations, "Blocks and abilities ask between think ticks and must get fresh answers");
        }

        [Test]
        public void SharedGraph_TwoAgents_KeepTheirOwnState()
        {
            // One description, two agents out of step: each is in its own node, with its own wait left
            const float WAIT = 1f;
            var firstWait = new WaitTask(WAIT);
            var g = new BrainGraphBuilder();
            AgentState always = g.Always();
            AgentState first = g.Block(firstWait, "First Wait");
            AgentState second = g.Block(new WaitTask(WAIT), "Second Wait");
            g.Do(always, first);
            g.Then(first, second);
            g.Then(second, g.Block(new ProbeTask(), "Done"));
            BehaviorGraph graph = g.Build();
            var other = new GraphRig { ThinkInterval = THINK };
            _rig.SetGraph(graph);

            _rig.StepSeconds(1.5f * WAIT);
            other.SetGraph(graph);
            other.StepSeconds(0.6f * WAIT);

            Assert.AreEqual("Second Wait", _rig.StateName);
            Assert.AreEqual("First Wait", other.StateName);
            Assert.That(firstWait.LeftFor(other.Context), Is.GreaterThan(0f), "The other still waits out the first wait");
            Assert.That(firstWait.LeftFor(_rig.Context), Is.LessThanOrEqualTo(0f), "This one has waited it out");

            _rig.StepSeconds(WAIT);
            Assert.AreEqual("Done", _rig.StateName, "Its chain ran on without the other's progress");
            Assert.AreEqual("First Wait", other.StateName, "The other is still on its first wait");
            other.StepSeconds(2f * WAIT);
            Assert.AreEqual("Done", other.StateName);
        }

        [Test]
        public void SharedGraph_SameAgent_CanTakeItBack()
        {
            BehaviorGraph graph = GraphRig.OneState(new ProbeTask(), "Work");

            _rig.SetGraph(graph);
            _rig.SetGraph(null);
            _rig.SetGraph(graph);

            Assert.IsTrue(_rig.StepUntil(() => _rig.StateName == "Work", 1f), "A graph the agent let go of runs again");
        }
    }
}
