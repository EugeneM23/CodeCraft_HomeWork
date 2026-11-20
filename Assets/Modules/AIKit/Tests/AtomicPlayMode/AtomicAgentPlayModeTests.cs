using System.Collections;
using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AIKit.Entities.PlayModeTests
{
    /// <summary>
    /// The live check of the Atomic bridge (it replaces the elite Regax's sandbox test): a minimal Atomic world, a
    /// minimal Atomic agent (a <see cref="MonoEntity"/> with <see cref="AIAgentInstaller"/>, an AI Character with an
    /// <see cref="EntityBody"/>, a walker behaviour that moves it by <c>AgentMove</c>) and a target entity that is told
    /// of blows by <see cref="EntityHitReceiver"/>. The graph: Brain → Claw (Use Any Ability: a blow in reach) or Chase.
    /// The target is handed to the graph through the entity (a bound value), not written into the blackboard. Played by
    /// Unity's own loop: the world's entity ticks the agent, the agent's entity walks it.
    /// </summary>
    public sealed class AtomicAgentPlayModeTests
    {
        private const string TARGET = "Target";
        private const float SPEED = 5f;
        private const float REACH = 2f;

        /// <summary>The game's side of the walk in this test: AgentMove × speed, turned towards AgentFace.</summary>
        private sealed class WalkBehaviour : IEntityInit, IEntityTick
        {
            private Transform _transform;
            private IReactiveVariable<Vector3> _move;
            private IReactiveVariable<Vector3> _face;

            public void Init(IEntity entity)
            {
                _transform = ((Component)entity).transform;
                _move = entity.GetAgentMove();
                _face = entity.GetAgentFace();
            }

            public void Tick(IEntity entity, float deltaTime)
            {
                _transform.position += _move.Value * (SPEED * deltaTime);
                if (_face.Value != Vector3.zero)
                    _transform.rotation = Quaternion.LookRotation(_face.Value);
            }
        }

        private readonly List<Object> _made = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object made in _made)
                if (made != null)
                    Object.Destroy(made);
            _made.Clear();
        }

        private MonoEntity Entity(string name, Vector3 at)
        {
            MonoEntity entity = MonoEntity.Create(new MonoEntity.CreateArgs
            {
                name = name,
                installOnAwake = false,
                useUnityLifecycle = true,
                disposeValues = false
            });
            entity.transform.position = at;
            _made.Add(entity.gameObject);
            return entity;
        }

        private AbilityAsset Claw()
        {
            AbilityAsset claw = AbilityAsset.Create("Claw",
                new RangeCondition { Min = 0f, Max = REACH },
                new CooldownCondition { Seconds = 0.4f },
                new StrikeDelivery { Reach = REACH + 0.5f },
                new DamageEffect { Damage = 5f });
            claw.Timing = new AbilityTiming { WindUp = 0.1f, Recovery = 0.1f };
            claw.Queue.AtOnce = 0;
            _made.Add(claw);
            return claw;
        }

        private static BehaviorGraph ChaseAndClaw(AbilityAsset claw)
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            AgentState fight = g.Decision(new UseAnyAbilityReasoner(claw), 1, "Claw");
            g.Chain(fight, new UseAbilityTask(), new WaitTask(0.2f));
            AgentState chase = g.Decision(new ChaseReasoner(), 2, "Chase");
            g.Chain(chase, new ChaseTask { StopDistance = REACH * 0.6f });
            return g.Build();
        }

        [UnityTest]
        public IEnumerator AnAtomicAgent_ChasesTheTarget_AndClawsIt()
        {
            // The world: an entity that owns the AIKit world and steps its agents every frame
            MonoEntity world = Entity("[AIWorld]", Vector3.zero);
            world.gameObject.AddComponent<AIWorldInstaller>().Install(world);

            // The target: an entity told of AIKit blows (AIHitTaken), counted here as the game's damage would be
            MonoEntity target = Entity("Target", new Vector3(12f, 0f, 0f));
            var hitTaken = new Event<HitInfo>();
            target.AddAIHitTaken(hitTaken);
            target.gameObject.AddComponent<EntityHitReceiver>();
            int hits = 0;
            hitTaken.OnEvent += _ => hits++;

            // The agent: built inactive so its AI Character gets its body before it wakes
            MonoEntity agent = Entity("Agent", Vector3.zero);
            agent.gameObject.SetActive(false);
            var character = agent.gameObject.AddComponent<AICharacter>();
            character.Body = new EntityBody { Speed = SPEED };
            character.ThinkInterval = 0.05f;
            character.SetGraph(ChaseAndClaw(Claw()));
            agent.AddAgentSpeed(new Const<float>(SPEED));
            var installer = agent.gameObject.AddComponent<AIAgentInstaller>();
            installer.SetWorld(world);
            installer.Install(agent);
            agent.AddBehaviour(new WalkBehaviour());
            var targetValue = new ReactiveVariable<Transform>(target.transform);
            installer.Behaviour.Bind(targetValue, TARGET);     // the game hands the target over through the entity
            var decisions = new List<string>();
            agent.GetAgentDecision().OnEvent += decision => decisions.Add(decision);
            agent.gameObject.SetActive(true);

            float timeLeft = 8f;
            while (hits == 0 && timeLeft > 0f)
            {
                timeLeft -= Time.deltaTime;
                yield return null;
            }

            float distance = Vector3.Distance(agent.transform.position, target.transform.position);
            Assert.That(hits, Is.GreaterThan(0), $"the claw landed (distance {distance:0.0} m, decisions: {string.Join(", ", decisions)})");
            Assert.That(distance, Is.LessThanOrEqualTo(REACH + 0.5f), "it came up to the target");
            Assert.That(world.GetWorldAgentList().Count, Is.EqualTo(1), "the world steps it");
            Assert.That(character.Simulation, Is.EqualTo(AICharacter.SimulationMode.Manual));
            CollectionAssert.Contains(decisions, "Chase", "the entity showed the chase");
            CollectionAssert.Contains(decisions, "Claw", "and the attack");
            Assert.That(agent.GetAgentTarget().Value, Is.SameAs(target.transform), "the entity shows what it is after");

            // Death: the entity goes, the agent leaves the world
            Object.Destroy(agent.gameObject);
            yield return null;
            Assert.That(world.GetWorldAgentList().Count, Is.EqualTo(0), "it left the world when its entity was destroyed");
        }
    }
}
