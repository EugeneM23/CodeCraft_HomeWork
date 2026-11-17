namespace AIKit
{
    /// <summary>
    /// Where an ability is in its life (handbook §16): the telegraph, the part that hits, the vulnerable tail. The
    /// fight reads it — a token taken away during the wind-up does not cancel the swing, two telegraphs never start
    /// together, a recovering attacker steps back.
    /// </summary>
    public enum AbilityPhase
    {
        /// <summary>Not playing.</summary>
        None,

        /// <summary>The telegraph before it lands.</summary>
        WindUp,

        /// <summary>It lands (the hit window).</summary>
        Active,

        /// <summary>The vulnerable tail after it.</summary>
        Recovery
    }

    /// <summary>What keeps an ability from being used right now; None — nothing does, it is ready.</summary>
    public enum AbilityBlock
    {
        None,

        /// <summary>The agent has no ability by that name.</summary>
        Unknown,

        /// <summary>Its cooldown or its start delay is not over.</summary>
        Resting,

        Playing,
        NoTarget,
        InAir,

        /// <summary>It is used only while the agent holds its place to attack, and the agent does not.</summary>
        NeedsPlace,

        TooNear,
        TooFar,
        Unaware,
        NoLine,

        /// <summary>The target is not where the ability wants it around the agent (in front, at the side, behind).</summary>
        WrongSide,

        /// <summary>The runner says the body cannot start it now.</summary>
        BodyBusy,

        /// <summary>It takes turns (its pool of the target's tokens), and every turn is another's now.</summary>
        TurnTaken
    }

    /// <summary>
    /// Plays an ability the AI chose — the bridge to whatever the game already has (a skill, an animation, a spell).
    /// The AI decides which and when; the runner knows how.
    /// </summary>
    public interface IAbilityRunner
    {
        /// <summary>Matches AbilityDefinition.Id.</summary>
        string Id { get; }

        /// <summary>The body is in a state where the ability can start at all (not stunned, not mid-air…).</summary>
        bool CanStart(AgentContext context);

        /// <summary>
        /// Plays the ability. Not called Start on purpose: a runner is usually a MonoBehaviour, and Unity reserves
        /// Start() as a message — a public Start(AgentContext) on a component makes the editor log
        /// "Start() can not take parameters".
        /// </summary>
        void Begin(AgentContext context);

        /// <summary>True from Start until the ability is over: the cooldown starts when this falls back to false.</summary>
        bool IsRunning { get; }

        /// <summary>
        /// The phase now, read from the content when the runner has it (a clip and its hit window); a runner that does
        /// not know calls the whole run Active.
        /// </summary>
        AbilityPhase Phase => IsRunning ? AbilityPhase.Active : AbilityPhase.None;

        void Cancel();
    }
}
