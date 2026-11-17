namespace AIKit
{
    /// <summary>
    /// The Attack Slot condition "the place to attack from is mine" (step 90, C7), for what wires it into a decision in
    /// code: the graph upgrade of an ability that needed its user's place, and the melee preset that attacks only from a
    /// place.
    /// </summary>
    public static class AttackSlotLink
    {
        /// <summary>The Attack Slot condition's id in saved graphs.</summary>
        public const string ID = "aikit.condition.attack-slot";

        /// <summary>A new Attack Slot condition asking "mine" (of the common group of places).</summary>
        public static AgentCondition Mine() => new AttackSlotCondition(AttackSlotIs.Mine);
    }
}
