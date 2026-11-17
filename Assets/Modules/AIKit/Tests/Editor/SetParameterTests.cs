using System.Collections.Generic;
using AIKit.NavMeshNavigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AIKit.Tests
{
    /// <summary>
    /// Set Parameter (step 90, C11; spec 10): a multiple of a parameter of the body — run speed, turn speed, ramp
    /// distance — for as long as the decision whose chain set it is in charge; the decision leaves and the body has its
    /// own value back. The body stays the one owner of the value (no copy on the blackboard): the pace a block walks at
    /// stays a part of the speed, the acceleration worked out from the ramp distance follows it.
    /// </summary>
    public sealed class SetParameterTests
    {
        private const string TARGET = "Target";
        private const float DT = 0.05f;
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
            GraphRig.DestroyAll();
            LogAssert.ignoreFailingMessages = false;
        }

        // A character whose part keeps the body's numbers (Speed, Turn Speed, Acceleration — and the game's Morale)
        private AICharacter Numbered(out StatusTests.Numbers numbers)
        {
            var go = new GameObject("Numbered") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.SetActive(false);
            numbers = go.AddComponent<StatusTests.Numbers>();
            var character = go.AddComponent<AICharacter>();
            character.Simulation = AICharacter.SimulationMode.Manual;
            character.ThinkInterval = 0f;
            var actuator = new TestActuator();
            character.SetParts(actuator, new DirectNavigator(actuator));
            character.Initialize();
            return character;
        }

        [Test]
        public void SetParameter_RunSpeedTimes_LastsWhileTheDecisionIsInCharge_ThenTheBodysOwnComesBack()
        {
            AICharacter character = Numbered(out StatusTests.Numbers numbers);
            float own = numbers.Values[0];
            var target = new GameObject("Foe") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(target);
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            AgentState enraged = g.Decision(new WatchReasoner(), 1);
            AgentState faster = g.Block(new SetParameterTask { Parameter = BodyParameter.RunSpeed, Value = 1.5f });
            g.Do(enraged, faster);
            g.Then(faster, g.Block(new WaitTask(100f), "Enraged"));
            g.Do(g.Always(), g.Block(new WaitTask(100f), "Calm"));
            character.SetGraph(g.Build());

            Steps(character, 0.3f);
            Assert.AreEqual("Calm", character.State.DisplayName, "Set-up: nothing to be angry about");
            Assert.AreEqual(own, numbers.Values[0], "The body's own run speed");

            character.Blackboard.Set(TARGET, target.transform);
            Steps(character, 0.3f);
            Assert.AreEqual("Enraged", character.State.DisplayName);
            Assert.AreEqual(own * 1.5f, numbers.Values[0], 1e-4f, "Half as fast again, while the decision is in charge");
            Assert.IsTrue(character.TryGetStat(CharacterStat.RunSpeed, out float shown));
            Assert.AreEqual(own * 1.5f, shown, 1e-4f, "The status shows it as it is now");
            Assert.IsFalse(character.Blackboard.Has(BodyNumbers.RUN_SPEED), "No copy of it on the blackboard");

            character.Blackboard.Set(TARGET, (Transform)null);
            Steps(character, 0.6f);
            Assert.AreEqual("Calm", character.State.DisplayName);
            Assert.AreEqual(own, numbers.Values[0], 1e-4f, "The decision left: the body's own value is back");
        }

        [Test]
        public void SetParameter_TwiceInOneDecision_MultipliesTheSameBase_BackToBase_TheOwnValueAtOnce()
        {
            AICharacter character = Numbered(out StatusTests.Numbers numbers);
            float own = numbers.Values[1];
            var twice = new SetParameterTask { Parameter = BodyParameter.TurnSpeed, Value = 2f };
            var half = new SetParameterTask { Parameter = BodyParameter.TurnSpeed, Value = 0.5f };

            twice.Tick(character.Context, DT);
            Assert.AreEqual(own * 2f, numbers.Values[1], 1e-4f);
            half.Tick(character.Context, DT);
            Assert.AreEqual(own * 0.5f, numbers.Values[1], 1e-4f, "A multiple of the body's own value, not of the last one");

            new SetParameterTask { Parameter = BodyParameter.TurnSpeed, BackToBase = true }.Tick(character.Context, DT);
            Assert.AreEqual(own, numbers.Values[1], 1e-4f, "Back To Base: the body's own value now, whoever set it");
            Assert.IsFalse(character.Context.ParameterChanges.IsSet(BodyParameter.TurnSpeed));
        }

        [Test]
        public void SetParameter_ABodyWithoutTheParameter_ChangesNothing_AndIsDone()
        {
            var rig = new GraphRig(trace: false);
            var task = new SetParameterTask { Parameter = BodyParameter.RampDistance, Value = 3f };

            Assert.AreEqual(TaskStatus.Success, task.Tick(rig.Context, DT));
            Assert.AreEqual(TaskStatus.Success, task.Tick(null, DT), "No agent: nothing to change");
            StringAssert.Contains("decision", task.Requirement, "The node says how long it lasts");
        }

        [Test]
        public void SetParameter_NavMeshBody_RampDistanceTimes_TheAccelerationFollows_AndComesBack()
        {
            LogAssert.ignoreFailingMessages = true;          // a NavMeshAgent without a baked mesh complains
            var go = new GameObject("Character") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.SetActive(false);
            go.AddComponent<NavMeshAgent>();
            var body = new NavMeshBody { RampDistance = 0.5f, Acceleration = 8f };
            var character = go.AddComponent<AICharacter>();
            character.Body = body;
            character.Initialize();
            NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
            float gentle = agent.acceleration;

            var longer = new SetParameterTask { Parameter = BodyParameter.RampDistance, Value = 2f };
            longer.Tick(character.Context, DT);

            Assert.AreEqual(1f, body.RampDistance, 1e-4f);
            Assert.AreEqual(gentle * 0.5f, agent.acceleration, 1e-3f, "Twice the ramp: half the acceleration (a = v² / 2s)");
            body.Pathing.Pace = 0.4f;
            Assert.AreEqual(agent.speed * agent.speed / (2f * body.RampDistance), agent.acceleration, 1e-3f,
                "A pace works the acceleration out from the ramp it has now — the body is the one owner");

            new SetParameterTask { Parameter = BodyParameter.RampDistance, BackToBase = true }.Tick(character.Context, DT);
            longer.Tick(character.Context, DT);
            Assert.AreEqual(1f, body.RampDistance, 1e-4f);
        }

        private static void Steps(AICharacter character, float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / DT); i++)
                character.Step(DT);
        }
    }
}
