using System.Collections.Generic;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Runs a graph through a real AICharacter — the same Step the game runs: decisions on the think interval, execution
    /// every step — but with a fake body and manual steps, so a whole fight takes milliseconds and always plays out
    /// the same way. The agent lives on a hidden object that is never saved; GraphRigCleanup removes them all.
    /// </summary>
    internal sealed partial class GraphRig
    {
        private static readonly List<GameObject> Created = new();
        private static readonly List<WeaponAsset> Weapons = new();

        private readonly List<TestAbilityRunner> _runners = new();

        private Transform _target;

        public GraphRig(int seed = 1234, bool trace = true, IAIWorld world = null, INavigator navigator = null,
            TestActuator actuator = null)
        {
            Actuator = actuator ?? new TestActuator();

            var go = new GameObject("GraphRig Agent") { hideFlags = HideFlags.HideAndDontSave };
            go.SetActive(false);
            Created.Add(go);

            Agent = go.AddComponent<AICharacter>();
            Agent.Simulation = AICharacter.SimulationMode.Manual;
            Agent.SetSeed(seed);
            Agent.IsTracing = trace;
            Agent.SetParts(Actuator, navigator ?? new DirectNavigator(Actuator));
            Agent.Initialize();
            Agent.SetWorld(world);
        }

        public AICharacter Agent { get; }

        public AgentContext Context => Agent.Context;

        public TestActuator Actuator { get; }

        public AgentAbilities Abilities => Agent.Abilities;

        public BehaviorGraphInstance Instance => Agent.Graph;

        public float ThinkInterval
        {
            get => Agent.ThinkInterval;
            set => Agent.ThinkInterval = value;
        }

        public string StateName => Instance?.Current?.DisplayName;

        /// <summary>Destroys every rig's agent object (and their targets) — run once after the tests.</summary>
        public static void DestroyAll()
        {
            for (int i = 0; i < Created.Count; i++)
            {
                if (Created[i] != null)
                    Object.DestroyImmediate(Created[i]);
            }

            Created.Clear();
            for (int i = 0; i < Weapons.Count; i++)
            {
                if (Weapons[i] != null)
                    Object.DestroyImmediate(Weapons[i]);
            }

            Weapons.Clear();
        }

        public void SetGraph(BehaviorGraph graph) => Agent.SetGraph(graph);

        /// <summary>The character holds this weapon from now on (the Attack node's moves); DestroyAll removes it.</summary>
        public void GiveWeapon(WeaponAsset weapon)
        {
            if (weapon != null && !Weapons.Contains(weapon))
                Weapons.Add(weapon);
            Agent.Weapon = weapon;
        }

        /// <summary>
        /// The character carries this weapon too (its Weapons list), with these models in the hand and on the back;
        /// DestroyAll removes the weapon.
        /// </summary>
        public CharacterWeapon Carry(WeaponAsset weapon, GameObject inHand = null, GameObject onBack = null)
        {
            if (weapon != null && !Weapons.Contains(weapon))
                Weapons.Add(weapon);
            var entry = new CharacterWeapon { Weapon = weapon, InHand = inHand, OnBack = onBack };
            Agent.Weapons.Add(entry);
            Agent.Weapon = Agent.Weapon;                // the models are shown where they are now
            return entry;
        }

        /// <summary>A weapon of one move, Normal, with these numbers (the numbers the node once had).</summary>
        public void GiveWeapon(float reach, float windUp, float recovery, float damage = 1f) =>
            GiveWeapon(WeaponAsset.Create<WeaponAsset>(new WeaponMove
            {
                Name = "Normal", Reach = reach, WindUp = windUp, Recovery = recovery, Damage = damage
            }));

        /// <summary>
        /// The smallest graph that runs this task: a Brain, one decision that always holds (Daily Routine) and the task
        /// as the one block of its chain, in a node with this name. The block done, the chain ends and the Brain takes
        /// the decision again — the block runs anew (a block that did nothing makes the decision rest a moment first).
        /// </summary>
        public static BehaviorGraph OneState(AgentTask task, string name = "Action")
        {
            var g = new BrainGraphBuilder();
            AgentState always = g.Always();
            AgentState node = g.Block(task, name);
            g.Do(always, node);
            return g.Build();
        }

        /// <summary>Scans the rig's target (and any extra candidates) — perception without physics.</summary>
        internal sealed class RigScanner : ITargetScanner
        {
            public readonly List<Transform> Candidates = new();

            public int Scan(Vector3 center, float radius, List<Transform> results)
            {
                results.Clear();
                foreach (Transform candidate in Candidates)
                {
                    if (candidate != null && (candidate.position - center).sqrMagnitude <= radius * radius)
                        results.Add(candidate);
                }

                return results.Count;
            }
        }

        internal RigScanner Scanner { get; private set; }

        /// <summary>
        /// Gives the agent senses (by default: looks every step, sees all around): from now on targets made by
        /// CreateTargetAt are perceived, not handed over.
        /// </summary>
        public AgentPerception GivePerception(PerceptionProfile profile = null)
        {
            Scanner = new RigScanner();
            if (_target != null)
                Scanner.Candidates.Add(_target);
            Context.Perception = new AgentPerception(Context,
                profile ?? PerceptionProfile.Create(p => { p.VisionIntervalMin = 0f; p.VisionIntervalMax = 0f; p.FocusHalfAngle = 180f; }),
                Scanner);
            return Context.Perception;
        }

        /// <summary>A live target the agent can see (a bare transform); DestroyTarget cleans it up.</summary>
        public Transform CreateTargetAt(Vector3 position)
        {
            if (_target == null)
            {
                var go = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
                Created.Add(go);
                _target = go.transform;
            }

            _target.position = position;
            if (Context.Perception != null)
            {
                if (!Scanner.Candidates.Contains(_target))
                    Scanner.Candidates.Add(_target);
                return _target;                         // perceived, not handed over
            }

            Context.Target = _target;
            Context.LastKnownTargetPosition = position;
            return _target;
        }

        public void MoveTargetTo(Vector3 position)
        {
            if (_target != null)
                _target.position = position;

            Context.LastKnownTargetPosition = position;
        }

        /// <summary>The target is gone, only the place it was last seen remains.</summary>
        public void DestroyTarget()
        {
            Context.Target = null;
            if (_target == null)
                return;

            Object.DestroyImmediate(_target.gameObject);
            _target = null;
        }

        /// <summary>Only the remembered position, no live target (searching, memory).</summary>
        public void RememberTargetAt(Vector3 position)
        {
            Context.Target = null;
            Context.LastKnownTargetPosition = position;
        }

        /// <summary>Gives the agent an ability that takes `duration` seconds to play.</summary>
        public TestAbilityRunner AddAbility(string id, float duration = 0f, float cooldown = 0.5f,
            float maxRange = 2f, float weight = 1f)
        {
            Abilities.Add(new AbilityDefinition
            {
                Id = id,
                Cooldown = cooldown,
                MaxRange = maxRange,
                Weight = weight,
                RequiresGround = false
            });

            var runner = new TestAbilityRunner(id, duration);
            Abilities.Register(runner);
            _runners.Add(runner);
            return runner;
        }

        /// <summary>Time without a graph: the agent's clock, the ability runners and their cooldowns.</summary>
        public void Tick(float dt)
        {
            Context.Advance(dt);
            TickRunners(dt);
            Abilities.Tick(dt);
        }

        /// <summary>Steps of the real agent; the test's ability runners play before it, as the body's skills would.</summary>
        public void Step(float dt = 0.02f, int steps = 1)
        {
            for (int i = 0; i < steps; i++)
            {
                TickRunners(dt);
                Agent.Step(dt);
            }
        }

        public void StepSeconds(float seconds, float dt = 0.02f) => Step(dt, Mathf.CeilToInt(seconds / dt));

        /// <summary>Steps until the condition holds (true) or the time is out (false) — §35.2 of the handbook.</summary>
        public bool StepUntil(System.Func<bool> condition, float maxSeconds, float dt = 0.02f)
        {
            int steps = Mathf.CeilToInt(maxSeconds / dt);
            for (int i = 0; i < steps; i++)
            {
                if (condition())
                    return true;

                Step(dt);
            }

            return condition();
        }

        private void TickRunners(float dt)
        {
            for (int i = 0; i < _runners.Count; i++)
                _runners[i].Tick(dt);
        }
    }
}
