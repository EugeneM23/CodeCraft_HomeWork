using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AIKit.Tests
{
    /// <summary>
    /// The corners of the graph: what an agent remembers (and forgets on a new behavior), entering a node before the
    /// graph started, no decision that holds any more, a graph without a context, a root or a Brain, writing and reading
    /// the odd asset (duplicate ids, an old asset with reflexes, a node outside the root's list), compile messages, and
    /// the small parts without what they need.
    /// </summary>
    public class GraphGapTests
    {
        private const float DT = 0.02f;
        private const float THINK = 0.1f;

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

        /// <summary>A background task of the root — what a sensor is to the graph — counting its ticks.</summary>
        private sealed class PulseTask : AgentTask
        {
            public int Ticks;

            public PulseTask() => IsBackground = true;

            protected override TaskStatus OnTick(AgentContext context, float dt)
            {
                Ticks++;
                return TaskStatus.Running;
            }
        }

        private BehaviorGraphAsset NewAsset()
        {
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            asset.name = "GapGraph";
            _created.Add(asset);
            return asset;
        }

        // The smallest graph that does something: the Brain, Daily Routine, and a long wait as its one block
        private static BehaviorGraph Idle() => GraphRig.OneState(new WaitTask(100f), "Idle");

        // ---------- what the agent remembers ----------

        [Test]
        public void Memory_GrowsWhileRunning_ANewBehaviorStartsFromNothing()
        {
            var rig = new GraphRig(trace: false);
            rig.SetGraph(Idle());
            Assert.AreEqual(0, rig.Context.Memory.Count, "Nothing remembered before the first step");

            rig.Step(DT, 3);
            int running = rig.Context.Memory.Count;
            Assert.Greater(running, 0, "The nodes the agent is in and their blocks are remembered");

            new WaitTask(1f).Tick(rig.Context, DT);         // a task outside any graph is remembered too
            Assert.AreEqual(running + 1, rig.Context.Memory.Count);

            rig.SetGraph(Idle());
            Assert.AreEqual(0, rig.Context.Memory.Count, "A new behavior: everything forgotten");
        }

        // ---------- the instance ----------

        [Test]
        public void EnterState_BeforeTheGraphStarted_StartsInThatBlock_AndItsChainGoesOn()
        {
            var g = new BrainGraphBuilder();
            AgentState always = g.Always();
            AgentState first = g.Block(new WaitTask(100f), "First");
            AgentState second = g.Block(new WaitTask(2f * THINK), "Second");
            g.Do(always, first);
            g.Then(first, second);
            g.Then(second, g.Block(new WaitTask(100f), "Third"));
            var rig = new GraphRig(trace: false) { ThinkInterval = THINK };
            rig.SetGraph(g.Build());
            var entered = new List<string>();
            rig.Instance.StateChanged += (_, to) => entered.Add(to.DisplayName);

            Assert.IsTrue(rig.Agent.EnterState("Second"), "A loaded save: straight into a block of a chain");
            Assert.IsTrue(rig.Instance.IsRunning);
            Assert.AreEqual("Second", rig.StateName);
            Assert.AreSame(always, rig.Instance.Decision, "The decision whose chain it is runs it for the Brain");

            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "Third", 1f), "The chain goes on from there");
            CollectionAssert.DoesNotContain(entered, "First");
        }

        [Test]
        public void NoDecisionHoldsAnyMore_TheAgentStandsInTheBrain_Quietly()
        {
            const string ALLOWED = "Allowed";
            var g = new BrainGraphBuilder();
            AgentState once = g.Decision(new FlagReasoner { Flag = ALLOWED }, 1, "Once");
            g.Do(once, g.Block(new WaitTask(100f), "Wait"));
            var rig = new GraphRig { ThinkInterval = THINK };
            rig.Context.Blackboard.Set(ALLOWED, true);
            rig.SetGraph(g.Build());
            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "Wait", 1f));

            rig.Context.Blackboard.Set(ALLOWED, false);
            rig.StepSeconds(1f);

            Assert.AreEqual(g.Brain.DisplayName, rig.StateName, "Nothing holds: the agent stands in the Brain");
            Assert.IsNull(rig.Instance.Decision);
            Assert.IsTrue(rig.Instance.IsRunning, "Not torn down for nothing");

            int recorded = rig.Context.Trace.TotalRecorded;
            rig.StepSeconds(1f);
            Assert.AreEqual(recorded, rig.Context.Trace.TotalRecorded, "Standing, it records nothing think after think");
        }

        [Test]
        public void Graph_WithoutAContext_RunsByHand()
        {
            BehaviorGraphInstance instance = Idle().CreateInstance();

            instance.Start(null);
            Assert.AreEqual("Brain", instance.Current.DisplayName, "A graph starts in its Brain");

            instance.Think(null);
            instance.Tick(null, DT);

            Assert.AreEqual("Idle", instance.Current.DisplayName, "The Brain took its decision, whose Do leads to the block");
        }

        [Test]
        public void Graph_WithoutARoot_NeverRuns_AndHasNoStatesToEnter()
        {
            var graph = new BehaviorGraph(null);
            BehaviorGraphInstance instance = graph.CreateInstance();
            var context = new AgentContext(null, null, 1);
            var states = new List<AgentState>();

            instance.Start(context);
            graph.CollectStates(states);

            Assert.IsFalse(instance.IsRunning);
            Assert.IsEmpty(states);
            Assert.Throws<System.ArgumentException>(() => instance.EnterState(1, context));
            Assert.Throws<System.ArgumentException>(() => instance.EnterState("Idle", context));
        }

        [Test]
        public void Graph_WithoutABrain_DoesNothing_NotEvenItsSensors()
        {
            var pulse = new PulseTask();
            var root = new AgentState("Root");
            root.AddTask(pulse);
            root.AddChild(new AgentState("Idle").AddTask(new WaitTask(100f)));
            var rig = new GraphRig(trace: false);

            LogAssert.Expect(LogType.Error, new Regex("has no Brain"));
            rig.SetGraph(new BehaviorGraph(root));
            rig.StepSeconds(1f);

            Assert.IsFalse(rig.Instance.IsRunning, "The canvas is the program: no Brain, nothing runs");
            Assert.IsNull(rig.Instance.Current);
            Assert.AreEqual(0, pulse.Ticks, "Not even its sensors");
            Assert.IsFalse(rig.Agent.EnterState("Idle"), "No chain reaches the block: it cannot be entered");
        }

        // ---------- the asset ----------

        [Test]
        public void Write_NullGraphOrAsset_SaysWhich()
        {
            BehaviorGraphAsset asset = NewAsset();

            var noGraph = Assert.Throws<System.ArgumentNullException>(() => GraphSerializer.Write(null, asset));
            var noAsset = Assert.Throws<System.ArgumentNullException>(() => GraphSerializer.Write(Idle(), null));

            Assert.AreEqual("graph", noGraph.ParamName);
            Assert.AreEqual("asset", noAsset.ParamName);
        }

        [Test]
        public void Read_TwoRecordsWithOneId_ReportedAndTheFirstKept()
        {
            BehaviorGraph written = Idle();
            BehaviorGraphAsset asset = NewAsset();
            GraphSerializer.Write(written, asset);
            GraphRecord root = asset.Find(asset.RootId);
            asset.Records.Add(new GraphRecord { Id = root.Id, Type = "aikit.state", Data = root.Data });
            var issues = new List<GraphIssue>();

            BehaviorGraph graph = GraphSerializer.Read(asset, issues);

            Assert.IsNotNull(graph);
            Assert.IsTrue(issues.Any(i => i.Code == GraphIssueCode.DanglingEdge && i.Message.Contains("share id")),
                string.Join(" | ", issues));
            Assert.AreEqual(written.Root.Children.Count, graph.Root.Children.Count,
                "The first record of the id is the one read");
        }

        [Test]
        public void Read_AnOldAssetWithReflexes_ReportedAsTheOldModel_TheRestRead()
        {
            BehaviorGraph written = Idle();
            BehaviorGraphAsset asset = NewAsset();
            GraphSerializer.Write(written, asset);
            asset.Reflexes.Add(asset.RootId);
            var issues = new List<GraphIssue>();

            BehaviorGraph graph = GraphSerializer.Read(asset, issues);

            Assert.IsTrue(issues.Any(i => i.Code == GraphIssueCode.OldModel && i.IsError), string.Join(" | ", issues));
            Assert.IsNotNull(graph);
            Assert.AreEqual(written.Root.Children.Count, graph.Root.Children.Count);
        }

        [Test]
        public void Write_GraphWithoutARoot_ReadsBackAsMissingRoot()
        {
            BehaviorGraphAsset asset = NewAsset();
            GraphSerializer.Write(new BehaviorGraph(null), asset);
            var issues = new List<GraphIssue>();

            Assert.IsNull(GraphSerializer.Read(asset, issues));
            Assert.AreEqual(0, asset.RootId);
            Assert.IsTrue(issues.Any(i => i.Code == GraphIssueCode.MissingRoot));
        }

        [Test]
        public void Write_NodeOutsideTheRootsList_EveryRecordGetsItsOwnId()
        {
            var g = new BrainGraphBuilder();
            AgentState always = g.Always();
            AgentState idle = g.Block(new WaitTask(1f), "Idle");
            g.Do(always, idle);
            BehaviorGraph graph = g.Build();
            int max = 0;
            graph.VisitNodes(node => max = Mathf.Max(max, node.Id));

            // A node numbered apart, with the id the writer would hand the second arrow
            var outside = new AgentState("Outside") { Id = max + 1001 };
            outside.AddTransition(new Transition { Target = graph.Root });
            idle.AddTransition(new Transition { Target = outside, Trigger = TransitionTrigger.TasksSucceeded });
            BehaviorGraphAsset asset = NewAsset();

            GraphSerializer.Write(graph, asset);

            List<int> ids = asset.Records.Select(r => r.Id).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count(), string.Join(", ", ids));
            Assert.IsNotNull(asset.Records.FirstOrDefault(r => r.Id == outside.Id), "The node outside the list is saved");
        }

        [Test]
        public void Compile_LogsErrorsAndWarnings_NamingTheAsset()
        {
            var g = new BrainGraphBuilder();
            g.Do(g.Always(), g.Block(new WaitTask(1f), "Idle"));
            g.Root.AddChild(new AgentState("Empty"));               // no block: a warning
            BehaviorGraphAsset asset = NewAsset();
            GraphSerializer.Write(g.Build(), asset);
            asset.Reflexes.Add(asset.RootId);                       // the old model: an error
            asset.MarkChanged();

            LogAssert.Expect(LogType.Error, new Regex("GapGraph: .*reflex"));
            LogAssert.Expect(LogType.Warning, new Regex("GapGraph: .*has no block"));
            BehaviorGraph graph = asset.Compile();

            Assert.IsNotNull(graph);
            Assert.AreSame(graph, asset.Compile(), "Compiled once");
        }

        [Test]
        public void Registry_ListsTheNodeTypes_InMenuOrder()
        {
            IReadOnlyList<AIKitNodeRegistry.Entry> entries = AIKitNodeRegistry.Entries;

            Assert.IsTrue(entries.Any(e => e.Type == typeof(WaitTask)));
            for (int i = 1; i < entries.Count; i++)
                Assert.LessOrEqual(string.CompareOrdinal(entries[i - 1].Info.Path, entries[i].Info.Path), 0);
        }

        [Test]
        public void Validate_NoAsset_NothingToSay()
        {
            Assert.IsEmpty(GraphValidator.Validate(null));
        }

        // ---------- the small parts without what they need ----------

        [Test]
        public void Random_AllWeightsZero_PicksNothing()
        {
            var random = new PickRandomTask();
            random.Outputs[0].Weight = 0f;
            random.Outputs[1].Weight = 0f;
            AgentState node = new AgentState("Random").AddTask(random);
            node.AddTransition(new Transition { Target = new AgentState("A"), Trigger = TransitionTrigger.TasksFinished, Output = 0 });
            node.AddTransition(new Transition { Target = new AgentState("B"), Trigger = TransitionTrigger.TasksFinished, Output = 1 });
            var context = new AgentContext(null, null, 1);

            Assert.AreEqual(-1, random.Pick(context, node), "No output can be picked: the flow ends here");
            Assert.AreEqual(-1, random.LastFor(context));
        }

        [Test]
        public void MoveTo_AStrongerWriterHoldsTheBody_WaitsWithoutMoving()
        {
            var rig = new GraphRig(trace: false);
            var stronger = new object();
            Assume.That(rig.Context.Arbiter.TryClaim(stronger, BodyAxis.Move, MoveAuthority.Frozen));
            var move = new MoveToTask { Destination = BlackboardValue<Vector3>.Constant(new Vector3(10f, 0f, 0f)) };

            Assert.AreEqual(TaskStatus.Running, move.Tick(rig.Context, DT));
            rig.Actuator.Apply(DT);

            Assert.AreEqual(Vector3.zero, rig.Actuator.Position);
            Assert.AreSame(stronger, rig.Context.Arbiter.OwnerOf(BodyAxis.Move));
        }

        [Test]
        public void Arbiter_NoOwner_ClaimsNothing()
        {
            var arbiter = new MovementArbiter();

            Assert.IsFalse(arbiter.TryClaim(null, BodyAxis.Move, MoveAuthority.Frozen));
            Assert.AreEqual(MoveAuthority.None, arbiter.AuthorityOf(BodyAxis.Move));
        }

        [Test]
        public void Wait_LeftFor_CountsDown()
        {
            var context = new AgentContext(null, null, 1);
            var wait = new WaitTask(1f);

            wait.Tick(context, 0.25f);

            Assert.AreEqual(0.75f, wait.LeftFor(context), 1e-4f);
        }

        [Test]
        public void Compare_NoAgent_False()
        {
            var condition = new CompareCondition("Health", NumberComparison.Less, 1f);

            Assert.IsFalse(condition.Check(null));
        }
    }
}
