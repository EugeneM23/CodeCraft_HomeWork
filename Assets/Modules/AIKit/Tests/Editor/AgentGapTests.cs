using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The corners of the agent and its small parts: an agent used before it woke (senses, a step, a world, a shout),
    /// the world's decision budget, a bus joined twice, the body without physics, the context without a body or a
    /// world, a patrol over points it cannot reach or around a loop, the debug label, and the value types and helpers
    /// a node leans on.
    /// </summary>
    public class AgentGapTests
    {
        private const float DT = 0.02f;

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

        /// <summary>A navigator whose answers and status the test sets.</summary>
        private sealed class FakeNavigator : INavigator
        {
            public NavVerdict Verdict = NavVerdict.Reachable;
            public float Remaining = 10f;

            public NavVerdict SetDestination(Vector3 point)
            {
                Destination = point;
                return Verdict;
            }

            public void Clear() => Status = NavStatus.Idle;

            public NavStatus Status { get; set; }

            public Vector3 Destination { get; private set; }

            public Vector3 SteeringTarget => Destination;

            public float RemainingDistance => Remaining;

            public NavEdgeKind CurrentEdge => NavEdgeKind.Jump;

            public void Step(float dt)
            {
            }
        }

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            return go;
        }

        /// <summary>An agent on a test body that has not been initialized yet.</summary>
        private (AICharacter Agent, TestActuator Body) Sleeping()
        {
            GameObject go = NewObject("Sleeping Agent");
            go.SetActive(false);
            var agent = go.AddComponent<AICharacter>();
            agent.Simulation = AICharacter.SimulationMode.Manual;
            var body = new TestActuator();
            agent.SetParts(body);
            return (agent, body);
        }

        // A Brain whose one decision waits in a block named Idle
        private static BehaviorGraph Waiting() => GraphRig.OneState(new WaitTask(100f), "Idle");

        // ---------- an agent used before it woke ----------

        [Test]
        public void Agent_SensedOrSteppedBeforeWaking_WakesItself()
        {
            (AICharacter sensed, _) = Sleeping();
            (AICharacter stepped, _) = Sleeping();

            sensed.Sense(DT);
            stepped.Step(DT);

            Assert.IsNotNull(sensed.Context);
            Assert.IsNotNull(stepped.Context);
            Assert.AreEqual(DT, stepped.Context.LocalTime, 1e-6f, "Time went on, nothing to decide");
        }

        [Test]
        public void Agent_JoinsAWorldBeforeWaking_GetsItsBlackboardWhenItWakes()
        {
            var world = new AIWorld(seed: 3);
            (AICharacter agent, _) = Sleeping();

            agent.SetWorld(world);
            Assert.IsNull(agent.Context);
            Assert.AreEqual(1, world.Roster.Count);

            agent.Initialize();
            Assert.AreSame(world.Blackboard, agent.Context.GlobalBlackboard);
        }

        [Test]
        public void Agent_HearsBeforeWaking_NothingHappens()
        {
            (AICharacter agent, _) = Sleeping();

            ((ISignalListener)agent).Hear(new Signal("Help", Vector3.zero, null, null));

            Assert.IsNull(agent.Context);
        }

        [Test]
        public void Agent_WithoutSenses_ListensAsItsFaction()
        {
            (AICharacter agent, _) = Sleeping();
            agent.Faction = 2;
            agent.Initialize();

            Assert.IsNull(agent.Context.Perception);
            Assert.AreEqual(2, ((ISignalListener)agent).Faction);
        }

        [Test]
        public void Agent_TheAbilitiesItsGraphNames_AreItsAbilities()
        {
            (AICharacter agent, _) = Sleeping();
            AbilityAsset kick = AbilityAsset.Create("Kick");
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(new UseAbilityReasoner { Ability = kick }), new UseAbilityTask());

            agent.Initialize();
            agent.SetGraph(g.Build());

            Assert.IsTrue(agent.Abilities.Has("Kick"));
            Object.DestroyImmediate(kick);
        }

        [Test]
        public void Agent_SameBusTwice_JoinsOnce()
        {
            var rig = new GraphRig(trace: false);
            rig.Agent.gameObject.SetActive(true);
            var bus = new SignalBus();

            rig.Agent.SetSignals(bus);
            rig.Agent.SetSignals(bus);

            Assert.AreEqual(1, bus.Count);
            Assert.AreSame(bus, rig.Context.Signals);
            rig.Agent.gameObject.SetActive(false);
        }

        [Test]
        public void World_DecisionBudgetSpent_TheNextAgentDecidesOnTheNextStep()
        {
            var world = new AIWorld { Scheduler = { MaxDecisionsPerStep = 1 } };
            var first = new GraphRig(trace: false, world: world);
            var second = new GraphRig(trace: false, world: world);
            first.SetGraph(Waiting());
            second.SetGraph(Waiting());

            world.BeginStep(DT);
            first.Step(DT);
            second.Step(DT);

            Assert.AreEqual(1, first.Agent.DecisionCount);
            Assert.AreEqual(0, second.Agent.DecisionCount, "Over the budget: postponed");
            Assert.AreEqual(1, world.Scheduler.PostponedThisStep);

            world.BeginStep(DT);
            second.Step(DT);
            Assert.AreEqual(1, second.Agent.DecisionCount, "Its turn on the next step");
        }

        // ---------- the body without physics ----------

        [Test]
        public void TransformMover_JumpsCrouchesAndDropsDoNothing_MovesOnlyWhereTold()
        {
            GameObject go = NewObject("Body");
            var body = new TransformMover(go.transform, new SimpleBody());

            body.Jump(0.3f);
            body.Crouch(true);
            body.DropThrough();
            body.SetVertical(1f);
            body.Apply(DT);

            Assert.AreEqual(Vector3.zero, go.transform.position);
            Assert.AreEqual(0f, body.HoldTimeForHeight(2f));
            Assert.IsFalse(body.Body.CanJump);

            body.Move(Vector3.right);
            body.Apply(1f);
            Assert.AreEqual(body.Body.MaxSpeed, go.transform.position.x, 1e-4f);
        }

        // ---------- the context without a body or a world ----------

        [Test]
        public void Context_NoBody_EyesAtItsTransform_NoWorld_NothingLimitsAttacks()
        {
            Transform self = NewObject("Self").transform;
            self.position = new Vector3(1f, 2f, 3f);
            var context = new AgentContext(null, null, 1) { Self = self };

            Assert.AreEqual(self.position, context.EyePosition);
            Assert.AreEqual(TokenResult.Granted, context.RequestToken("Melee"));
            Assert.DoesNotThrow(() => context.ReleaseAllTokens());
            Assert.IsNull(context.Roster);
        }

        // ---------- patrol ----------

        [Test]
        public void Patrol_PointItCannotReach_IsSkipped()
        {
            var rig = new GraphRig(trace: false);
            rig.Context.Navigation = new FakeNavigator { Verdict = NavVerdict.Unreachable };
            var patrol = new PatrolTask(new Vector3(5f, 0f, 0f), new Vector3(-5f, 0f, 0f));

            Assert.AreEqual(TaskStatus.Running, patrol.Tick(rig.Context, DT));

            Assert.AreEqual(1, patrol.CurrentIndexFor(rig.Context), "On to the next point");
        }

        [Test]
        public void Patrol_Loop_GoesRoundAndStartsAgain()
        {
            var rig = new GraphRig(trace: false);
            var patrol = new PatrolTask(new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f), new Vector3(3f, 0f, 0f))
            {
                PingPong = false, PauseAtPoint = 0f, ArriveDistance = 100f
            };

            var visited = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                patrol.Tick(rig.Context, DT);
                visited.Add(patrol.CurrentIndexFor(rig.Context));
            }

            CollectionAssert.AreEqual(new[] { 1, 2, 0, 1 }, visited);
        }

        [Test]
        public void Patrol_OnePoint_StaysAtIt()
        {
            var rig = new GraphRig(trace: false);
            var patrol = new PatrolTask(new Vector3(1f, 0f, 0f)) { PauseAtPoint = 0f, ArriveDistance = 100f };

            patrol.Tick(rig.Context, DT);
            patrol.Tick(rig.Context, DT);

            Assert.AreEqual(0, patrol.CurrentIndexFor(rig.Context));
        }

        // ---------- the debug label ----------

        private AgentDebugComponent DebugFor(AICharacter agent)
        {
            GameObject go = NewObject("Debug");
            go.SetActive(false);
            var debug = go.AddComponent<AgentDebugComponent>();
            typeof(AgentDebugComponent).GetField("_agent", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(debug, agent);
            return debug;
        }

        [Test]
        public void Debug_NoAgent_SaysNothing()
        {
            AgentDebugComponent debug = DebugFor(null);

            Assert.IsNull(debug.Agent);
            Assert.AreEqual(string.Empty, debug.Describe());
            Assert.DoesNotThrow(debug.SampleNavigation);
        }

        [Test]
        public void Debug_NoGraph_SaysSo_BrainOff_SaysThat()
        {
            var rig = new GraphRig(trace: false);
            AgentDebugComponent debug = DebugFor(rig.Agent);

            StringAssert.StartsWith("(no state)", debug.Describe());
            rig.Agent.IsThinking = false;
            StringAssert.StartsWith("(brain off)", debug.Describe());
        }

        [Test]
        public void Debug_RunningGraph_PathTasksBlackboardAndDecisions()
        {
            var rig = new GraphRig();
            AgentDebugComponent debug = DebugFor(rig.Agent);
            rig.SetGraph(Waiting());
            rig.Context.Blackboard.Set("Hp", 3);
            rig.Context.Blackboard.SetTag("Angry");
            rig.Context.Blackboard.Set("Gone", 1f);
            rig.Context.Blackboard.Remove("Gone");
            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "Idle", 1f), "The Brain led into the waiting block");
            rig.Agent.IsThinking = false;

            string text = debug.Describe();

            StringAssert.Contains("Root / Idle  (brain off)", text);
            StringAssert.Contains("  Wait running", text);        // step 90: a node is never called by its class
            StringAssert.Contains("Hp = 3", text);
            StringAssert.Contains("Angry (tag)", text);
            StringAssert.DoesNotContain("Gone", text, "A removed variable is not shown");
            StringAssert.Contains(rig.Context.Trace.Last.ToString(), text, "The last decision");
        }

        [Test]
        public void Debug_Navigation_StatusEdgeDistanceAndRefusals_KeepsTheLastFew()
        {
            var rig = new GraphRig(trace: false);
            var navigator = new FakeNavigator { Status = NavStatus.Following, Remaining = 4.2f };
            rig.Context.Navigation = navigator;
            AgentDebugComponent debug = DebugFor(rig.Agent);
            debug.ShowNavigation = true;
            Assert.IsTrue(debug.ShowNavigation);

            StringAssert.Contains($"nav: Following, Jump, {4.2f.ToString("0.0")} m", debug.Describe());

            for (int i = 0; i < 6; i++)
            {
                navigator.Status = NavStatus.Blocked;
                debug.SampleNavigation();
                debug.SampleNavigation();                       // the same refusal is noted once
                navigator.Status = NavStatus.Following;
                debug.SampleNavigation();
            }

            Assert.AreEqual(4, debug.Refusals.Count, "Only the last few are kept");
            StringAssert.Contains($"Blocked Jump at ({0f:0.0}, {0f:0.0})", debug.Describe());

            rig.Context.Navigation = null;
            StringAssert.Contains("nav: none", debug.Describe());
        }

        [Test]
        public void Debug_Perception_LevelAndAwareness_OrNoSenses()
        {
            var rig = new GraphRig(trace: false);
            AgentDebugComponent debug = DebugFor(rig.Agent);
            debug.ShowPerception = true;
            Assert.IsTrue(debug.ShowPerception);

            StringAssert.Contains("senses: none (target set by the game)", debug.Describe());

            rig.GivePerception();
            StringAssert.Contains($"alert: Idle, awareness {0f.ToString("0.00")}", debug.Describe());
        }

        [Test]
        public void Debug_Combat_NoFight_PhaseAndAxesOnly()
        {
            var rig = new GraphRig(trace: false);
            AgentDebugComponent debug = DebugFor(rig.Agent);
            debug.ShowCombat = true;

            string text = debug.Describe();

            StringAssert.Contains("phase: None", text);
            StringAssert.Contains("move: None -, face: None", text);
            StringAssert.DoesNotContain("tokens", text, "No world, no target: no tokens to show");
        }

        [Test]
        public void Debug_Combat_PhaseAxesAndTheTokensItHolds()
        {
            var world = new AIWorld();
            var rig = new GraphRig(trace: false, world: world);
            _created.Add(rig.Agent.gameObject);
            rig.Agent.gameObject.SetActive(true);
            var other = new GraphRig(trace: false, world: world);
            _created.Add(other.Agent.gameObject);
            other.Agent.gameObject.SetActive(true);
            Transform target = rig.CreateTargetAt(new Vector3(1f, 0f, 0f));
            AgentDebugComponent debug = DebugFor(rig.Agent);
            debug.ShowCombat = true;
            Assert.IsTrue(debug.ShowCombat);

            Engagement engagement = world.Roster.EngagementOf(target);
            engagement.Tokens.Request(rig.Agent, "Melee", 1, 0f, 1f);
            engagement.Tokens.Request(other.Agent, "Ranged", 1, 0f, 1f);
            engagement.Tokens.Request(other.Agent, "Ranged", 1, 0f, 1f);
            world.Roster.Step(DT);
            rig.Context.Arbiter.TryClaim("Owner", BodyAxis.Move, MoveAuthority.Path);

            string text = debug.Describe();

            StringAssert.Contains("phase: None", text);
            StringAssert.Contains("move: Path Owner, face: None", text);
            StringAssert.Contains($"tokens Melee: 1/{engagement.CapacityOf("Melee")} (holds)", text);
            StringAssert.Contains($"tokens Ranged: 1/{engagement.CapacityOf("Ranged")}\n", text, "Held by another");

            engagement.Tokens.Release(other.Agent, "Ranged");
            engagement.Tokens.Request(other.Agent, "Ranged", 1, 0f, 1f);
            StringAssert.DoesNotContain("Ranged", debug.Describe(), "Only asked for: not shown");
            rig.DestroyTarget();
        }

        // ---------- small parts ----------

        [Test]
        public void BlackboardVariable_VersionAndMissingNames()
        {
            var board = new Blackboard();
            var variable = new BlackboardVariable<float>();
            board.Set("Hp", 1f);
            int before = variable.Version(board, "Hp");

            board.Set("Hp", 2f);

            Assert.Greater(variable.Version(board, "Hp"), before);
            Assert.AreEqual(0, variable.Version(null, "Hp"));
            Assert.IsFalse(variable.TryGet(board, "", out _));
            Assert.DoesNotThrow(() => variable.Set(null, "Hp", 3f));
            Assert.AreEqual(2f, board.Get("Hp", 0f));
        }

        [Test]
        public void BlackboardValue_EqualWhenTheyReadTheSameThing()
        {
            BlackboardValue<float> a = BlackboardValue<float>.Variable("Hp");
            BlackboardValue<float> b = BlackboardValue<float>.Variable("Hp");
            BlackboardValue<float> constant = 1f;

            Assert.IsTrue(a.Equals((object)b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.IsFalse(a.Equals((object)constant));
            Assert.IsFalse(a.Equals("Hp"));
        }

        [Test]
        public void Keys_None_IsNotValid()
        {
            Assert.IsFalse(TagKey.None.IsValid);
            Assert.IsTrue(new TagKey(0).IsValid);
            Assert.IsFalse(BlackboardKey<float>.None.IsValid);
            Assert.IsTrue(new Blackboard().DeclareTag("Stunned").IsValid);
            Assert.AreEqual(-1, Blackboard.NameToId(""));
            Assert.AreEqual(-1, Blackboard.NameToId(null));
        }

        [Test]
        public void Curves_HyperbolicConstantAndUnknown()
        {
            Assert.AreEqual(1f, ResponseCurve.Hyperbolic().Evaluate(0f), 1e-5f);
            Assert.Less(ResponseCurve.Hyperbolic().Evaluate(1f), ResponseCurve.Hyperbolic().Evaluate(0.5f));
            Assert.AreEqual(0.3f, ResponseCurve.Constant(0.3f).Evaluate(0.9f), 1e-5f);
            Assert.AreEqual(0.4f, new ResponseCurve { Type = (CurveType)999 }.Evaluate(0.4f), 1e-5f, "Unknown: straight");
        }

        [Test]
        public void Random_EmptyRange_GivesItsMinimum()
        {
            var random = new AgentRandom(5);

            Assert.AreEqual(3, random.Next(3, 3));
            Assert.AreEqual(3, random.Next(3, 1));
        }

        [Test]
        public void World_Suspicion_IsTheStepsAlert_UntilTheNextStep()
        {
            var world = new AIWorld();

            world.ReportAlert(AlertLevel.Suspicious);
            world.ReportAlert(AlertLevel.Idle);
            world.BeginStep();
            Assert.AreEqual(AlertLevel.Suspicious, world.Alert);

            world.BeginStep();
            Assert.AreEqual(AlertLevel.Idle, world.Alert);
        }

        [Test]
        public void Cheats_Registered_AreListed()
        {
            GameObject prefab = NewObject("GapGoblin");
            AICheats.Register("GapGoblin", prefab);
            try
            {
                Assert.IsTrue(AICheats.SpawnableNames.Contains("GapGoblin"));
            }
            finally
            {
                AICheats.Unregister("GapGoblin");
            }

            Assert.IsFalse(AICheats.SpawnableNames.Contains("GapGoblin"));
        }

        [Test]
        public void DirectNavigator_SteersStraightAtTheDestination_OnFoot()
        {
            var navigator = new DirectNavigator(new TestActuator());

            navigator.SetDestination(new Vector3(3f, 0f, 0f));

            Assert.AreEqual(new Vector3(3f, 0f, 0f), navigator.SteeringTarget);
            Assert.AreEqual(NavEdgeKind.Walk, navigator.CurrentEdge);
        }
    }
}
