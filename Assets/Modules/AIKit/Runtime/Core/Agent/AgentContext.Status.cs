namespace AIKit
{
    /// <summary>
    /// What the agent's own blocks say it is doing, for its status (ICharacterStatus, step 90) and for whoever reads it —
    /// another agent's Is condition, the animation component. One owner each: the attack runner for the phase of a blow,
    /// the Block block for guarding, a game that staggers for being staggered (the flinch of a hit is no state: the hit
    /// receiver marks HurtAt, the presentation plays it).
    /// </summary>
    public sealed partial class AgentContext
    {
        /// <summary>The phase of the blow the agent strikes now (None — it does not strike). Set by AttackRunner.</summary>
        public AbilityPhase AttackPhase { get; set; }

        /// <summary>The agent holds its guard up (the Block block).</summary>
        public bool IsBlocking { get; set; }

        /// <summary>The agent is staggered by a hit (a game that staggers; the status reads it).</summary>
        public bool IsStaggered { get; set; }

        /// <summary>
        /// When the agent was last hurt, on its own clock (LocalTime; negative — never), and from where (step 90: the
        /// flinch is the body's response, not a decision — the presentation reads these and plays it on its own layer).
        /// Written by AICharacter's hit receiver only.
        /// </summary>
        public float HurtAt { get; set; } = -1f;

        /// <summary>Where the last hit came from (the attacker's position at the hit).</summary>
        public UnityEngine.Vector3 HurtFrom { get; set; }

        /// <summary>
        /// The decision in charge changed, or none is in charge any more: what a block of its chain set for the length of
        /// the decision (Set Parameter) goes back now. Raised by the Brain.
        /// </summary>
        public event System.Action DecisionLeft;

        internal void NotifyDecisionLeft() => DecisionLeft?.Invoke();
    }
}
