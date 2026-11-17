namespace AIKit
{
    /// <summary>
    /// What a Senses sensor of the graph needs to open the character's senses (step 90, C21: the senses are the sensor's,
    /// not a section of the character): the body's eyes, the game's say on which targets count, and a profile given in
    /// code for this one agent.
    /// </summary>
    public sealed partial class AgentContext
    {
        /// <summary>What the body's eyes find around it (the body's scanner); null — it sees nothing.</summary>
        public ITargetScanner Scanner { get; set; }

        /// <summary>
        /// Which targets count: the game's filter (an ITargetFilter component of the character); null — every target.
        /// The senses and the attack groups (an engagement drops a target the game says is gone) read it.
        /// </summary>
        public ITargetFilter TargetFilter { get; set; }

        /// <summary>
        /// A perception profile given in code for this one agent (a spawner's own sight per fighter): the Senses sensor
        /// sees with it instead of its own. Null — the sensor's own profile. SensesSensorTask.SetProfile sets it.
        /// </summary>
        public PerceptionProfile SensesProfile { get; set; }
    }
}
