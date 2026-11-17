using AIKit.NavMeshNavigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace AIKit.Tests.SensesCoverage
{
    /// <summary>
    /// Set Parameter in the corners (step-90 recheck): the graph stops and the body's own value is back, the next
    /// decision's multiple is of the body's own value, a value from a variable (and a negative one), a NavMesh body
    /// walking at a pace, and a chain taken again and again without allocating.
    /// </summary>
    public sealed class SensesParameterTests
    {
        private const string TARGET = "Target";
        private const float DT = 0.05f;

        private readonly Things _things = new();

        [TearDown]
        public void TearDown()
        {
            _things.DestroyAll();
            GraphRig.DestroyAll();
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void SetParameter_TheGraphStops_TheBodysOwnValueIsBack()
        {
            AICharacter character = Numbered(out StatusTests.Numbers numbers);
            float own = numbers.Values[0];
            character.SetGraph(Enraged(1.5f));
            character.Blackboard.Set(TARGET, Foe());
            Steps(character, 0.3f);
            Assume.That(numbers.Values[0], Is.EqualTo(own * 1.5f).Within(1e-4f), "Set-up: enraged, half as fast again");

            character.SetGraph(null);

            Assert.AreEqual(own, numbers.Values[0], 1e-4f, "The graph stopped: the decision left with it, the own value is back");
            Assert.IsFalse(character.Context.ParameterChanges.IsSet(BodyParameter.RunSpeed));
        }

        [Test]
        public void SetParameter_TheNextDecisionSetsItToo_ItMultipliesTheBodysOwnValue()
        {
            AICharacter character = Numbered(out StatusTests.Numbers numbers);
            float own = numbers.Values[0];
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            AgentState a = g.Decision(new WatchReasoner(), 1);
            AgentState aFaster = g.Block(new SetParameterTask { Parameter = BodyParameter.RunSpeed, Value = 1.5f });
            g.Do(a, aFaster);
            g.Then(aFaster, g.Block(new WaitTask(100f), "A"));
            AgentState b = g.Decision(new DailyRoutineReasoner(), 2);
            AgentState bFaster = g.Block(new SetParameterTask { Parameter = BodyParameter.RunSpeed, Value = 2f });
            g.Do(b, bFaster);
            g.Then(bFaster, g.Block(new WaitTask(100f), "B"));
            character.SetGraph(g.Build());
            Transform foe = Foe();

            character.Blackboard.Set(TARGET, foe);
            Steps(character, 0.3f);
            Assert.AreEqual("A", character.State.DisplayName);
            Assert.AreEqual(own * 1.5f, numbers.Values[0], 1e-4f);

            character.Blackboard.Set(TARGET, (Transform)null);
            Steps(character, 0.6f);
            Assert.AreEqual("B", character.State.DisplayName);
            Assert.AreEqual(own * 2f, numbers.Values[0], 1e-4f, "Twice the body's own value — not twice what A set");

            character.Blackboard.Set(TARGET, foe);
            Steps(character, 0.6f);
            Assert.AreEqual("A", character.State.DisplayName);
            Assert.AreEqual(own * 1.5f, numbers.Values[0], 1e-4f);
        }

        [Test]
        public void SetParameter_ValueFromAVariable_ANegativeOneIsZero()
        {
            AICharacter character = Numbered(out StatusTests.Numbers numbers);
            float own = numbers.Values[0];
            character.Blackboard.Set("Rage", 2f);

            new SetParameterTask { Parameter = BodyParameter.RunSpeed, Value = BlackboardValue<float>.Variable("Rage") }
                .Tick(character.Context, DT);
            Assert.AreEqual(own * 2f, numbers.Values[0], 1e-4f, "Times the variable's number");

            new SetParameterTask { Parameter = BodyParameter.RunSpeed, Value = -1f }.Tick(character.Context, DT);
            Assert.AreEqual(0f, numbers.Values[0], 1e-4f, "A negative multiple is none");

            new SetParameterTask { Parameter = BodyParameter.RunSpeed, BackToBase = true }.Tick(character.Context, DT);
            Assert.AreEqual(own, numbers.Values[0], 1e-4f);
        }

        [Test]
        public void SetParameter_NavMeshBody_ThePaceStaysAPart_TurnSpeedReachesTheAgent()
        {
            LogAssert.ignoreFailingMessages = true;          // a NavMeshAgent without a baked mesh complains
            var go = _things.Keep(new GameObject("Character") { hideFlags = HideFlags.HideAndDontSave });
            go.SetActive(false);
            NavMeshAgent agent = go.AddComponent<NavMeshAgent>();
            var body = new NavMeshBody();
            var character = go.AddComponent<AICharacter>();
            character.Body = body;
            character.Initialize();
            float full = agent.speed;
            float turn = body.TurnSpeed;
            body.Pathing.Pace = 0.4f;

            new SetParameterTask { Parameter = BodyParameter.RunSpeed, Value = 1.5f }.Tick(character.Context, DT);
            Assert.AreEqual(full * 1.5f * 0.4f, agent.speed, 1e-4f, "The pace stays a part of the faster speed");
            Assert.IsTrue(character.TryGetStat(CharacterStat.RunSpeed, out float run));
            Assert.AreEqual(full * 1.5f, run, 1e-4f, "The run speed is the full one");

            new SetParameterTask { Parameter = BodyParameter.TurnSpeed, Value = 2f }.Tick(character.Context, DT);
            Assert.AreEqual(turn * 2f, agent.angularSpeed, 1e-3f, "The turn speed reaches the agent");

            new SetParameterTask { Parameter = BodyParameter.RunSpeed, BackToBase = true }.Tick(character.Context, DT);
            new SetParameterTask { Parameter = BodyParameter.TurnSpeed, BackToBase = true }.Tick(character.Context, DT);
            Assert.AreEqual(full * 0.4f, agent.speed, 1e-4f, "Back: the own full speed at the pace");
            Assert.IsTrue(character.TryGetStat(CharacterStat.RunSpeed, out float back));
            Assert.AreEqual(full, back, 1e-4f);
            Assert.AreEqual(turn, agent.angularSpeed, 1e-3f);
        }

        [Test]
        public void SetParameter_InAChainTakenAgainAndAgain_AllocatesNothing()
        {
            AICharacter character = Numbered(out StatusTests.Numbers numbers);
            float own = numbers.Values[0];
            var g = new BrainGraphBuilder();
            g.Chain(g.Always(), new SetParameterTask { Parameter = BodyParameter.RunSpeed, Value = 1.5f }, new WaitTask(0.05f));
            character.SetGraph(g.Build());

            for (int i = 0; i < 200; i++)
                character.Step(0.02f);

            // Unity's own GC.Alloc counter (GC.GetAllocatedBytesForCurrentThread says 0 in the editor, whatever is made)
            Assert.That(() =>
            {
                for (int i = 0; i < 200; i++)
                    character.Step(0.02f);
            }, Is.Not.AllocatingGCMemory(), "Taken again and again, the decision and its Set Parameter allocate nothing");
            Assert.AreEqual(own * 1.5f, numbers.Values[0], 1e-4f, "Still half as fast again: the same base each time");
        }

        // ---------- helpers ----------

        // A character whose part keeps the body's numbers (Speed 6, Turn Speed 360, Acceleration 12)
        private AICharacter Numbered(out StatusTests.Numbers numbers)
        {
            var go = _things.Keep(new GameObject("Numbered") { hideFlags = HideFlags.HideAndDontSave });
            go.SetActive(false);
            numbers = go.AddComponent<StatusTests.Numbers>();
            var character = go.AddComponent<AICharacter>();
            character.Simulation = AICharacter.SimulationMode.Manual;
            character.ThinkInterval = 0f;
            character.IsTracing = false;
            var actuator = new TestActuator();
            character.SetParts(actuator, new DirectNavigator(actuator));
            character.Initialize();
            return character;
        }

        // Watch (1) → Set Parameter (Run Speed × times) → a long Wait "Enraged"; Daily Routine (2) → a long Wait "Calm"
        private static BehaviorGraph Enraged(float times)
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            AgentState enraged = g.Decision(new WatchReasoner(), 1);
            AgentState faster = g.Block(new SetParameterTask { Parameter = BodyParameter.RunSpeed, Value = times });
            g.Do(enraged, faster);
            g.Then(faster, g.Block(new WaitTask(100f), "Enraged"));
            g.Do(g.Always(), g.Block(new WaitTask(100f), "Calm"));
            return g.Build();
        }

        private Transform Foe() => _things.Make("Foe", new Vector3(3f, 0f, 0f));

        private static void Steps(AICharacter character, float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / DT); i++)
                character.Step(DT);
        }
    }
}
