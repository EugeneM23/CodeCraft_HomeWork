using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AIKit.Tests
{
    /// <summary>
    /// Stage 3b, what later stages build on: seeds from the world and a random that saves, an urgent think, entering a
    /// node directly, sight asked once per think, and a graph that starts in its Brain — with none it does nothing.
    /// </summary>
    public class BrainContractTests
    {
        private const float THINK = 0.1f;
        private const string TARGET = "Target";

        private sealed class CountingSight : ISightProbe
        {
            public bool Clear = true;
            public int Calls;

            public bool HasLineOfSight(Vector3 from, Vector3 to)
            {
                Calls++;
                return Clear;
            }
        }

        /// <summary>A block that holds the agent for as long as its decision does.</summary>
        private sealed class Hold : AgentTask
        {
            protected override TaskStatus OnTick(AgentContext context, float dt) => TaskStatus.Running;
        }

        // The Brain's Target is handed over by the test: the variable a sensor would write
        private static void See(GraphRig rig) => rig.Context.Blackboard.Set(TARGET, rig.CreateTargetAt(Vector3.one));

        // --- seeds and random ---

        [Test]
        public void SeedZero_InAWorld_ComesFromTheWorld_AndReplays()
        {
            int[] SeedsOf(int worldSeed)
            {
                var world = new AIWorld(worldSeed);
                var a = new GraphRig(seed: 0, world: world);
                var b = new GraphRig(seed: 0, world: world);
                return new[] { a.Context.Seed, b.Context.Seed };
            }

            int[] first = SeedsOf(42);
            int[] again = SeedsOf(42);
            int[] other = SeedsOf(43);

            CollectionAssert.AreEqual(first, again, "The same world seed hands out the same agent seeds");
            Assert.AreNotEqual(first[0], first[1], "Two agents of one world do not share a seed");
            CollectionAssert.AreNotEqual(first, other);
        }

        [Test]
        public void SeedZero_WithoutAWorld_IsPickedAndKept()
        {
            var rig = new GraphRig(seed: 0);

            Assert.AreNotEqual(0, rig.Context.Seed, "The picked seed is kept, so the run can be replayed");
        }

        [Test]
        public void RandomState_SavedAndRestored_ContinuesTheSameSequence()
        {
            var rig = new GraphRig(seed: 7);
            rig.Context.NextRandom();
            ulong saved = rig.Context.RandomState;
            var expected = new List<double> { rig.Context.NextRandom(), rig.Context.NextRandom() };

            var loaded = new GraphRig(seed: 99);
            loaded.Context.RandomState = saved;

            Assert.AreEqual(expected[0], loaded.Context.NextRandom());
            Assert.AreEqual(expected[1], loaded.Context.NextRandom());
        }

        [Test]
        public void AgentRandom_Range_StaysInBounds()
        {
            var random = new AgentRandom(3);
            for (int i = 0; i < 1000; i++)
            {
                int value = random.Next(-2, 3);
                Assert.That(value, Is.InRange(-2, 2));
                Assert.That(random.NextDouble(), Is.InRange(0.0, 0.9999999999));
            }
        }

        // --- urgent think ---

        // Watch — the Brain sees its target: Alarmed; Daily Routine — the rest of the time: Calm
        private static BehaviorGraph AlarmGraph()
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            AgentState watch = g.Decision(new WatchReasoner(), 1);
            g.Do(watch, g.Block(new Hold(), "Alarmed"));
            AgentState routine = g.Always();
            g.Do(routine, g.Block(new Hold(), "Calm"));
            return g.Build();
        }

        [Test]
        public void RequestThink_DecidesOnTheNextStep()
        {
            var rig = new GraphRig { ThinkInterval = THINK };
            rig.SetGraph(AlarmGraph());
            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "Calm", 1f), "Nothing seen: the routine");

            // One more think on the short interval, then the next is ten seconds away; the routine is long past the
            // time a decision just taken is kept
            rig.ThinkInterval = 10f;
            rig.StepSeconds(BehaviorGraphInstance.MIN_DECISION_TIME + 2f * THINK);
            See(rig);
            rig.Step(0.02f, 5);
            Assert.AreEqual("Calm", rig.StateName, "Not due for ten seconds");

            rig.Agent.RequestThink();
            rig.Step();

            Assert.AreEqual("Alarmed", rig.StateName);
        }

        [Test]
        public void Brain_ThinkingAboutATarget_MakesNoFightRoundIt_UntilABlockAsksForAPlace()
        {
            // The Brain's facts ask whether the agent holds a place and who attacks first: questions, which must not
            // make an engagement — the world would step every target merely seen as a fight from then on
            var world = new AIWorld();
            var rig = new GraphRig(world: world) { ThinkInterval = THINK };
            rig.SetGraph(AlarmGraph());
            See(rig);
            rig.StepSeconds(1f);

            Assert.AreEqual("Alarmed", rig.StateName, "Set-up: the Brain thinks about the target");
            CollectionAssert.IsEmpty(world.Roster.Engagements, "No engagement round a target nobody asked a place of");
        }

        // --- entering a node directly ---

        // Fight — the target is seen: Strike (a random pause), then Recover; Idle — the rest of the time
        private static BehaviorGraph CombatGraph()
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            AgentState fight = g.Decision(new WatchReasoner(), 1, "Fight");
            AgentState strike = g.Block(new WaitTask(0.2f, 0.6f), "Strike");
            g.Do(fight, strike);
            g.Then(strike, g.Block(new WaitTask(0.3f), "Recover"));
            AgentState idle = g.Always("Idle");
            g.Do(idle, g.Block(new Hold(), "Stand"));
            return g.Build();
        }

        private static List<string> TraceAfter(GraphRig rig, int fromRecord)
        {
            var lines = new List<string>();
            for (int i = fromRecord; i < rig.Context.Trace.Count; i++)
            {
                DecisionRecord record = rig.Context.Trace[i];
                lines.Add($"{record.Kind} {record.From?.DisplayName} {record.To?.DisplayName}");
            }

            return lines;
        }

        [Test]
        public void EnterState_ADecision_PlaysTheSameAsTakenByTheBrain()
        {
            var byBrain = new GraphRig(seed: 5) { ThinkInterval = THINK };
            byBrain.SetGraph(CombatGraph());
            byBrain.Step();
            See(byBrain);
            int steps = 1;
            while (byBrain.StateName != "Strike" && steps < 100)
            {
                byBrain.Step();
                steps++;
            }

            Assert.AreEqual("Strike", byBrain.StateName, "Set-up: the Brain took Fight");
            int brainMark = byBrain.Context.Trace.Count;

            // The same agent, entered into the decision right before the step in which the Brain took it: same clock,
            // same think phase
            var entered = new GraphRig(seed: 5) { ThinkInterval = THINK };
            entered.SetGraph(CombatGraph());
            entered.Step();
            See(entered);
            for (int i = 1; i < steps - 1; i++)
                entered.Step();

            Assert.IsTrue(entered.Agent.EnterState("Fight"));
            Assert.AreEqual("Fight", entered.StateName);
            Assert.AreEqual("Fight", entered.Instance.Decision?.DisplayName, "The Brain is in charge of the decision");
            entered.Step();                                  // now both have taken the same steps
            Assert.AreEqual("Strike", entered.StateName, "Its chain runs as usual: Do leads on at the next think");
            int enterMark = entered.Context.Trace.Count;

            // Both are in Strike, entered at the same local time: what follows must read the same
            byBrain.StepSeconds(2f);
            entered.StepSeconds(2f);

            List<string> taken = TraceAfter(byBrain, brainMark);
            Assert.Greater(taken.Count, 2, "Set-up: the chain ran on and the Brain took the decision again");
            CollectionAssert.AreEqual(taken, TraceAfter(entered, enterMark));
        }

        [Test]
        public void EnterState_UnknownName_Throws()
        {
            var rig = new GraphRig();
            rig.SetGraph(CombatGraph());
            rig.Step();

            Assert.Throws<System.ArgumentException>(() => rig.Agent.EnterState("Nowhere"));
        }

        // --- sight once per think ---

        [Test]
        public void CanSeeTarget_AskedOncePerThink_HoweverManyAsk_EveryCallOutsideAThink()
        {
            var sight = new CountingSight { Clear = false };
            var rig = new GraphRig();
            rig.Context.Sight = sight;
            rig.CreateTargetAt(new Vector3(3f, 0f, 0f));

            rig.Context.BeginThink();
            for (int i = 0; i < 3; i++)
                Assert.IsFalse(rig.Context.CanSeeTarget());
            rig.Context.EndThink();
            Assert.AreEqual(1, sight.Calls, "One probe query per think, however many ask");

            rig.Context.BeginThink();
            rig.Context.CanSeeTarget();
            rig.Context.EndThink();
            Assert.AreEqual(2, sight.Calls, "The next think asks again");

            rig.Context.CanSeeTarget();
            rig.Context.CanSeeTarget();
            Assert.AreEqual(4, sight.Calls, "Outside a think every call asks the probe");
        }

        // --- the graph starts in its Brain ---

        [Test]
        public void Graph_WithoutABrain_DoesNothing_NotEvenItsSensors()
        {
            var rig = new GraphRig { ThinkInterval = THINK };
            var root = new AgentState("Root");
            root.AddTask(new DetectionTask(new RadiusSensorTask()) { StoreIn = "Seen", Interval = 0f });
            root.AddChild(new AgentState("Idle").AddTask(new Hold()));
            var graph = new BehaviorGraph(root);
            graph.AssignIds();

            LogAssert.Expect(LogType.Error, new Regex("has no Brain"));
            rig.SetGraph(graph);
            rig.StepSeconds(0.5f);

            Assert.IsFalse(rig.Instance.IsRunning, "The graph never starts");
            Assert.IsNull(rig.Instance.Current);
            Assert.AreEqual(-1, rig.Context.Blackboard.IndexOf("Seen"), "Its sensor never looked");
            Assert.IsFalse(rig.Agent.EnterState("Idle"), "No chain leads anywhere: nothing to enter");
            Assert.IsFalse(rig.Instance.IsRunning);
        }
    }
}
