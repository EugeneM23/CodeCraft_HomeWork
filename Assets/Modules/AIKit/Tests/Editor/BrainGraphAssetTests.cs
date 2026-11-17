using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AIKit.Tests
{
    /// <summary>
    /// The Brain-with-decisions scheme as an asset: what the check of the graph says about it (a Brain with no Target, a
    /// decision with nothing behind Do, equal priorities, "not from" a stranger, a pin that needs a wire, a decision no
    /// Brain takes, a second Brain), and that a save and a load keep everything — priorities, "not from", the Timer
    /// wire, Detection's shapes, a Branch's question — so the agent runs the asset exactly as the graph built in code.
    /// </summary>
    public sealed class BrainGraphAssetTests
    {
        private const string TARGET = "Target";
        private const string TIMER = "Grenade Timer";
        private const string COUNT = "Attacks";
        private const string NORMAL = "Normal";
        private const string SPECIAL = "Special";

        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
                Object.DestroyImmediate(created);
            _created.Clear();
            GraphRig.DestroyAll();
        }

        // ---------- the check of the graph ----------

        [Test]
        public void Validator_ABrainWithNoTargetWiredIn_IsAnError()
        {
            Skeleton graph = Build();
            ((CombatBrainTask)graph.Brain.Tasks[0]).Target = "";

            Assert.IsTrue(Issues(graph).Exists(issue => issue.Code == GraphIssueCode.UnwiredPin && issue.IsError &&
                                                        issue.RecordId == graph.Brain.Id));
        }

        [Test]
        public void Validator_ADecisionWithNothingBehindDo_IsAnError()
        {
            Skeleton graph = Build();
            graph.Chase.Transitions.Clear();

            Assert.IsTrue(Issues(graph).Exists(issue => issue.Code == GraphIssueCode.NoChain && issue.RecordId == graph.Chase.Id));
        }

        [Test]
        public void Validator_TwoDecisionsOfOnePriority_AreAWarning_OnBoth()
        {
            Skeleton graph = Build();
            graph.Chase.Reasoner.Priority = graph.Fight.Reasoner.Priority;

            List<GraphIssue> equal = Issues(graph).FindAll(issue => issue.Code == GraphIssueCode.EqualPriority);
            Assert.AreEqual(2, equal.Count);
            Assert.IsFalse(equal.Any(issue => issue.IsError), "It works — by the order they were made — so a warning");
        }

        [Test]
        public void Validator_NotFromSomethingThatIsNoDecisionOfTheBrain_IsAWarning()
        {
            Skeleton graph = Build();
            graph.Grenade.Reasoner.NotFrom.Add(graph.Strike.Id);

            Assert.IsTrue(Issues(graph).Exists(issue => issue.Code == GraphIssueCode.NotFromStranger && issue.RecordId == graph.Grenade.Id));
        }

        [Test]
        public void Validator_ADecisionNoBrainTakes_IsAWarning_NotUnreachable()
        {
            Skeleton graph = Build();
            graph.Brain.Transitions.RemoveAll(wire => wire.Target == graph.Chase);

            List<GraphIssue> issues = Issues(graph);
            Assert.IsTrue(issues.Exists(issue => issue.Code == GraphIssueCode.UnwiredPin && issue.RecordId == graph.Chase.Id && !issue.IsError));
            Assert.IsFalse(issues.Exists(issue => issue.Code == GraphIssueCode.UnreachableState && issue.RecordId == graph.Chase.Id),
                "A decision is taken by its Brain, not selected: it is only unused");
        }

        [Test]
        public void Validator_ASecondBrain_IsAnError()
        {
            Skeleton graph = Build();
            var second = new AgentState("Brain 2");
            second.AddTask(new CombatBrainTask { Target = TARGET });
            graph.Root.AddChild(second);

            Assert.IsTrue(Issues(graph).Exists(issue => issue.Code == GraphIssueCode.SecondBrain && issue.IsError &&
                                                        issue.RecordId == second.Id),
                "The graph runs its first Brain: the second one is a mistake");
        }

        [Test]
        public void Validator_TheWholeSkeleton_HasNoErrors()
        {
            List<GraphIssue> issues = Issues(Build());

            CollectionAssert.IsEmpty(issues.Where(issue => issue.IsError), string.Join(" | ", issues));
        }

        // ---------- save and load ----------

        [Test]
        public void Asset_KeepsPriorities_NotFrom_TheTimerWire_DetectionsShapes_AndTheBranchsQuestion()
        {
            Skeleton built = Build();
            BehaviorGraphAsset asset = Write(built);

            BehaviorGraph read = asset.Compile();

            CollectionAssert.IsEmpty(asset.Issues.Where(issue => issue.IsError));
            var nodes = new List<AgentNode>();
            read.VisitNodes(nodes.Add);
            AgentState grenade = read.Root.Children.Single(state => state.Id == built.Grenade.Id);
            var decision = (UseAbilityReasoner)grenade.Reasoner;
            Assert.AreEqual(2, decision.Priority);
            CollectionAssert.AreEqual(new[] { built.Fight.Id }, decision.NotFrom, "\"Not from\" keeps the ids of the decisions");
            Assert.AreEqual(TIMER, ((CompareCondition)decision.Condition).Variable, "Its pause: a Compare of the timer");
            DetectionTask detection = read.Root.Tasks.OfType<DetectionTask>().Single();
            Assert.AreEqual(2, detection.Sensors.Count, "Both shapes are wired into Detection again");
            BranchTask branch = nodes.OfType<BranchTask>().Single();
            Assert.IsInstanceOf<CompareCondition>(branch.Condition);
            Assert.AreEqual(COUNT, ((CompareCondition)branch.Condition).Variable);
        }

        [Test]
        public void Asset_TheAgentRunsItAsTheGraphBuiltInCode()
        {
            // The target is handed to the Brain's variable by the test: no Detection to look for tagged objects
            List<string> fromCode = Trace(Build(detection: false).Graph);
            List<string> fromAsset = Trace(Write(Build(detection: false)).Compile());

            Assert.Greater(fromCode.Count, 4, "Decisions were taken");
            CollectionAssert.AreEqual(fromCode, fromAsset);
        }

        // ---------- the graph ----------

        private sealed class Skeleton
        {
            public BehaviorGraph Graph;
            public AgentState Root;
            public AgentState Brain;
            public AgentState Fight;
            public AgentState Grenade;
            public AgentState Chase;
            public AgentState Patrol;
            public AgentState Strike;
        }

        // The lab skeleton in small: Detection → Brain; Fight (its chain counts the blows and picks the special one),
        // Use Ability (a grenade: not from Fight, its timer wired in), Chase, Patrol
        private static Skeleton Build(bool detection = true)
        {
            var g = new BrainGraphBuilder();
            if (detection)
                g.Sensor(new DetectionTask(new RadiusSensorTask { Radius = 6f }, new ConeSensorTask { Range = 10f })
                {
                    Tag = "Player", StoreIn = TARGET, Interval = 0.2f
                });
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            g.Variable(TIMER, VariableType.Float);
            g.Variable(COUNT, VariableType.Float);

            var s = new Skeleton { Root = g.Root, Brain = g.Brain };
            s.Fight = g.Decision(new FightReasoner(), 1);
            s.Grenade = g.Decision(new UseAbilityReasoner { Ability = GraphRig.Named("Grenade"), Condition = new CompareCondition(TIMER, NumberComparison.LessOrEqual, 0f) }, 2);
            s.Chase = g.Decision(new ChaseReasoner(), 3);
            s.Patrol = g.Decision(new PatrolReasoner(), 4);

            // Fight: Strike → Add → Branch (3 blows?) — yes: the special Strike → Set the count back; no: Strike again
            s.Strike = g.Block(new AttackTask { Move = NORMAL });
            AgentState count = g.Block(new AddNumberTask(COUNT));
            AgentState branch = g.Block(new BranchTask(new CompareCondition(COUNT, NumberComparison.GreaterOrEqual, 3f)));
            AgentState special = g.Block(new AttackTask { Move = SPECIAL });
            AgentState reset = g.Block(new SetNumberTask { Variable = COUNT, Value = 0f });
            g.Do(s.Fight, s.Strike);
            g.Then(s.Strike, count);
            g.Then(count, branch);
            g.Then(branch, special);
            g.OnFail(branch, s.Strike);
            g.Then(special, reset);

            g.Chain(s.Grenade, new UseAbilityTask(GraphRig.Named("Grenade")), new SetNumberTask { Variable = TIMER, Value = 6f });
            g.Chain(s.Chase, new ChaseTask { StopDistance = 1f });
            g.Chain(s.Patrol, new WaitTask(100f));

            s.Graph = g.Build();
            s.Grenade.Reasoner.NotFrom.Add(s.Fight.Id);
            return s;
        }

        private BehaviorGraphAsset Write(Skeleton graph)
        {
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _created.Add(asset);
            GraphSerializer.Write(graph.Graph, asset);
            return asset;
        }

        private List<GraphIssue> Issues(Skeleton graph)
        {
            graph.Graph.Link();
            graph.Graph.AssignIds();
            BehaviorGraphAsset asset = Write(graph);
            var issues = new List<GraphIssue>();
            LogAssert.ignoreFailingMessages = true;
            GraphValidator.Check(asset, GraphSerializer.Read(asset, issues), issues);
            return issues;
        }

        // The way a seeded agent goes through the graph: near, far, gone and back
        private static List<string> Trace(BehaviorGraph graph)
        {
            var rig = new GraphRig(seed: 7, trace: false) { ThinkInterval = 0f };
            rig.GiveWeapon(WeaponAsset.Create<WeaponAsset>(
                new WeaponMove { Name = NORMAL, Reach = 2f, WindUp = 0.3f, Recovery = 0.3f },
                new WeaponMove { Name = SPECIAL, Reach = 2.5f, WindUp = 0.5f, Recovery = 0.5f }));
            var victim = new GameObject("Victim") { hideFlags = HideFlags.HideAndDontSave };
            victim.AddComponent<HitProbe>();
            var path = new List<string>();
            try
            {
                rig.SetGraph(graph);
                rig.Agent.Blackboard.Set(TARGET, victim.transform);
                rig.Instance.StateChanged += (_, to) =>
                {
                    if (to != null)
                        path.Add($"{rig.Context.LocalTime:0.00} {to.DisplayName}");
                };
                foreach (float x in new[] { 1f, 8f, 1f, 30f })
                {
                    victim.transform.position = new Vector3(x, 0f, 0f);
                    rig.StepSeconds(2f, 0.05f);
                }
            }
            finally
            {
                Object.DestroyImmediate(victim);
            }

            return path;
        }
    }
}
