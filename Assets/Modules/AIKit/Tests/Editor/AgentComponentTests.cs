using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AIKit.Tests
{
    /// <summary>
    /// The character as a component: its parts come from its body (or SetParts), never from a search of the hierarchy.
    /// Components are driven by hand (Initialize/Step), so the tests do not depend on editor lifecycle callbacks.
    /// </summary>
    public class AgentComponentTests
    {
        private const float DT = 0.02f;

        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _objects.Count; i++)
            {
                if (_objects[i] != null)
                    Object.DestroyImmediate(_objects[i]);
            }

            _objects.Clear();
        }

        [Test]
        public void AICharacter_AddedFromMenuPath_IsTheAgentThatRunsTheGraph()
        {
            var go = new GameObject("Character") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                AICharacter character = go.AddComponent<AICharacter>();
                character.Body = new SimpleBody();
                character.Initialize();
                Assert.That(character.Context.Agent, Is.SameAs(character));
                var menu = (AddComponentMenu)System.Attribute.GetCustomAttribute(typeof(AICharacter), typeof(AddComponentMenu));
                Assert.That(menu.componentMenu, Is.EqualTo("AI Kit/AI Character"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private AICharacter CreateAgent(BehaviorPresets.Preset preset = BehaviorPresets.Preset.MeleeChaser)
        {
            var root = new GameObject("Agent");
            _objects.Add(root);

            AICharacter agent = root.AddComponent<AICharacter>();
            agent.Body = new SimpleBody();
            agent.Initialize();
            agent.SetGraph(BehaviorPresets.Build(preset, new BehaviorPresets.Tuning { PauseAtPoint = 0f }));
            return agent;
        }

        // Steps for this long: the graph starts in its Brain on the first think, the Brain decides on the next one
        private static void StepSeconds(AICharacter agent, float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / DT); i++)
                agent.Step(DT);
        }

        [Test]
        public void Agent_WithSimpleBody_MovesByItAndNavigatesStraight()
        {
            AICharacter agent = CreateAgent();

            Assert.IsInstanceOf<TransformMover>(agent.Context.Actuator, "The body's actuator");
            Assert.IsInstanceOf<DirectNavigator>(agent.Context.Navigation, "A body without a navigator: a direct one");
            Assert.IsNotNull(agent.Graph);
        }

        [Test]
        public void Agent_Step_RunsTheGraph()
        {
            AICharacter agent = CreateAgent();

            StepSeconds(agent, agent.ThinkInterval + 0.1f);

            Assert.AreEqual("Patrol", agent.State?.DisplayName, "Nothing seen: the Patrol decision's block walks");
        }

        [Test]
        public void Agent_WithTarget_EntersCombat()
        {
            AICharacter agent = CreateAgent();
            var target = new GameObject("Target");
            _objects.Add(target);
            target.transform.position = new Vector3(5f, 0f, 0f);
            agent.SetTarget(target.transform);

            StepSeconds(agent, agent.ThinkInterval + 0.1f);

            Assert.AreEqual("Chase", agent.State?.DisplayName, "The target out of reach: the Chase decision closes in");
        }

        [Test]
        public void Agent_NotThinking_KeepsItsState()
        {
            AICharacter agent = CreateAgent();
            StepSeconds(agent, agent.ThinkInterval + 0.1f);
            Assert.AreEqual("Patrol", agent.State?.DisplayName, "Nothing seen yet: it patrols");
            agent.IsThinking = false;

            var target = new GameObject("Target");
            _objects.Add(target);
            target.transform.position = new Vector3(2f, 0f, 0f);
            agent.SetTarget(target.transform);
            for (int i = 0; i < 20; i++)
                agent.Step(DT);

            Assert.AreEqual("Patrol", agent.State?.DisplayName, "A paused brain must not change its mind");
        }

        /// <summary>Presses the stick one way every tick it runs; an instant one presses once and is done.</summary>
        private sealed class PullTask : AgentTask
        {
            private readonly Vector3 _direction;
            private readonly bool _instant;

            public PullTask(Vector3 direction, string name, bool instant = false)
            {
                _direction = direction;
                _instant = instant;
                Name = name;
            }

            public override bool IsInstant => _instant;

            protected override TaskStatus OnTick(AgentContext context, float dt)
            {
                context.Actuator.Move(_direction);
                return _instant ? TaskStatus.Success : TaskStatus.Running;
            }
        }

        [Test]
        public void TwoTasksMovingInOneStep_AreReported()
        {
            AxisWriteDetector.ForgetReports();
            LogAssert.Expect(LogType.Warning, new Regex("built with no abilities"));   // the preset says it never attacks
            AICharacter agent = CreateAgent();
            // A block that pulls right and, on its On Tick pin, an instant block that pulls left: in the step of every
            // think (but the first in the block) both press the stick
            var g = new BrainGraphBuilder();
            AgentState routine = g.Always();
            AgentState right = g.Block(new PullTask(Vector3.right, "Pull Right"), "Pull Right");
            g.Do(routine, right);
            g.OnTick(right, g.Block(new PullTask(Vector3.left, "Pull Left", instant: true), "Pull Left"));
            agent.SetGraph(g.Build());

            LogAssert.Expect(LogType.Warning, new Regex("Pull Left and Pull Right both wrote the Move axis"));
            StepSeconds(agent, 4f * agent.ThinkInterval);       // several thinks: reported once per pair, not every step

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Agent_WithoutBody_SaysSo()
        {
            var root = new GameObject("Lonely");
            _objects.Add(root);
            root.SetActive(false);
            AICharacter agent = root.AddComponent<AICharacter>();

            LogAssert.Expect(LogType.Warning, new Regex("AI Character has no body"));
            agent.Initialize();

            Assert.IsNull(agent.Context.Actuator);
            Assert.AreSame(root.transform, agent.EntityRoot, "The character's own object is its entity");
        }

        [Test]
        public void Agent_BodyGivesNoActuator_BlamesTheBodyNotItsAbsence()
        {
            var root = new GameObject("Broken");
            _objects.Add(root);
            root.SetActive(false);
            AICharacter agent = root.AddComponent<AICharacter>();
            agent.Body = new NoPartsBody();

            LogAssert.Expect(LogType.Warning, new Regex("the body gave no actuator"));
            agent.Initialize();

            LogAssert.NoUnexpectedReceived();           // and not "has no body": it has one
            Assert.IsNull(agent.Context.Actuator);
        }

        /// <summary>A body that failed to make its parts (as a NavMesh Body without its NavMeshAgent).</summary>
        private sealed class NoPartsBody : AgentBody
        {
            public override void Attach(Transform owner)
            {
            }

            public override IAgentActuator Actuator => null;
        }

        [Test]
        public void Agent_ActuatorComponentOnTheObject_IsNotPickedUp()
        {
            // Parts come only from the body (or SetParts): a stray actuator component is not searched for
            var root = new GameObject("Agent");
            _objects.Add(root);
            root.SetActive(false);
            root.AddComponent<StrayActuator>();
            AICharacter agent = root.AddComponent<AICharacter>();
            agent.Body = new SimpleBody();

            agent.Initialize();

            Assert.IsInstanceOf<TransformMover>(agent.Context.Actuator);
        }

        /// <summary>An actuator as a component: the kind the character used to search its hierarchy for.</summary>
        private sealed class StrayActuator : MonoBehaviour, IAgentActuator
        {
            public AgentBodyInfo Body => default;

            public void Move(Vector3 direction, float speedScale = 1f) { }

            public void SetVertical(float axis) { }

            public void Face(Vector3 direction) { }

            public void Jump(float holdTime) { }

            public float HoldTimeForHeight(float rise) => 0f;

            public void Crouch(bool crouch) { }

            public void DropThrough() { }

            public void Stop() { }

            public void Apply(float dt) { }
        }
    }
}
