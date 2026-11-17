using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>A node of the tests whose fields were renamed: the asset still holds the old names.</summary>
    [Serializable]
    [AIKitNode("Tests/Renamed", "Task", "aikit.tests.renamed")]
    public sealed class RenamedTestTask : AgentTask
    {
        [FormerlySerializedAs("OldSeconds")] public float Seconds;

        [FormerlySerializedAs("OldInner")] [NonSerialized] public AgentTask Inner;

        protected override TaskStatus OnTick(AgentContext context, float dt) => TaskStatus.Running;
    }

    /// <summary>
    /// Graph assets saved by the old model, made by hand: a small Brain graph written as today, then one part of the old
    /// model put into its records the way an old asset held it — reflex arrows, a node holding nodes, conditions on an
    /// arrow or on entering a node, an arrow fired by a signal or by a change of a variable.
    /// </summary>
    internal static class OldModelAssets
    {
        public const string REFLEX = "a reflex";
        public const string NESTED_NODE = "a nested node";
        public const string CONDITIONS_ON_AN_ARROW = "conditions on an arrow";
        public const string CONDITIONS_ON_ENTERING = "conditions on entering a node";
        public const string SIGNAL_ARROW = "an arrow by a signal";
        public const string CHANGE_ARROW = "an arrow by a change of a variable";

        // The id of a record the old part adds: far above any the writer gives (nodes from 1, arrows a thousand above)
        private const int OLD = 90000;

        // The old model's arrow kinds, gone from TransitionTrigger: a signal, a change of a variable
        private const int SIGNAL = 2;
        private const int CHANGE = 3;

        /// <summary>Writes the graph with this part of the old model into the asset; returns the id of the record the problem is about.</summary>
        public static int Write(BehaviorGraphAsset asset, string part)
        {
            var g = new BrainGraphBuilder();
            AgentState routine = g.Always();
            AgentState wait = g.Block(new WaitTask(1f), "Wait");
            g.Do(routine, wait);
            GraphSerializer.Write(g.Build(), asset);

            GraphRecord node = asset.Find(wait.Id);
            GraphRecord arrow = asset.Find(asset.Find(routine.Id).Links
                .Find(link => link.Field == nameof(AgentState.Transitions)).Ids[0]);
            switch (part)
            {
                case REFLEX:
                    asset.Records.Add(Arrow(TransitionTrigger.Always, wait.Id));
                    asset.Reflexes.Add(OLD);
                    return asset.RootId;

                case NESTED_NODE:
                    asset.Records.Add(new GraphRecord
                    {
                        Id = OLD, Type = "aikit.state", Data = JsonUtility.ToJson(new AgentState("Inner") { Id = OLD })
                    });
                    node.Links.Add(new GraphLink { Field = nameof(AgentState.Children), Ids = { OLD } });
                    return wait.Id;

                case CONDITIONS_ON_AN_ARROW:
                    asset.Records.Add(Condition());
                    arrow.Links.Add(new GraphLink { Field = "Conditions", Ids = { OLD } });
                    return arrow.Id;

                case CONDITIONS_ON_ENTERING:
                    asset.Records.Add(Condition());
                    node.Links.Add(new GraphLink { Field = "EnterConditions", Ids = { OLD } });
                    return wait.Id;

                case SIGNAL_ARROW:
                case CHANGE_ARROW:
                    asset.Records.Add(Arrow((TransitionTrigger)(part == SIGNAL_ARROW ? SIGNAL : CHANGE), wait.Id));
                    node.Links.Add(new GraphLink { Field = nameof(AgentState.Transitions), Ids = { OLD } });
                    return OLD;

                default:
                    throw new ArgumentException($"No such part of the old model: '{part}'");
            }
        }

        private static GraphRecord Arrow(TransitionTrigger trigger, int target) => new()
        {
            Id = OLD,
            Type = GraphSerializer.TRANSITION,
            Data = JsonUtility.ToJson(new Transition { Trigger = trigger, TargetId = target }),
            Links = { new GraphLink { Field = nameof(Transition.Target), Ids = { target } } }
        };

        private static GraphRecord Condition() => new()
        {
            Id = OLD,
            Type = "aikit.condition.compare",
            Data = JsonUtility.ToJson(new CompareCondition("Alarm", VariableType.Bool) { Id = OLD })
        };
    }

    /// <summary>
    /// Stage 9, tier 1: the graph as an asset. Round trip of every element kind; the same trace from the asset as from
    /// the builder; one compiled graph shared by many agents that keep their own state; the validator's cases, each
    /// naming the node — a graph with no Brain, a block no chain reaches, a node of two blocks, the parts of the old
    /// model; renames that keep the asset; no allocation in the hot path.
    /// </summary>
    public class GraphAssetTests
    {
        private const float DT = 0.02f;
        private const float THINK = 0.1f;

        private const string TARGET = "Target";
        private const string NIGHT = "Night";
        private const string PAUSE = "Pause";
        private const string TIRED = "Tired";
        private const string COUNT = "Count";
        private const float PAUSE_SECONDS = 0.25f;

        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
                Object.DestroyImmediate(created);
            _created.Clear();
        }

        private BehaviorGraphAsset NewAsset()
        {
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            asset.name = "TestGraph";
            _created.Add(asset);
            return asset;
        }

        private BehaviorGraphAsset AssetOf(BehaviorGraph graph)
        {
            BehaviorGraphAsset asset = NewAsset();
            GraphSerializer.Write(graph, asset);
            return asset;
        }

        // A graph with every kind of element: a sensor with two shapes wired in (Detection), the Brain with its Target,
        // decisions with priorities and "not from", their chains with every kind of arrow — Do, Done, Failed, either way,
        // Random's numbered outputs, an On Tick pin — a Branch asking an OR of a Compare and an AND, numbers read from
        // variables, declared variables
        private static BehaviorGraph Everything()
        {
            var g = new BrainGraphBuilder();
            g.Sensor(new DetectionTask(new RadiusSensorTask { Radius = 5f }, new ConeSensorTask { Range = 8f })
            {
                StoreIn = TARGET
            });
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform)
                .Variable(NIGHT, VariableType.Transform)
                .Variable(new VariableDeclaration(PAUSE, VariableType.Float) { Float = PAUSE_SECONDS })
                .Variable(TIRED, VariableType.Bool)
                .Variable(COUNT, VariableType.Float);

            // Sleep — while it is night: lie down
            AgentState sleep = g.Decision(new SleepReasoner { Condition = new CompareCondition(NIGHT, VariableType.Transform) }, 1);
            g.Do(sleep, g.Block(new WaitTask(2f), "Lie Down"));

            // Watch — the target is seen: a glance or a stare, at random
            AgentState watch = g.Decision(new WatchReasoner(), 2);
            AgentState look = g.Block(new PickRandomTask
            {
                Outputs = new List<RandomOutput>
                {
                    new RandomOutput { Weight = 3f }, new RandomOutput { Weight = 1f, NotTwiceInARow = true }
                }
            }, "Look Around");
            g.Do(watch, look);
            g.Out(look, 0, g.Block(new WaitTask { Seconds = BlackboardValue<float>.Variable(PAUSE) }, "Glance"));
            g.Out(look, 1, g.Block(new WaitTask(1f), "Stare"));

            // Daily Routine — the rest of the time: idle (counting the thinks), dig, rest when tired — or when it has
            // idled long and sees somebody — else start the count anew
            AgentState routine = g.Always();
            AgentState idle = g.Block(new WaitTask(0.3f), "Idle");
            AgentState dig = g.Block(new WaitTask { Seconds = BlackboardValue<float>.Variable(PAUSE) }, "Dig");
            AgentState tired = g.Block(new BranchTask(new AnyCondition(
                new CompareCondition(TIRED, VariableType.Bool),
                new AllCondition(new CompareCondition(COUNT, NumberComparison.GreaterOrEqual, 3f),
                    new CompareCondition(TARGET, VariableType.Transform)))), "Tired?");
            g.Do(routine, idle);
            g.OnTick(idle, g.Block(new AddNumberTask(COUNT), "Count Up"));
            g.Then(idle, dig);
            g.OnEnd(dig, tired);
            g.Then(tired, g.Block(new WaitTask(0.5f), "Rest"));
            g.OnFail(tired, g.Block(new SetNumberTask { Variable = COUNT, Value = 0f }, "Reset"));

            BehaviorGraph built = g.Build();
            sleep.Reasoner.NotFrom.Add(watch.Id);           // no sleep while it watches
            return built;
        }

        // Every node once — the nodes, their blocks, what those hold: its id, type and name
        private static List<string> Nodes(BehaviorGraph graph)
        {
            var nodes = new List<string>();
            graph.VisitNodes(node => nodes.Add($"{node.Id} {node.GetType().Name} {node.DisplayName}"));
            return nodes;
        }

        // Every arrow: from where to where, by what, out of which numbered output
        private static List<string> Arrows(BehaviorGraph graph)
        {
            var states = new List<AgentState>();
            graph.CollectStates(states);
            return states.SelectMany(state => state.Transitions.Select(arrow =>
                $"{state.Id} → {arrow.Target?.Id} {arrow.Trigger} {arrow.Output}")).ToList();
        }

        /// <summary>What the graph's sensors find around the agent: these objects, not the scene's tagged ones.</summary>
        private sealed class Around : IObjectFinder
        {
            public readonly List<Transform> Objects = new();

            public Transform FindNearest(string kind, Vector3 center, float radius, Transform self)
            {
                Transform nearest = null;
                float best = radius * radius;
                foreach (Transform found in Objects)
                {
                    float distance = (found.position - center).sqrMagnitude;
                    if (found != self && distance <= best)
                    {
                        best = distance;
                        nearest = found;
                    }
                }

                return nearest;
            }

            public void FindAll(string kind, Vector3 center, float radius, Transform self, List<Transform> results)
            {
                results.Clear();
                foreach (Transform found in Objects)
                {
                    if (found != self && (found.position - center).sqrMagnitude <= radius * radius)
                        results.Add(found);
                }
            }
        }

        private static List<string> TraceOf(BehaviorGraph graph, Action<GraphRig, int> drive, int steps = 200)
        {
            var rig = new GraphRig(seed: 7);
            rig.SetGraph(graph);
            for (int i = 0; i < steps; i++)
            {
                drive?.Invoke(rig, i);
                rig.Step(DT);
            }

            var lines = new List<string>();
            DecisionTrace trace = rig.Context.Trace;
            for (int i = 0; i < trace.Count; i++)
                lines.Add($"{trace[i].Time:F2} {trace[i].Kind} {trace[i].From?.DisplayName} {trace[i].To?.DisplayName}");

            return lines;
        }

        // The way a seeded agent goes through Everything: the routine; somebody comes into sight and goes; it is tired;
        // night falls and ends. Its sensor finds the somebody among the test's objects, not the scene's
        private static List<string> TraceOfEverything(BehaviorGraph graph)
        {
            var around = new Around();
            var somebody = new GameObject("Somebody") { hideFlags = HideFlags.HideAndDontSave };
            var night = new GameObject("Night") { hideFlags = HideFlags.HideAndDontSave };
            somebody.transform.position = new Vector3(2f, 0f, 0f);
            try
            {
                return TraceOf(graph, (rig, step) =>
                {
                    if (step == 0)
                        rig.Context.Objects = around;
                    if (step == 60)
                        around.Objects.Add(somebody.transform);
                    if (step == 100)
                    {
                        around.Objects.Clear();
                        rig.Context.Blackboard.Set(TIRED, true);
                    }

                    if (step == 140)
                        rig.Context.Blackboard.Set(NIGHT, night.transform);
                    if (step == 170)
                        rig.Context.Blackboard.Set<Transform>(NIGHT, null);
                });
            }
            finally
            {
                Object.DestroyImmediate(somebody);
                Object.DestroyImmediate(night);
            }
        }

        // ---------- round trip ----------

        [Test]
        public void RoundTrip_EveryElementKind_SameShape()
        {
            BehaviorGraph code = Everything();
            BehaviorGraphAsset asset = AssetOf(code);

            BehaviorGraph read = asset.Compile();

            CollectionAssert.IsEmpty(asset.Issues, string.Join(" | ", asset.Issues));
            CollectionAssert.AreEqual(Nodes(code), Nodes(read));
            CollectionAssert.AreEqual(Arrows(code), Arrows(read));
            var states = new List<AgentState>();
            read.CollectStates(states);
            CollectionAssert.IsSubsetOf(Enum.GetValues(typeof(TransitionTrigger)),
                states.SelectMany(state => state.Transitions.Select(arrow => arrow.Trigger)).ToList(),
                "Set-up: every kind of arrow is in the graph");
            Assert.AreEqual(code.Brain.Id, read.Brain?.Id, "It starts in the same Brain");
            CollectionAssert.AreEqual(code.Variables.Select(v => $"{v.Name} {v.Type}"),
                read.Variables.Select(v => $"{v.Name} {v.Type}"));
            Assert.AreEqual(PAUSE_SECONDS, read.Variables.Single(v => v.Name == PAUSE).Float);
        }

        [Test]
        public void FromTheAsset_TheSameTrace_AsFromTheBuilder()
        {
            List<string> fromCode = TraceOfEverything(Everything());
            List<string> fromAsset = TraceOfEverything(AssetOf(Everything()).Compile());

            Assert.Greater(fromCode.Count, 5, "Something was decided");
            foreach (string decision in new[] { "Daily Routine", "Watch", "Sleep" })
                Assert.IsTrue(fromCode.Exists(line => line.EndsWith($" {decision}")), $"Set-up: the Brain took {decision}");
            CollectionAssert.AreEqual(fromCode, fromAsset);
        }

        [Test]
        public void EveryPreset_WrittenAndRead_TheSameTrace()
        {
            foreach (BehaviorPresets.Preset preset in Enum.GetValues(typeof(BehaviorPresets.Preset)))
            {
                var tuning = new BehaviorPresets.Tuning();
                void Target(GraphRig rig, int step)
                {
                    if (step == 0)
                        rig.CreateTargetAt(new Vector3(5f, 0f, 0f));
                }

                List<string> fromCode = TraceOf(BehaviorPresets.Build(preset, tuning), Target, 150);
                List<string> fromAsset = TraceOf(AssetOf(BehaviorPresets.Build(preset, tuning)).Compile(), Target, 150);

                CollectionAssert.AreEqual(fromCode, fromAsset, preset.ToString());
                GraphRig.DestroyAll();
            }
        }

        /// <summary>A node of code only — no [AIKitNode] id: it cannot be saved.</summary>
        private sealed class CodeOnlyTask : AgentTask
        {
            protected override TaskStatus OnTick(AgentContext context, float dt) => TaskStatus.Running;
        }

        [Test]
        public void Write_CodeOnlyNode_RefusedByName()
        {
            var g = new BrainGraphBuilder();
            g.Do(g.Always(), g.Block(new CodeOnlyTask { Name = "my code node" }, "Code"));

            var error = Assert.Throws<InvalidOperationException>(() => AssetOf(g.Build()));

            StringAssert.Contains("my code node", error.Message);
        }

        // ---------- one compiled graph, many agents ----------

        private const float IDLE = 0.5f;

        // Idle for a while, then work: the agents are told apart by where they are and how long they have been there
        private static BehaviorGraph IdleThenWork()
        {
            var g = new BrainGraphBuilder();
            AgentState idle = g.Block(new WaitTask(IDLE), "Idle");
            g.Do(g.Always(), idle);
            g.Then(idle, g.Block(new WaitTask(100f), "Work"));
            return g.Build();
        }

        [Test]
        public void Compile_OnceForTheAsset()
        {
            BehaviorGraphAsset asset = AssetOf(Everything());

            Assert.AreSame(asset.Compile(), asset.Compile());
            asset.MarkChanged();
            Assert.AreNotSame(asset.Compile(), null);
        }

        [Test]
        public void FiftyAgents_OneCompiledGraph_EachItsOwnState()
        {
            BehaviorGraphAsset asset = AssetOf(IdleThenWork());
            BehaviorGraph graph = asset.Compile();
            var rigs = new List<GraphRig>();
            for (int i = 0; i < 50; i++)
            {
                var rig = new GraphRig(seed: i + 1, trace: false) { ThinkInterval = THINK };
                rig.SetGraph(asset.Compile());
                rig.Step(DT, i);                                  // out of step: agent i is i steps ahead
                rigs.Add(rig);
            }

            foreach (GraphRig rig in rigs)
                rig.Step(DT, 10);

            float first = rigs[0].Instance.Current.TimeIn(rigs[0].Context);
            for (int i = 0; i < rigs.Count; i++)
            {
                Assert.AreSame(graph, rigs[i].Instance.Graph, "One description");
                if ((i + 10) * DT >= IDLE)
                    continue;                                     // alive less than Idle lasts: still idling

                Assert.AreEqual("Idle", rigs[i].StateName, $"agent {i}");
                Assert.AreEqual(first + i * DT, rigs[i].Instance.Current.TimeIn(rigs[i].Context), 1e-4f,
                    $"agent {i}: its own time in the node — i steps longer than the first agent's");
            }

            Assert.AreEqual("Idle", rigs[0].StateName);
            Assert.AreEqual("Work", rigs[49].StateName, "The agents ahead have moved on; the others did not follow");
        }

        [Test]
        public void SharedGraph_HotPath_DoesNotAllocate()
        {
            BehaviorGraphAsset asset = AssetOf(Everything());
            var rigs = new List<GraphRig>();
            for (int i = 0; i < 4; i++)
            {
                var rig = new GraphRig(seed: i + 1, trace: false);
                rig.Context.Objects = new Around();               // the sensor looks without asking the scene
                rig.SetGraph(asset.Compile());
                rigs.Add(rig);
            }

            for (int step = 0; step < 100; step++)                // warm up: every node entered, every memory made
            {
                foreach (GraphRig rig in rigs)
                    rig.Step(DT);
            }

            // Unity's own count (GC.GetAllocatedBytesForCurrentThread reads 0 in this editor whatever is allocated)
            Assert.That(() =>
            {
                for (int step = 0; step < 50; step++)
                {
                    foreach (GraphRig rig in rigs)
                    {
                        rig.Instance.Think(rig.Context);
                        rig.Instance.Tick(rig.Context, DT);
                    }
                }
            }, NoAllocation.Constraint, "Ticking a shared graph allocates nothing");
        }

        // ---------- the validator: one case each, each naming the node ----------

        private static GraphIssue Only(IReadOnlyList<GraphIssue> issues, GraphIssueCode code)
        {
            List<GraphIssue> found = issues.Where(issue => issue.Code == code).ToList();
            Assert.AreEqual(1, found.Count, $"One {code}: {string.Join(" | ", issues)}");
            return found[0];
        }

        [Test]
        public void Validator_MissingRoot()
        {
            BehaviorGraphAsset asset = AssetOf(Everything());
            asset.RootId = 99999;

            GraphIssue issue = Only(GraphValidator.Validate(asset), GraphIssueCode.MissingRoot);

            Assert.IsTrue(issue.IsError);
        }

        [Test]
        public void Validator_UnknownNodeType_NamesTheRecord()
        {
            BehaviorGraphAsset asset = AssetOf(Everything());
            GraphRecord wait = asset.Records.First(r => r.Type == "aikit.task.wait");
            wait.Type = "aikit.task.gone";

            GraphIssue issue = Only(GraphValidator.Validate(asset), GraphIssueCode.UnknownNodeType);

            Assert.AreEqual(wait.Id, issue.RecordId);
            StringAssert.Contains("'aikit.task.gone'", issue.Message);
        }

        [Test]
        public void Validator_DanglingEdge_NamesTheNodeAndTheField()
        {
            BehaviorGraphAsset asset = AssetOf(Everything());
            GraphRecord idle = asset.Records.First(r => r.Data.Contains("\"Idle\""));
            idle.Links.First(l => l.Field == nameof(AgentState.Tasks)).Ids[0] = 4242;

            GraphIssue issue = Only(GraphValidator.Validate(asset), GraphIssueCode.DanglingEdge);

            StringAssert.Contains("'Idle'.Tasks points to 4242", issue.Message);
        }

        [Test]
        public void Validator_NoBrain_IsAnError()
        {
            var root = new AgentState("Root");
            root.AddChild(new AgentState("Idle").AddTask(new WaitTask(1f)));
            var graph = new BehaviorGraph(root);
            graph.AssignIds();
            BehaviorGraphAsset asset = AssetOf(graph);

            GraphIssue issue = Only(GraphValidator.Validate(asset), GraphIssueCode.NoBrain);

            Assert.IsTrue(issue.IsError, "A character with the graph would do nothing at all");
            Assert.AreEqual(asset.RootId, issue.RecordId);
        }

        [Test]
        public void Validator_NotInChain_ABlockNoDecisionLeadsTo_IsAWarningNamingIt()
        {
            var g = new BrainGraphBuilder();
            g.Do(g.Always(), g.Block(new WaitTask(1f), "Reached"));
            AgentState never = g.Block(new WaitTask(1f), "Never");

            GraphIssue issue = Only(GraphValidator.Validate(AssetOf(g.Build())), GraphIssueCode.NotInChain);

            Assert.IsFalse(issue.IsError, "The graph runs; only this block never does");
            Assert.AreEqual(never.Id, issue.RecordId);
            StringAssert.Contains("'Never'", issue.Message);
        }

        [Test]
        public void Validator_TwoBlocks_ANodeOfTwoTasks_IsAnErrorNamingIt()
        {
            var g = new BrainGraphBuilder();
            AgentState both = g.Block(new WaitTask(1f), "Both");
            both.AddTask(new WaitTask(2f));
            g.Do(g.Always(), both);

            GraphIssue issue = Only(GraphValidator.Validate(AssetOf(g.Build())), GraphIssueCode.TwoBlocks);

            Assert.IsTrue(issue.IsError, "A node is one block: a chain shows the order");
            Assert.AreEqual(both.Id, issue.RecordId);
            StringAssert.Contains("'Both'", issue.Message);
        }

        [TestCase(OldModelAssets.REFLEX)]
        [TestCase(OldModelAssets.NESTED_NODE)]
        [TestCase(OldModelAssets.CONDITIONS_ON_AN_ARROW)]
        [TestCase(OldModelAssets.CONDITIONS_ON_ENTERING)]
        [TestCase(OldModelAssets.SIGNAL_ARROW)]
        [TestCase(OldModelAssets.CHANGE_ARROW)]
        public void Validator_APartOfTheOldModel_IsAnErrorNamingItsRecord_TheRestIsRead(string part)
        {
            BehaviorGraphAsset asset = NewAsset();
            int record = OldModelAssets.Write(asset, part);

            GraphIssue issue = Only(GraphValidator.Validate(asset), GraphIssueCode.OldModel);
            BehaviorGraph read = GraphSerializer.Read(asset, new List<GraphIssue>());

            Assert.IsTrue(issue.IsError);
            Assert.AreEqual(record, issue.RecordId);
            Assert.IsNotNull(read?.Brain, "What the old model left out, the graph has: it starts in its Brain");
        }

        [Test]
        public void Validator_UnreachableState_OutsideTheGraphsList()
        {
            BehaviorGraphAsset asset = AssetOf(Everything());
            var orphan = new AgentState("Orphan") { Id = 777 };
            asset.Records.Add(new GraphRecord { Id = 777, Type = "aikit.state", Data = JsonUtility.ToJson(orphan) });

            GraphIssue issue = Only(GraphValidator.Validate(asset), GraphIssueCode.UnreachableState);

            Assert.AreEqual(777, issue.RecordId);
        }

        [Test]
        public void Validator_EmptyState_IsAWarning()
        {
            var g = new BrainGraphBuilder();
            var nothing = new AgentState("Nothing");
            g.Root.AddChild(nothing);
            g.Do(g.Always(), nothing);

            GraphIssue issue = Only(GraphValidator.Validate(AssetOf(g.Build())), GraphIssueCode.EmptyState);

            Assert.IsFalse(issue.IsError);
            StringAssert.Contains("'Nothing'", issue.Message);
        }

        [Test]
        public void Validator_UnknownVariable_ATypoIsAnError_NotASecondVariable()
        {
            BehaviorGraphAsset asset = AssetOf(Everything());
            asset.Variables.RemoveAll(v => v.Name == PAUSE);

            List<GraphIssue> issues = GraphValidator.Validate(asset);

            List<GraphIssue> unknown = issues.Where(i => i.Code == GraphIssueCode.UnknownVariable).ToList();
            Assert.AreEqual(2, unknown.Count, "Both waits that read it say so: Glance and Dig");
            Assert.IsTrue(unknown.All(i => i.IsError && i.Message.Contains($"'{PAUSE}'")));
        }

        [Test]
        public void Validator_CleanGraph_NoIssues()
        {
            CollectionAssert.IsEmpty(GraphValidator.Validate(AssetOf(Everything())));
        }

        // ---------- renames ----------

        // The routine's one block: the renamed node
        private static BehaviorGraph Renamed(RenamedTestTask task)
        {
            var g = new BrainGraphBuilder();
            g.Do(g.Always(), g.Block(task, "Work"));
            return g.Build();
        }

        [Test]
        public void Rename_FieldWithFormerlySerializedAs_KeepsDataAndLinks()
        {
            BehaviorGraphAsset asset = AssetOf(Renamed(new RenamedTestTask { Seconds = 3f, Inner = new WaitTask(2f) }));
            GraphRecord record = asset.Records.First(r => r.Type == "aikit.tests.renamed");
            record.Data = record.Data.Replace("\"Seconds\"", "\"OldSeconds\"");     // as the asset was saved before
            record.Links[0].Field = "OldInner";

            BehaviorGraph read = asset.Compile();

            RenamedTestTask task = read.Root.Children.SelectMany(state => state.Tasks).OfType<RenamedTestTask>().Single();
            Assert.AreEqual(3f, task.Seconds);
            Assert.IsInstanceOf<WaitTask>(task.Inner);
            CollectionAssert.IsEmpty(asset.Issues);
        }

        [Test]
        public void Rename_FieldWithoutAlias_IsReportedByName()
        {
            BehaviorGraphAsset asset = AssetOf(Renamed(new RenamedTestTask { Inner = new WaitTask(2f) }));
            asset.Records.First(r => r.Type == "aikit.tests.renamed").Links[0].Field = "Vanished";

            GraphIssue issue = Only(GraphValidator.Validate(asset), GraphIssueCode.UnknownField);

            StringAssert.Contains("no link field 'Vanished'", issue.Message);
        }

        [Test]
        public void Rename_TheClass_TheTypeIdStays()
        {
            // The asset stores the [AIKitNode] id, not the class name: a renamed class with the same id still loads
            BehaviorGraphAsset asset = AssetOf(Everything());

            Assert.IsTrue(asset.Records.All(r => !r.Type.Contains("Task") && !r.Type.Contains("AIKit.")),
                "Records hold stable ids, not type names");
            Assert.AreEqual(typeof(WaitTask), AIKitNodeRegistry.Find("aikit.task.wait"));
        }
    }
}
