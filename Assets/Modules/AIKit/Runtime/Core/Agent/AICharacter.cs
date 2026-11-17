using System;
using Unity.Profiling;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// The orchestrator of an AI entity: holds the blackboard, the graph instance, the context, the abilities, and
    /// drives them. A step is: cooldowns, a decision when its time has come (and the world's scheduler allows it), the
    /// execution of the active states, then the buffered presses go to the body. Decisions can be thinned out and
    /// capped while execution runs every step, so movement never stutters.
    /// Exactly one driver steps an agent: in FixedUpdate mode the agent itself, in Manual mode its owner (a game loop
    /// component, a test rig). The scheduler never steps agents, so a double step is impossible by construction.
    /// The parts come from the character's own sections, never from a search of the hierarchy: the body (or SetParts
    /// in tests), the abilities its graph names; its picture is not its business (an AI Animator reads it, step 90). The
    /// game gives only a target filter (ITargetFilter) and its own numbers (IAgentNumbers) as components of the character.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-150)]
    [AddComponentMenu("AI Kit/AI Character")]
    public sealed partial class AICharacter : MonoBehaviour, ISignalListener, IWeaponRack
    {
        public enum SimulationMode
        {
            /// <summary>Steps itself in FixedUpdate.</summary>
            FixedUpdate,

            /// <summary>The owner calls Step (a game loop, tests).</summary>
            Manual
        }

        private static readonly ProfilerMarker ThinkMarker = new("AIKit.Think");
        private static readonly ProfilerMarker TickMarker = new("AIKit.Tick");
        private static readonly ProfilerMarker ApplyMarker = new("AIKit.Apply");

        [Header("Simulation")]
        [Tooltip("FixedUpdate — steps itself; Manual — the owner calls Step(dt)")]
        [SerializeField] private SimulationMode _simulation;

        [Tooltip("Seconds between decisions; execution runs every step")]
        [Min(0f)]
        [SerializeField] private float _thinkInterval = 0.1f;

        [Tooltip("0 — derived from the world's seed (or picked and kept in Context.Seed without a world); a fixed seed makes the agent's decisions repeatable")]
        [SerializeField] private int _seed;

        [Tooltip("On — decisions are recorded into Context.Trace")]
        [SerializeField] private bool _trace;

        [Tooltip("The character's side: a signal from another side is not heard, and a Senses sensor takes nobody of the " +
                 "same side for a target (0 — no side)")]
        [SerializeField] private int _faction;

        [Header("Body")]
        [Tooltip("What moves the character and what it sees with")]
        [SerializeReference] private AgentBody _body;

        [Header("Combat")]
        [Tooltip("The weapon in the hand at the start: the Attack node strikes with its moves; empty — it does not strike")]
        [SerializeField] private WeaponAsset _weapon;

        [Tooltip("Every weapon the character carries, with its model in the hand and on the back (the one in the hand " +
                 "shows In Hand, the others On Back); the Switch Weapon node takes one up")]
        [SerializeField] private System.Collections.Generic.List<CharacterWeapon> _weapons = new();

        // The sections that have files of their own: AICharacter.Abilities.cs, .Status.cs, .Hits.cs, .WorldWeapon.cs,
        // .Behavior.cs, .Weapons.cs, .Hands.cs, .Inside.cs (and .Legacy.cs: the old Animation and Senses data, for the
        // migration only). The character knows nothing of its picture (step 90, C22): the AI Animator reads it.

        private BehaviorGraphInstance _instance;
        private IAgentActuator _actuatorOverride;
        private INavigator _navigatorOverride;
        private float _thinkTimer;
        private bool _initialized;

        /// <summary>Previous leaf state → new leaf state.</summary>
        public event Action<AgentState, AgentState> StateChanged;

        public Blackboard Blackboard { get; } = new();

        public AgentContext Context { get; private set; }

        public AgentAbilities Abilities { get; } = new();

        public BehaviorGraphInstance Graph => _instance;

        /// <summary>The leaf state the agent is in; null — no graph yet.</summary>
        public AgentState State => _instance?.Current;

        /// <summary>The character's own object: what the body moves and what others see as this agent.</summary>
        public Transform EntityRoot { get; private set; }

        /// <summary>The world the agent shares facts and the decision budget with; null — the agent is alone.</summary>
        public IAIWorld World { get; private set; }

        /// <summary>False — the brain is paused: no decisions, the body keeps what it was told (cutscenes, tests).</summary>
        public bool IsThinking { get; set; } = true;

        /// <summary>How many decisions the agent has made (the scheduler tests and the debugger read it).</summary>
        public int DecisionCount { get; private set; }

        public SimulationMode Simulation
        {
            get => _simulation;
            set => _simulation = value;
        }

        public float ThinkInterval
        {
            get => _thinkInterval;
            set => _thinkInterval = Mathf.Max(0f, value);
        }

        /// <summary>The body the character is made of (before Initialize); SetParts wins over its parts.</summary>
        public AgentBody Body
        {
            get => _body;
            set
            {
                if (_initialized)
                    throw new InvalidOperationException($"{name}: the body is set before Initialize");
                _body = value;
            }
        }

        /// <summary>
        /// The character's own weapon in the hand (the Attack node's moves); set while it runs, it strikes with it at once
        /// and its model is shown in the hand. While a weapon taken from the world is held (a rock), it waits on the back
        /// and Context.Weapon is that one.
        /// </summary>
        public WeaponAsset Weapon
        {
            get => _weapon;
            set
            {
                _weapon = value;
                _shownInHand = value;
                ShowWeapons();
                if (_worldWeapon != null)
                    return;                     // behind a weapon taken from the world: on the back, its clips not shown

                if (Context != null)
                    Context.Weapon = value;
                ShowMotionsOf(value);
            }
        }

        /// <summary>The seed set on this agent in the inspector (0 — none: the world gives one).</summary>
        public int Seed => _seed;

        /// <summary>Joins a signal bus (null — leaves it): the agent hears the bus's signals and can shout on it.</summary>
        public void SetSignals(ISignalBus bus)
        {
            Initialize();
            if (Context.Signals == bus)
                return;

            Context.Signals?.Unsubscribe(this);
            Context.Signals = bus;
            if (isActiveAndEnabled)
                bus?.Subscribe(this);
        }

        Transform ISignalListener.Root => EntityRoot != null ? EntityRoot : transform;

        Vector3 ISignalListener.ListenPosition => Context != null ? Context.Position : transform.position;

        /// <summary>
        /// The character's side (the Advanced section): a signal from another side is not heard, and a Senses sensor
        /// takes nobody of the same side for a target. 0 — no side.
        /// </summary>
        public int Faction
        {
            get => _faction;
            set => _faction = value;
        }

        int ISignalListener.Faction => _faction;

        void ISignalListener.Hear(in Signal signal)
        {
            if (Context == null)
                return;

            Context.HearSignal(signal);
            Context.Perception?.Hear(signal);
            RequestThink();
        }

        public bool IsTracing
        {
            get => _trace;
            set
            {
                _trace = value;
                if (Context != null)
                    Context.TraceEnabled = value;
            }
        }

        private void Awake() => Initialize();

        private void OnEnable()
        {
            if (Context != null)
                Context.Signals?.Subscribe(this);
            if (_initialized)
                _body?.OnEnable();         // the body's parts exist only after Initialize attached it
        }

        private void OnDisable()
        {
            Context?.Signals?.Unsubscribe(this);
            _instance?.Stop(Context);
            Context?.Actuator?.Stop();
            Abilities.Cancel();
            LetGoWhenSwitchedOff();             // a rock, a manned gun: a dead hand holds nothing (AICharacter.WorldWeapon.cs)
            if (_initialized)
                _body?.OnDisable();
        }

        private void OnDestroy()
        {
            SetWorld(null);
            _instance?.Release(Context);    // the graph is free for whoever builds on it next
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _body?.FrameTick(dt);
            StowWhileClimbing();                    // the climb starts in the body's frame: the sword goes back the same frame
        }

        private void FixedUpdate()
        {
            if (_simulation != SimulationMode.FixedUpdate)
                return;

            // An agent that drives itself in a scene (no game gave it a world) joins the scene's world before its first
            // think — what its Brain's start would do, a think later: the scheduler then spreads its first decision
            // (handbook §27.3: no saw of a wave that woke up together) and a seed of 0 is the world's before the first
            // draw (§29: one seed, one trace). A game's loop (Manual) gives its agents their world itself
            if (DecisionCount == 0 && World == null && _instance?.Graph.Brain != null)
                AIWorldRunner.Join(this);

            Step(Time.fixedDeltaTime);
        }

        /// <summary>
        /// Parts given in code (tests, agents built without a body); call before Initialize. They win over the body's.
        /// </summary>
        public void SetParts(IAgentActuator actuator, INavigator navigator = null)
        {
            if (_initialized)
                throw new InvalidOperationException($"{name}: SetParts must come before Initialize");

            _actuatorOverride = actuator;
            _navigatorOverride = navigator;
        }

        /// <summary>
        /// Builds the context from the character's own sections: the parts come from SetParts or the body, the abilities
        /// from its graph (SetGraph). Called by Awake, safe to call earlier from a test.
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            _body?.Attach(transform);
            EntityRoot = transform;
            IAgentActuator actuator = _actuatorOverride ?? _body?.Actuator;
            if (actuator == null && _body == null)
            {
                Debug.LogWarning($"{name}: AI Character has no body — the agent will decide but cannot move. Pick a " +
                                 "body in its Body section.", this);
            }
            else if (actuator == null)
            {
                Debug.LogWarning($"{name}: the body gave no actuator — see the body's error above. The agent will " +
                                 "decide but cannot move.", this);
            }

            Context = new AgentContext(this, Blackboard, _seed != 0 ? _seed : World?.NextAgentSeed() ?? 0)
            {
                Self = transform,
                Actuator = actuator,
                Abilities = Abilities,
                TraceEnabled = _trace,
                GlobalBlackboard = World?.Blackboard
            };

            INavigator navigator = _navigatorOverride ?? _body?.Navigator;
            // The questions perception asks the world's physics — whatever the body answers them with
            Context.Sight = _body?.Sight;
            Context.Ground = _body?.Ground;
            Context.Walk = _body?.Walk ?? actuator as IWalkProbe;
            Context.Hands = this;
            Context.Navigation = navigator ?? (actuator != null ? new DirectNavigator(actuator, Context.Ground) : null);
            Context.Weapon = _weapon;
            Context.Rack = this;
            Context.Launcher = _body?.Launcher;
            // What a Senses sensor of the graph looks with, and which targets count (the game's filter, if any)
            Context.Scanner = _body?.Scanner;
            Context.TargetFilter = GetComponentInChildren<ITargetFilter>(true);
            _shownInHand = _weapon;
            MotionsWeapon = _weapon;
            ShowWeapons();

            if (Context.Navigation is IAgentBound bound)
                bound.Bind(Context);


            InitializeObjects();
            if (_graph != null)
                SetGraph(_graph.Compile());
        }

        /// <summary>
        /// Joins a world: its global blackboard becomes the agent's global scope and its scheduler rations the agent's
        /// decisions. Null leaves the current world.
        /// </summary>
        public void SetWorld(IAIWorld world)
        {
            if (World == world)
                return;

            World?.Scheduler.Unregister(this);
            World?.Roster.Remove(this);
            World = world;
            World?.Scheduler.Register(this);
            World?.Roster.Add(this);
            if (Context == null)
                return;

            Context.GlobalBlackboard = World?.Blackboard;
            if (_seed == 0 && World != null)
                Context.Reseed(World.NextAgentSeed());     // seed 0: the world hands it out, so a level replays
        }

        /// <summary>Gives the agent a behavior; the previous one is stopped.</summary>
        public void SetGraph(BehaviorGraph graph)
        {
            Initialize();
            if (_instance != null)
            {
                _instance.Release(Context);
                _instance.StateChanged -= HandleStateChanged;
                _instance = null;
            }

            Context.Memory.Clear();                         // a new behavior starts from nothing remembered
            PrepareAbilities(graph);                        // the abilities the graph names get this character's runners
            _instance = graph?.CreateInstance();
            if (_instance != null)
                _instance.StateChanged += HandleStateChanged;

            // The canvas is the program: a graph with no Brain does nothing at all — said once, not hidden
            if (graph != null && graph.Brain == null)
                Debug.LogError($"{name}: its graph has no Brain — the character does nothing (add a Brain and wire " +
                               "decisions to it)", this);

            _thinkTimer = 0f;
        }

        public void SetTarget(Transform target)
        {
            Initialize();
            Context.Track.Set(target);
        }

        /// <summary>
        /// Only the senses for one step — the brain is off (switched off, a cutscene), the eyes stay open: the senses a
        /// Senses sensor of the graph has opened look and remember.
        /// </summary>
        public void Sense(float dt)
        {
            if (!_initialized)
                Initialize();

            Context?.Perception?.Step(dt);
        }

        /// <summary>A signal reached this agent (an ally's shout, an order); it lives until the next think tick ends.</summary>
        public void SendSignal(string id)
        {
            Initialize();
            Context.SendSignal(id);
        }

        /// <summary>
        /// Something worth a decision happened (a hit, a signal, an order): the agent thinks on its next step instead of
        /// waiting out the think interval. The world's decision budget still applies.
        /// </summary>
        public void RequestThink() => _thinkTimer = 0f;

        /// <summary>Enters a state of the running graph by name (a load, a debug jump); see BehaviorGraphInstance.EnterState.</summary>
        public bool EnterState(string stateName)
        {
            Initialize();
            return _instance != null && _instance.EnterState(stateName, Context);
        }

        public void SetSeed(int seed)
        {
            _seed = seed;
            Context?.Reseed(seed);
        }

        /// <summary>
        /// The first decision comes this fraction of the think interval later (the scheduler spreads agents). An agent
        /// that has thought already keeps its rhythm: joining a world later (its first ask for an attack place joins the
        /// scene's world) never puts its next think off.
        /// </summary>
        internal void DelayFirstDecision(float fraction)
        {
            if (DecisionCount > 0)
                return;

            _thinkTimer = Mathf.Max(_thinkTimer, Mathf.Clamp01(fraction) * _thinkInterval);
        }

        /// <summary>
        /// One step: cooldowns, a decision when its time has come and the world allows it (otherwise the agent decides
        /// on a following step), execution of the active states, then the buffered presses go to the body.
        /// </summary>
        public void Step(float dt)
        {
            if (!_initialized)
                Initialize();

            if (Context == null)
                return;

            StowWhileClimbing();
            if (_instance == null)
            {
                // No behavior yet: time and the senses go on, nothing is decided or pressed
                Context.Advance(dt);
                Context.Perception?.Step(dt);
                return;
            }

            Context.Advance(dt);
            Abilities.Tick(dt);
            // The senses a Senses sensor of the graph has opened (its state is the sensor's): they look before the think
            Context.Perception?.Step(dt);

            _thinkTimer -= dt;
            if (IsThinking && _thinkTimer <= 0f)
                TryThink();

            using (TickMarker.Auto())
                _instance.Tick(Context, dt);

            using (ApplyMarker.Auto())
                Context.Actuator?.Apply(dt);
        }

        private void TryThink()
        {
            AgentScheduler scheduler = World?.Scheduler;
            if (scheduler != null && !scheduler.TryBeginDecision(this))
                return;                                     // over the step's budget: due again on the next step

            using (ThinkMarker.Auto())
            {
                _thinkTimer = Mathf.Max(0f, _thinkInterval);
                _instance.Think(Context);
                Context.ClearSignals();
                DecisionCount++;
            }

            scheduler?.EndDecision();
        }

        private void HandleStateChanged(AgentState from, AgentState to) => StateChanged?.Invoke(from, to);
    }
}
