namespace AIKit
{
    /// <summary>What a move request is, decided once when it is made (spec §15).</summary>
    public enum MoveKind
    {
        /// <summary>Toward the target itself: follows it as it moves.</summary>
        Approach,

        /// <summary>To a point (where the target was seen, a detour, a step back): the point stays put.</summary>
        Lateral
    }
}
