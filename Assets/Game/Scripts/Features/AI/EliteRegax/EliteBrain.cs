using AIKit;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// The elite Regax's behavior, as a graph (the MeleeChaser preset grown by the blast reaction). The asset
    /// <c>Regax_Elite_Brain</c> is written from this by the editor builder (Tools/AI/Build Regax Elite), so the graph can be
    /// opened in the AIKit graph window and rebuilt; tests build the same graph in code.
    /// <para>The Brain's decisions, the most important first:</para>
    /// <list type="number">
    /// <item>Blast Nearby (a blast within 1.5 × its radius) — Wait (stunned) → Leap Away 3–5 m from the blast.</item>
    /// <item>Got Hit (a hit with no blast: the tank's gun) — Wait (a flinch).</item>
    /// <item>Use Any Ability: Bite (the hull within reach) — Use Ability → Wait (recovery).</item>
    /// <item>Chase (sees the tank) — Chase, pressing up to the hull (the body stops against it).</item>
    /// <item>Search Last Seen (lost it) — Go To Last Seen.</item>
    /// <item>Patrol (the rest of the time) — Wander near the spawn.</item>
    /// </list>
    /// The Senses sensor writes the tank into Target; the game writes blasts into the blackboard (BlastCount,
    /// BlastPoint, BlastTime).
    /// </summary>
    public static class EliteBrain
    {
        public const string TARGET = BehaviorPresets.TARGET;

        /// <summary>Blackboard: how many blasts the agent has felt (each is a new event).</summary>
        public const string BLAST_COUNT = "BlastCount";

        /// <summary>Blackboard: where the last blast was.</summary>
        public const string BLAST_POINT = "BlastPoint";

        /// <summary>Blackboard: when the last blast was, on the agent's clock (AgentContext.LocalTime).</summary>
        public const string BLAST_TIME = "BlastTime";

        /// <summary>A blast is felt within this many of its radii.</summary>
        public const float BLAST_REACH = 1.5f;

        public const float STUN = 0.4f;
        public const float FLINCH = 0.25f;
        public const float BITE_RECOVERY = 0.3f;
        public const float BITE_COOLDOWN = 1.2f;
        public const float LEAP_MIN = 3f;
        public const float LEAP_MAX = 5f;

        /// <summary>Chase stops this close to the tank's middle — inside the hull, so it presses up to the hull's side.</summary>
        public const float CHASE_STOP = 0.5f;

        public const float WANDER_RADIUS = 6f;

        /// <summary>The Bite ability the graph names: the game plays it (<see cref="RegaxBiteRunner"/>), with its pause.</summary>
        public static AbilityAsset CreateBite()
        {
            AbilityAsset bite = AbilityAsset.Create(RegaxBiteRunner.ID, new CodeRunnerDelivery(),
                new CooldownCondition { Seconds = BITE_COOLDOWN });
            bite.Queue.AtOnce = 0;
            return bite;
        }

        /// <summary>The elite's graph; <paramref name="senses"/> — what its Senses see with (null — AIKit's defaults).</summary>
        public static BehaviorGraph Build(AbilityAsset bite, PerceptionProfile senses = null)
        {
            var g = new BrainGraphBuilder();
            var sensor = new SensesSensorTask { StoreIn = TARGET, Profile = senses };
            g.Sensor(sensor);
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            g.Variable(new VariableDeclaration(BLAST_COUNT, VariableType.Int));
            g.Variable(new VariableDeclaration(BLAST_POINT, VariableType.Vector3));
            g.Variable(new VariableDeclaration(BLAST_TIME, VariableType.Float) { Float = -1000f });

            AgentState blast = g.Decision(new BlastReaction(), 1, "Blast Nearby");
            g.Chain(blast, new WaitTask(STUN), new LeapAwayTask { MinDistance = LEAP_MIN, MaxDistance = LEAP_MAX });

            AgentState hit = g.Decision(new GotHitReaction
            {
                Window = 0.6f,
                Cooldown = 1.5f,
                ReactionTime = 0.05f,
                ReactionTimeMax = 0.15f
            }, 2, "Got Hit");
            g.Chain(hit, new WaitTask(FLINCH));

            var fight = new UseAnyAbilityReasoner();
            if (bite != null)
                fight.Abilities.Add(new AbilityValue(bite));
            AgentState attack = g.Decision(fight, 3, "Bite");
            g.Chain(attack, new UseAbilityTask(), new WaitTask(BITE_RECOVERY));

            AgentState chase = g.Decision(new ChaseReasoner(), 4, "Chase");
            g.Chain(chase, new ChaseTask { StopDistance = CHASE_STOP });

            AgentState search = g.Decision(new LastSeenReasoner(), 5, "Search Last Seen");
            g.Chain(search, new GoToLastSeenTask());

            AgentState wander = g.Decision(new PatrolReasoner(), 6, "Wander");
            g.Chain(wander, new WanderTask { Radius = WANDER_RADIUS });

            return g.Build();
        }
    }
}
