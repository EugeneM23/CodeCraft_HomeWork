namespace AIKit
{
    /// <summary>
    /// What the agents of one world share: the global blackboard (alert level, combat phase), the scheduler that
    /// rations their decisions and the seed their randoms come from. Later stages add the signal bus and the roster.
    /// The game owns one instance (registered as a service by its composition root) and hands it to its agents — at any
    /// time: an agent may join after it was set up, its global scope and seed follow. An agent without a world works
    /// alone — its global scope falls back to its own blackboard and it thinks whenever it is due.
    /// </summary>
    public interface IAIWorld
    {
        Blackboard Blackboard { get; }

        AgentScheduler Scheduler { get; }

        /// <summary>The world's agents in a fixed order, and the engagements around their targets.</summary>
        AgentRoster Roster { get; }

        /// <summary>
        /// The objects of the world its agents search (step 90): who goes for which (claims, counted on the world's steps)
        /// and what each could not reach. The scene's registry unless the world was given its own.
        /// </summary>
        WorldObjects Objects { get; }

        /// <summary>The world's seed: agents with seed 0 get theirs from it, in the order they ask.</summary>
        int Seed { get; }

        /// <summary>A seed for the next agent that asks (deterministic from Seed and the number of agents before it).</summary>
        int NextAgentSeed();

        /// <summary>Once per fixed step, before any agent of the world steps: opens the step's decision budget.</summary>
        /// <summary>Once per fixed step, before the agents: the budget opens, token requests mature.</summary>
        void BeginStep(float dt = 0f);

        /// <summary>The highest alert of the world's agents this step (the level's alarm; Idle — calm).</summary>
        AlertLevel Alert { get; }

        /// <summary>An agent reports its alert level once per step.</summary>
        void ReportAlert(AlertLevel level);

        /// <summary>Forgets the shared facts (a new scene); registered agents stay registered.</summary>
        void Reset();
    }

    /// <inheritdoc />
    public sealed class AIWorld : IAIWorld
    {
        private int _seedsGiven;

        public AIWorld(int seed = 1) => Seed = seed;

        public Blackboard Blackboard { get; } = new();

        public AgentScheduler Scheduler { get; } = new();

        public AgentRoster Roster { get; } = new();

        private WorldObjects _objects;

        /// <summary>The objects of the world: the scene's registry (WorldObjects.Scene) unless given its own (a test).</summary>
        public WorldObjects Objects
        {
            get => _objects ?? WorldObjects.Scene;
            set => _objects = value;
        }

        public int Seed { get; set; }

        public int NextAgentSeed() => AgentRandom.Derive(Seed, _seedsGiven++);

        public AlertLevel Alert { get; private set; }

        private AlertLevel _alertThisStep;

        public void BeginStep(float dt = 0f)
        {
            Scheduler.BeginStep();
            Roster.Step(dt);
            Objects.Step(dt);
            Alert = _alertThisStep;
            _alertThisStep = AlertLevel.Idle;
        }

        public void ReportAlert(AlertLevel level)
        {
            if (Rank(level) > Rank(_alertThisStep))
                _alertThisStep = level;
        }

        // Searching is a lost fight: above suspicion, below knowing
        private static int Rank(AlertLevel level) => level switch
        {
            AlertLevel.Aware => 3,
            AlertLevel.Searching => 2,
            AlertLevel.Suspicious => 1,
            _ => 0
        };

        /// <summary>Forgets the shared facts and starts handing seeds from the beginning (a new scene replays).</summary>
        public void Reset()
        {
            Blackboard.Clear();
            _seedsGiven = 0;
        }
    }
}
