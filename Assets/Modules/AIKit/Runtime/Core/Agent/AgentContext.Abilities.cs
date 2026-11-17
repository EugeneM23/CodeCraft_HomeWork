namespace AIKit
{
    public sealed partial class AgentContext
    {
        /// <summary>
        /// The ability the decision in charge chose (step 90): a Use Ability decision's own, or the one Use Any Ability
        /// picked when it was taken. A Use Ability block of its chain with no ability of its own uses it — the canvas marks
        /// such a block "ability: decision's", as "target: Brain's". Null — no ability decision is in charge.
        /// </summary>
        public AbilityAsset ChosenAbility { get; set; }
    }
}
