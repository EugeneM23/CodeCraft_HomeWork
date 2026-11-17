using System;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The edges of the core: nodes without their parts (no context, no navigator, no target), the guards of the graph
    /// instance and the blackboard, the small value types (scores, records, ranges) and the scheduler budget. Each test
    /// is a behaviour a user meets when something is missing — a missing part fails quietly, a wrong call says why.
    /// </summary>
    public class EdgeCaseTests
    {
        private const float DT = 0.02f;

        /// <summary>A task that answers what the test sets.</summary>
        private sealed class StatusTask : AgentTask
        {
            public TaskStatus Answer = TaskStatus.Running;

            protected override TaskStatus OnTick(AgentContext context, float dt) => Answer;
        }

        private sealed class FixedCondition : AgentCondition
        {
            public bool Value;

            public FixedCondition(bool value) => Value = value;

            protected override bool Evaluate(AgentContext context) => Value;
        }

        /// <summary>A navigator whose status after a step the test decides.</summary>
        private sealed class ScriptedNavigator : INavigator
        {
            public NavStatus AfterStep = NavStatus.Following;
            public float Remaining = 10f;
            public int Steps;
            public int Sets;

            public NavVerdict SetDestination(Vector3 point)
            {
                Sets++;
                Destination = point;
                if (!Status.IsMoving() && Status != NavStatus.Unreachable)
                    Status = NavStatus.Following;
                return Status == NavStatus.Unreachable ? NavVerdict.Unreachable : NavVerdict.Reachable;
            }

            public void Clear() => Status = NavStatus.Idle;

            public NavStatus Status { get; private set; }

            public Vector3 Destination { get; private set; }

            public Vector3 SteeringTarget => Destination;

            public float RemainingDistance => Remaining;

            public NavEdgeKind CurrentEdge => NavEdgeKind.Walk;

            public void Step(float dt)
            {
                Steps++;
                Status = AfterStep;
            }
        }

        private static GraphRig RigWithTarget(Vector3 target, INavigator navigator = null)
        {
            var rig = new GraphRig(navigator: navigator);
            rig.CreateTargetAt(target);
            return rig;
        }

        // ---------- movement nodes without their parts ----------

        [Test]
        public void MoveTo_WithoutANavigator_Fails()
        {
            Assert.AreEqual(TaskStatus.Failure, new MoveToTask().Tick(null, DT));
        }

        [Test]
        public void MoveTo_Follow_ReadsTheDestinationEveryStep_AndSucceedsWithinStopDistance()
        {
            var navigator = new ScriptedNavigator();
            var rig = new GraphRig(navigator: navigator);
            rig.Context.Blackboard.Set("Spot", new Vector3(5f, 0f, 0f));
            var move = new MoveToTask
            {
                Destination = BlackboardValue<Vector3>.Variable("Spot"),
                Follow = true,
                StopDistance = 1f
            };

            Assert.AreEqual(TaskStatus.Running, move.Tick(rig.Context, DT));
            int sets = navigator.Sets;
            move.Tick(rig.Context, DT);
            Assert.Greater(navigator.Sets, sets, "Follow asks again every step");

            navigator.Remaining = 0.5f;
            Assert.AreEqual(TaskStatus.Success, move.Tick(rig.Context, DT));
        }

        [Test]
        public void MoveTo_Interrupted_StopsTheBody()
        {
            var navigator = new ScriptedNavigator();
            var rig = new GraphRig(navigator: navigator);
            var move = new MoveToTask { Destination = BlackboardValue<Vector3>.Constant(new Vector3(5f, 0f, 0f)) };
            move.Tick(rig.Context, DT);
            int stops = rig.Actuator.Stops;

            move.Interrupt(rig.Context);

            Assert.AreEqual(stops + 1, rig.Actuator.Stops);
            Assert.AreEqual(NavStatus.Idle, navigator.Status);
        }

        [Test]
        public void MoveTo_NavigatorGivesUp_Fails()
        {
            var navigator = new ScriptedNavigator { AfterStep = NavStatus.Unreachable };
            var rig = new GraphRig(navigator: navigator);
            var move = new MoveToTask
            {
                Destination = BlackboardValue<Vector3>.Constant(new Vector3(8f, 0f, 0f)),
                StopDistance = 0.5f
            };

            Assert.AreEqual(TaskStatus.Running, move.Tick(rig.Context, DT), "On its way: the navigator takes a step");
            Assert.AreEqual(TaskStatus.Failure, move.Tick(rig.Context, DT), "The navigator gave up: there is no way there");
        }

        // ---------- abilities ----------

        [Test]
        public void UseAbility_WithoutAbilities_OrUnknownId_Fails()
        {
            GraphRig rig = RigWithTarget(new Vector3(1f, 0f, 0f));

            Assert.AreEqual(TaskStatus.Failure, new UseAbilityTask(GraphRig.Named("Swing")).Tick(null, DT));
            Assert.AreEqual(TaskStatus.Failure, new UseAbilityTask(GraphRig.Named("Missing")).Tick(rig.Context, DT));
            rig.DestroyTarget();
        }

        [Test]
        public void UseAbility_NotWaiting_SucceedsAtOnce()
        {
            GraphRig rig = RigWithTarget(new Vector3(1f, 0f, 0f));
            rig.AddAbility("Swing", duration: 1f);
            var use = new UseAbilityTask(GraphRig.Named("Swing")) { WaitForFinish = false };

            Assert.AreEqual(TaskStatus.Success, use.Tick(rig.Context, DT));
            Assert.AreEqual("Swing", use.UsedBy(rig.Context));
            rig.DestroyTarget();
        }

        [Test]
        public void Abilities_AnAbilityInUse_IsWhatRuns_AndTheLastUsed()
        {
            GraphRig rig = RigWithTarget(new Vector3(1f, 0f, 0f));
            rig.AddAbility("Swing", duration: 1f);
            Assert.IsFalse(rig.Abilities.IsAnyRunning, "Nothing runs yet");

            Assert.IsTrue(rig.Abilities.TryUse("Swing", rig.Context));

            Assert.IsTrue(rig.Abilities.IsAnyRunning);
            Assert.AreEqual("Swing", rig.Abilities.Running);
            Assert.AreEqual("Swing", rig.Abilities.LastUsed);
            rig.DestroyTarget();
        }

        [Test]
        public void Abilities_RunnerWithoutDefinition_GetsOne_AndClearForgetsAll()
        {
            var rig = new GraphRig();
            rig.Abilities.Register(new TestAbilityRunner("Bare"));

            Assert.IsTrue(rig.Abilities.Has("Bare"));
            Assert.AreEqual(1, rig.Abilities.Count);
            Assert.AreEqual(0f, rig.Abilities.Score("Missing", rig.Context));

            rig.Abilities.Clear();
            Assert.AreEqual(0, rig.Abilities.Count);
            Assert.IsNull(rig.Abilities.LastUsed);
        }

        [Test]
        public void Abilities_AddWithoutId_IsIgnored()
        {
            var rig = new GraphRig();

            rig.Abilities.Add(new AbilityDefinition());
            rig.Abilities.Add(null);

            Assert.AreEqual(0, rig.Abilities.Count);
        }

        [Test]
        public void AbilityDefinition_RangeShortcuts_AndNames()
        {
            var definition = new AbilityDefinition { Id = "Swing", MinRange = 1f, MaxRange = 3f };

            Assert.AreEqual(1f, definition.Range.Min);
            Assert.AreEqual(3f, definition.MaxRange);
            Assert.AreEqual(1f, definition.MinRange);
            Assert.AreEqual("Swing", definition.ToString());
            Assert.AreEqual("<ability>", new AbilityDefinition().ToString());
            Assert.AreEqual("1..3", definition.Range.ToString());
        }

        // ---------- conditions and arrows ----------

        [Test]
        public void AnyCondition_TrueWhenOneHolds_EmptyHasNoData()
        {
            Assert.IsTrue(new AnyCondition(new FixedCondition(false), new FixedCondition(true)).Check(null));
            Assert.IsFalse(new AnyCondition(new FixedCondition(false)).Check(null));
            // Step-90 recheck: an OR with nothing wired in has no data (it was "true" before the three-valued answers)
            Assert.AreEqual(Truth.NoData, new AnyCondition().Ask(null));
        }

        [Test]
        public void Arrow_ToNothing_IsPassedOver_AndSaysSo()
        {
            var rig = new GraphRig();
            var g = new BrainGraphBuilder();
            AgentState always = g.Always();
            AgentState work = g.Block(new StatusTask { Answer = TaskStatus.Success }, "Work");
            g.Do(always, work);
            work.AddTransition(new Transition { Trigger = TransitionTrigger.TasksFinished });     // a wire to nothing
            g.OnEnd(work, g.Block(new StatusTask(), "Next"));
            rig.SetGraph(g.Build());

            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "Next", 1f), "The next arrow leads on");
            StringAssert.Contains("nowhere", work.Transitions[0].ToString(), "A target-less arrow reads as going nowhere");
        }

        // ---------- utility ----------

        [Test]
        public void Utility_NullAxisSkipped_OptionalWithoutData_HasNoSay()
        {
            var rig = new GraphRig();
            var optional = new Consideration
            {
                Input = ConsiderationInput.DistanceToTarget,
                Required = false
            };

            bool scored = UtilityMath.Evaluate(new[] { null, optional }, rig.Context, ScoreReduction.Product,
                out UtilityScore score, out Consideration veto);

            Assert.IsTrue(scored);
            Assert.IsNull(veto);
            Assert.AreEqual(1f, (float)score, "Nothing counted: the neutral score");
        }

        [Test]
        public void Utility_Normalize_EqualBounds_IsAStep()
        {
            Assert.AreEqual(1f, UtilityMath.Normalize(2f, 2f, 2f));
            Assert.AreEqual(0f, UtilityMath.Normalize(1f, 2f, 2f));
        }

        [Test]
        public void Consideration_RandomInput_DrawsFromTheAgent_DistanceWithoutTarget_HasNoData()
        {
            var rig = new GraphRig();
            var random = new Consideration { Input = ConsiderationInput.Random };
            var distance = new Consideration { Input = ConsiderationInput.DistanceToTarget, RequiresTarget = false };

            Assert.IsTrue(random.Evaluate(rig.Context, out UtilityScore score));
            Assert.That((float)score, Is.InRange(0f, 1f));
            Assert.IsTrue(random.Evaluate(null, out _), "Without an agent: the middle");
            Assert.IsFalse(distance.Evaluate(rig.Context, out _), "No target, no distance — not \"very far\"");
        }

        [Test]
        public void UtilityScore_ComparesAndPrints()
        {
            UtilityScore low = 0.25f;
            UtilityScore high = 0.75f;

            Assert.Less(low.CompareTo(high), 0);
            Assert.IsTrue(low.Equals((object)(UtilityScore)0.25f));
            Assert.AreEqual(((UtilityScore)0.25f).GetHashCode(), low.GetHashCode());
            Assert.AreEqual("0.250", low.ToString());
        }

        [Test]
        public void ResponseCurve_Logistic_RisesThroughTheMiddle()
        {
            ResponseCurve curve = ResponseCurve.Logistic(0.5f, 10f);

            Assert.Less(curve.Evaluate(0.2f), curve.Evaluate(0.8f));
        }

        [Test]
        public void RandomSelection_NothingAboveTheThreshold_PicksNothing()
        {
            var strategy = new RandomSelection(0.5f);

            Assert.AreEqual(-1, strategy.Select(new[] { 0.1f, 0.2f }, null));
            Assert.AreEqual(1, strategy.Select(new[] { 0.1f, 0.9f }, null));
        }

        // ---------- records and the trace ----------

        [Test]
        public void DecisionRecord_EqualsItsCopy_AndPrintsEveryKind()
        {
            var state = new AgentState("Idle");
            var record = new DecisionRecord(1f, DecisionKind.Start, to: state);

            Assert.IsTrue(record.Equals((object)new DecisionRecord(1f, DecisionKind.Start, to: state)));
            Assert.AreEqual(new DecisionRecord(1f, DecisionKind.Start, to: state).GetHashCode(), record.GetHashCode());
            foreach (DecisionKind kind in Enum.GetValues(typeof(DecisionKind)))
                Assert.IsNotEmpty(new DecisionRecord(1f, kind, state, state, abilityId: "A", detail: "d").ToString());
        }

        [Test]
        public void DecisionTrace_Full_DropsTheOldest_AndClears()
        {
            var trace = new DecisionTrace(2);
            trace.Add(new DecisionRecord(1f, DecisionKind.Start));
            trace.Add(new DecisionRecord(2f, DecisionKind.Start));
            trace.Add(new DecisionRecord(3f, DecisionKind.Start));

            Assert.AreEqual(2, trace.Capacity);
            Assert.AreEqual(2, trace.Count);
            Assert.AreEqual(3, trace.TotalRecorded);
            Assert.AreEqual(2f, trace[0].Time, "The oldest kept");
            Assert.AreEqual(3f, trace.Last.Time);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = trace[2]);

            trace.Clear();
            Assert.AreEqual(0, trace.Count);
            Assert.AreEqual(default(DecisionRecord), trace.Last);
        }

        // ---------- graph instance guards ----------

        // The Brain, Daily Routine, and its chain: Idle → Busy (both at work for ever); Locked — a node no chain reaches
        private static GraphRig RigWithGraph(out AgentState idle, out AgentState busy)
        {
            var rig = new GraphRig();
            var g = new BrainGraphBuilder();
            AgentState always = g.Always();
            idle = g.Block(new StatusTask(), "Idle");
            busy = g.Block(new StatusTask(), "Busy");
            g.Do(always, idle);
            g.Then(idle, busy);
            g.Block(new StatusTask(), "Locked");
            rig.SetGraph(g.Build());
            return rig;
        }

        [Test]
        public void EnterState_WrongArguments_SayWhy()
        {
            GraphRig rig = RigWithGraph(out _, out _);
            BehaviorGraphInstance instance = rig.Instance;

            Assert.Throws<ArgumentNullException>(() => instance.EnterState((AgentState)null, rig.Context));
            Assert.Throws<ArgumentException>(() => instance.EnterState(new AgentState("Stranger"), rig.Context));
            Assert.Throws<ArgumentException>(() => instance.EnterState(-12345, rig.Context));
            Assert.Throws<ArgumentException>(() => instance.EnterState("Nobody", rig.Context));
            Assert.IsNotNull(instance.Graph);
        }

        [Test]
        public void EnterState_BeforeTheGraphRuns_Enters_AndStartChangesNothing()
        {
            GraphRig rig = RigWithGraph(out _, out AgentState busy);

            Assert.IsTrue(rig.Instance.EnterState(busy, rig.Context));
            Assert.AreEqual("Busy", rig.StateName);

            rig.Instance.Start(rig.Context);                // already running: nothing changes
            Assert.AreEqual("Busy", rig.StateName);
        }

        [Test]
        public void EnterState_ANodeNoChainReaches_IsRefused()
        {
            GraphRig rig = RigWithGraph(out _, out _);

            Assert.IsFalse(rig.Instance.EnterState("Locked", rig.Context), "It never runs: there is nothing to enter");
            Assert.IsFalse(rig.Instance.IsRunning);
        }

        [Test]
        public void Agent_GuardsAndPlainSettings()
        {
            var rig = new GraphRig();

            Assert.Throws<InvalidOperationException>(() => rig.Agent.SetParts(rig.Actuator, null),
                "Parts are set before Initialize");
            rig.Agent.ThinkInterval = 0.25f;
            Assert.AreEqual(0.25f, rig.Agent.ThinkInterval);
            rig.Agent.IsTracing = false;
            Assert.IsFalse(rig.Agent.IsTracing);
            Assert.AreSame(rig.Agent, rig.Context.Agent);
        }

        // ---------- graph helpers ----------

        [Test]
        public void AgentState_ParentAndNullAdds()
        {
            var root = new AgentState("Root");
            var child = new AgentState("Child");
            root.AddChild(child);

            Assert.AreSame(root, child.Parent);
            Assert.AreSame(root, root.AddChild(null));
            Assert.IsNull(root.AddTransition(null));
        }

        [Test]
        public void AssignIds_WalksIntoWhatTheBlocksHold()
        {
            var root = new AgentState("Root");
            var shape = new RadiusSensorTask();
            root.AddTask(new DetectionTask(shape));
            var anyChild = new FixedCondition(true);
            var any = new AnyCondition(anyChild);
            root.AddChild(new AgentState("Branch").AddTask(new BranchTask(any)));
            var graph = new BehaviorGraph(root);

            graph.AssignIds();

            Assert.AreNotEqual(0, shape.Id, "A shape of Detection");
            Assert.AreNotEqual(0, any.Id, "The condition of a Branch");
            Assert.AreNotEqual(0, anyChild.Id, "A condition of an OR");
        }

        // ---------- blackboard edges ----------

        [Test]
        public void Blackboard_TagsByKey_IndexQueries_AndGuards()
        {
            var board = new Blackboard();
            board.SetTag("Angry");
            board.Set("Health", 3);

            Assert.IsTrue(board.TryGetTagKey("Angry", out TagKey tag));
            Assert.IsTrue(board.HasTag(tag));
            Assert.IsFalse(board.TryGetTagKey("Calm", out _));

            int index = board.IndexOf("Health");
            Assert.AreEqual("Health", board.NameOf(index));
            Assert.IsTrue(board.HasValueAt(index));
            Assert.IsFalse(board.IsTagAt(index));
            Assert.IsTrue(board.IsTagAt(board.IndexOf("Angry")));
            Assert.AreEqual(3, board.BoxedValueOf(index));
            Assert.AreEqual(typeof(int), board.TypeOf(index));
            Assert.IsNull(board.NameOf(99));
            Assert.IsFalse(board.HasValueAt(-1));
            Assert.IsNull(board.BoxedValueOf(99));
            Assert.IsNull(board.TypeOf(99));

            Assert.IsTrue(board.RemoveTag(tag));
            Assert.IsFalse(board.HasTag("Angry"));

            board.Set(new BlackboardKey<int>(99), 1);              // a key of no slot: ignored
            Assert.Throws<ArgumentException>(() => board.Ref(new BlackboardKey<float>(index)));
            Assert.Throws<ArgumentException>(() => board.Set("", 1));
        }

        [Test]
        public void BlackboardValue_DescribesItself()
        {
            BlackboardValue<float> variable = BlackboardValue<float>.Variable("Speed");
            var constant = BlackboardValue<float>.Constant(2f);

            Assert.AreEqual("Speed", variable.Name);
            Assert.AreEqual(BlackboardScope.Agent, variable.Scope);
            Assert.AreEqual("$Speed", variable.ToString());
            Assert.AreEqual("2", constant.ToString());
        }

        // ---------- perception, scheduler, presets ----------

        [Test]
        public void TargetTrack_Clear_ForgetsEverything()
        {
            var rig = new GraphRig();
            rig.RememberTargetAt(new Vector3(3f, 0f, 0f));

            rig.Context.Track.Clear();

            Assert.IsFalse(rig.Context.HasTarget);
            Assert.AreEqual(0f, rig.Context.Track.Awareness);
            Assert.AreEqual(Vector3.zero, rig.Context.LastKnownTargetPosition);
            Assert.IsFalse(rig.Context.CanSeeTarget(), "Nothing to see");
        }

        [Test]
        public void Scheduler_RegisterTwice_Once_BudgetPostpones_ClearForgets()
        {
            var rigA = new GraphRig();
            var rigB = new GraphRig();
            var scheduler = new AgentScheduler { MaxDecisionsPerStep = 1 };

            scheduler.Register(rigA.Agent);
            scheduler.Register(rigA.Agent);
            scheduler.Register(rigB.Agent);
            scheduler.BeginStep();

            Assert.IsTrue(scheduler.TryBeginDecision());
            Assert.IsFalse(scheduler.TryBeginDecision(), "Over the budget");
            Assert.AreEqual(1, scheduler.PostponedThisStep);

            scheduler.Clear();
            Assert.IsFalse(scheduler.Contains(rigA.Agent));
        }

        [Test]
        public void Presets_EveryPresetBuildsAGraphWithABrain()
        {
            var tuning = new BehaviorPresets.Tuning();
            foreach (BehaviorPresets.Preset preset in Enum.GetValues(typeof(BehaviorPresets.Preset)))
            {
                BehaviorGraph graph = BehaviorPresets.Build(preset, tuning);
                Assert.IsNotNull(graph.Root, preset.ToString());
                Assert.IsNotNull(graph.Brain, $"{preset}: a graph without a Brain does nothing");
            }
        }

        [Test]
        public void Patrol_Route_PingPongsAndLoops()
        {
            var rig = new GraphRig();
            var patrol = new PatrolTask { PauseAtPoint = 0f, ArriveDistance = 0.5f };
            patrol.SetRoute(new[] { new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f), new Vector3(3f, 0f, 0f) },
                rig.Context);
            Assert.AreEqual(0, patrol.CurrentIndexFor(rig.Context));

            var visited = new System.Collections.Generic.List<int>();
            for (int i = 0; i < 2000 && visited.Count < 5; i++)
            {
                int before = patrol.CurrentIndexFor(rig.Context);
                patrol.Tick(rig.Context, DT);
                rig.Context.Navigation?.Step(DT);
                rig.Actuator.Apply(DT);
                if (patrol.CurrentIndexFor(rig.Context) != before)
                    visited.Add(patrol.CurrentIndexFor(rig.Context));
            }

            CollectionAssert.AreEqual(new[] { 1, 2, 1, 0, 1 }, visited, "Ping-pong along the route");

            patrol.PingPong = false;
            patrol.SetRoute(new[] { Vector3.zero, Vector3.zero }, rig.Context);
            patrol.Tick(rig.Context, DT);
            patrol.Tick(rig.Context, DT);
            Assert.That(patrol.CurrentIndexFor(rig.Context), Is.InRange(0, 1), "A loop wraps round");
        }

        [Test]
        public void Patrol_WithoutAContext_Fails()
        {
            Assert.AreEqual(TaskStatus.Failure, new PatrolTask(Vector3.one).Tick(null, DT));
        }
    }
}
