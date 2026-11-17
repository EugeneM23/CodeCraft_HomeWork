namespace AIKit
{
    /// <summary>
    /// The place an ability needs in its pool on the target (its attack tokens, handbook §18): how the nodes that use
    /// abilities ask for it. An ability whose definition names a capacity limits the pool itself — the way the node Take
    /// Attack Slot does with its Slots; abilities that share a pool share its turns, the strictest of them counts — and is
    /// not refused to an agent that stands within the ability's own range (one with no range part: from anywhere).
    /// </summary>
    internal static class AbilityPlaces
    {
        /// <summary>
        /// aimed — the ask comes from a canvas node that was given its object: such a character joins the fighters of
        /// its scene first (it has no world of its own to count the places).
        /// </summary>
        public static TokenResult Ask(AgentContext context, AbilityDefinition definition, bool aimed)
        {
            if (definition == null || string.IsNullOrEmpty(definition.TokenPool))
                return TokenResult.Granted;

            if (aimed)
                AIWorldRunner.Join(context.Agent);

            AttackTokens tokens = context.Engagement?.Tokens;
            if (tokens != null && definition.TokenCapacity > 0)
            {
                tokens.LimitCapacity(definition.TokenPool, definition.TokenCapacity);
                tokens.ReachFor(definition.TokenPool, definition.Range.Max);   // no range part: float.MaxValue
            }

            return context.RequestToken(definition.TokenPool, definition.TokenCost, definition.Weight);
        }
    }
}
