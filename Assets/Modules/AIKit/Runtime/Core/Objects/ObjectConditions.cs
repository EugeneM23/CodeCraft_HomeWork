namespace AIKit
{
    /// <summary>
    /// The need of a search said as a visible condition (step 90, C15: "the pick-up stops" is not the object's hidden say):
    /// "Is (Me) &lt;state&gt;", or "not" — a Search For for a rock wants "not Holds Picked Weapon", one for a cover "not In
    /// Cover". Used by the upgrade of the old tactics nodes and by the lab's builders.
    /// </summary>
    public static class ObjectConditions
    {
        /// <summary>The stable id of the Is condition (the character's state).</summary>
        public const string IS = "aikit.condition.is";

        /// <summary>"Is (Me) &lt;state&gt;", or with invert "Is Not": a fresh condition.</summary>
        public static AgentCondition Is(CharacterState state, bool invert) =>
            new IsCondition { Of = Whose.Me, State = state, Is = invert ? IsCheck.IsNot : IsCheck.Is };
    }
}
