using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AIKit.Entities.Tests
{
    /// <summary>
    /// An AIKit agent on an Atomic entity made in code (no scene): what the installer puts on the entity, joining and
    /// leaving the world, the inputs (bound variables, think, hit) and the outputs (decision, target, walk).
    /// </summary>
    public sealed class AIAgentBehaviourTests
    {
        private const float DT = 0.02f;

        private GameObject _go;
        private AICharacter _character;
        private Entity _entity;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Agent");
            _character = _go.AddComponent<AICharacter>();
            _character.Body = new EntityBody();
            _entity = new Entity("Agent");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        private static Entity World(out AIAgentList agents)
        {
            var world = new Entity("World");
            agents = AIWorldInstaller.Install(world, new AIWorld(3));
            return world;
        }

        private AIAgentBehaviour Start(IEntity world = null)
        {
            AIAgentBehaviour behaviour = AIAgentInstaller.Install(_entity, _character, world);
            _entity.Init();
            _entity.Enable();
            return behaviour;
        }

        [Test]
        public void Install_PutsTheAgentsValuesOnTheEntity_AndKeepsTheGamesOwnWalk()
        {
            var walk = new ReactiveVariable<Vector3>();
            _entity.AddAgentMove(walk);                       // the game's walker, listed first

            AIAgentInstaller.Install(_entity, _character);

            Assert.IsTrue(_entity.HasAIAgentTag());
            Assert.AreSame(_character, _entity.GetAgentCharacter());
            Assert.AreSame(walk, _entity.GetAgentMove(), "the game's variable is kept");
            Assert.IsTrue(_entity.HasAgentFace());
            Assert.IsTrue(_entity.HasAgentDecision());
            Assert.IsTrue(_entity.HasAgentTarget());
            Assert.IsTrue(_entity.HasAgentThinkRequest());
            Assert.IsTrue(_entity.HasAgentHitRequest());
            Assert.IsTrue(_entity.HasAIHitTaken());
        }

        [Test]
        public void Init_JoinsTheWorld_Manual_AndDisposeLeavesIt()
        {
            Entity world = World(out AIAgentList agents);

            AIAgentBehaviour behaviour = Start(world);

            Assert.AreEqual(1, agents.Count);
            Assert.AreSame(agents, behaviour.Agents);
            Assert.AreEqual(AICharacter.SimulationMode.Manual, _character.Simulation);
            Assert.AreSame(agents.World, _character.World, "the character is in the world's scheduler and roster");

            _entity.Dispose();

            Assert.AreEqual(0, agents.Count);
            Assert.IsNull(_character.World);
        }

        [Test]
        public void TheWorldsTick_StepsTheAgent()
        {
            Entity world = World(out _);
            Start(world);
            world.Init();
            world.Enable();

            world.Tick(DT);
            world.Tick(DT);

            Assert.That(_character.Context.LocalTime, Is.EqualTo(2 * DT).Within(1e-5f));
        }

        [Test]
        public void NoWorld_TheEntitysFixedTickStepsIt()
        {
            AIAgentBehaviour behaviour = Start(new Entity("Not a world"));

            _entity.FixedTick(DT);

            Assert.IsNull(behaviour.Agents);
            Assert.That(_character.Context.LocalTime, Is.EqualTo(DT).Within(1e-5f));
        }

        [Test]
        public void AHitRequest_IsTakenOnTheNextStep()
        {
            AIAgentBehaviour behaviour = Start(new Entity("Not a world"));
            var attacker = new GameObject("Attacker").transform;
            try
            {
                _entity.GetAgentHitRequest().Invoke(new HitInfo(attacker, 3f));
                Assert.That(_character.Context.HurtAt, Is.LessThan(0f), "not before the step");

                behaviour.Step(DT);

                Assert.That(_character.Context.HurtAt, Is.GreaterThanOrEqualTo(0f));
                Assert.IsFalse(_entity.GetAgentHitRequest().Required, "consumed");
            }
            finally
            {
                Object.DestroyImmediate(attacker.gameObject);
            }
        }

        [Test]
        public void ABoundValue_ReachesTheGraphsVariable_WhenItChanges()
        {
            AIAgentBehaviour behaviour = Start(new Entity("Not a world"));
            var blasts = new ReactiveVariable<int>(0);
            behaviour.Bind(blasts, "BlastCount");

            behaviour.Step(DT);
            Assert.AreEqual(0, _character.Blackboard.Get("BlastCount", -1), "declared and copied before the first step");

            blasts.Value = 2;
            behaviour.Step(DT);
            Assert.AreEqual(2, _character.Blackboard.Get("BlastCount", -1));
        }

        [Test]
        public void TheDecision_IsShownOnTheEntity()
        {
            Entity world = World(out _);
            AIAgentBehaviour behaviour = Start(world);
            _character.SetGraph(BehaviorPresets.MeleeChaser(new BehaviorPresets.Tuning()));

            for (int i = 0; i < 10; i++)
                behaviour.Step(DT);

            Assert.That(_entity.GetAgentDecision().Value, Is.Not.Empty, "a preset always has a decision (Patrol)");
        }

        [Test]
        public void TheWalk_GoesIntoTheEntitysValues()
        {
            AIAgentBehaviour behaviour = Start(new Entity("Not a world"));
            _character.Context.Actuator.Move(Vector3.right);
            _character.Context.Actuator.Apply(DT);

            Assert.That(_entity.GetAgentMove().Value, Is.EqualTo(Vector3.right));
            Assert.That(_entity.GetAgentFace().Value, Is.EqualTo(Vector3.right));
        }
    }
}
