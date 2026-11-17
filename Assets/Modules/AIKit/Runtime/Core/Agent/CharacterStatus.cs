namespace AIKit
{
    /// <summary>
    /// A number about a character that the graph may read (the Get node, step 90): its own or another's — the target's,
    /// an ally's. Who gives it is the character's own status (ICharacterStatus): the AI character for an AI, the game for
    /// the player. Health comes from the game (IHealth); the others from the body.
    /// </summary>
    public enum CharacterStat
    {
        Health,
        MaxHealth,
        HealthPercent,

        /// <summary>Metres per second it moves now (measured, not asked).</summary>
        Speed,

        /// <summary>Its full speed: how fast it runs.</summary>
        RunSpeed,

        /// <summary>Degrees per second it turns.</summary>
        TurnSpeed,

        /// <summary>Metres per second² it speeds up and slows down with.</summary>
        Acceleration,

        Height,
        Radius,

        /// <summary>Moves left of the weapon in its hand (a rock: 1; a gun: its rounds); no data when there is no end.</summary>
        UsesLeft
    }

    /// <summary>
    /// What a character is doing or what it is in, as the graph may ask it (the Is condition, step 90; handbook §17 p.2:
    /// an exceptional move is a condition — "the target blocks"). One owner for each: the body for the movement, the
    /// attack runner for the phase of a blow, the game for what only it knows (healing, aiming…). A state the character
    /// does not report is "no data", not "no".
    /// </summary>
    public enum CharacterState
    {
        Moving,
        Running,
        InAir,
        Climbing,
        Swimming,
        Flying,
        Dashing,
        Attacking,

        /// <summary>The wind-up of a blow — the warning the other side can react to (handbook §19.1).</summary>
        WindingUp,

        /// <summary>After a blow: the window when it can be punished.</summary>
        Recovering,

        Blocking,
        UsingAbility,
        Aiming,
        Healing,
        Staggered,
        Dead,

        /// <summary>Its hands hold something (a box, a rock).</summary>
        Carrying,

        /// <summary>It is inside a building: not seen, not in anybody's way.</summary>
        Inside,

        /// <summary>It has an enemy to fight (its Brain has a target this think).</summary>
        InCombat,

        /// <summary>It holds a weapon in its hand.</summary>
        Armed,

        /// <summary>The weapon in its hand shoots (a bow, a rock to throw).</summary>
        WeaponShoots,

        /// <summary>The weapon in its hand was taken up from the world (a rock): it is spent, not put away.</summary>
        HoldsPickedWeapon,

        /// <summary>It uses an object of the world it occupies (behind a cover, at a gun, on a perch).</summary>
        Occupying,

        /// <summary>It occupies a cover.</summary>
        InCover
    }

    /// <summary>The health of an object, as the game keeps it — the module never changes it.</summary>
    public interface IHealth
    {
        float Current { get; }

        float Max { get; }
    }

    /// <summary>
    /// A character's own say about itself, read by the graph of another (or of itself): its numbers and states. The AI
    /// character implements it; the game implements it on the player and on whatever else the AI should read. Reading
    /// another's status is perception (it is the target's own say, like IPerceivable), not one agent driving another
    /// (invariant 1). False — the character does not say: no data, not zero or "no".
    /// </summary>
    public interface ICharacterStatus
    {
        bool TryGetStat(CharacterStat stat, out float value);

        bool TryGetState(CharacterState state, out bool value);

        /// <summary>
        /// A number of the game's own by its name (Stamina, Rage, Ammo — what the module does not know): the Get node's
        /// "Custom" parameter reads it. False — the character has no such number.
        /// </summary>
        bool TryGetCustom(string name, out float value);
    }

    /// <summary>
    /// Finds what an object says about itself: its own component or one above it in the hierarchy (a collider child of
    /// the player finds the player's status). A walk up the hierarchy — a node keeps the answer per object in its memory
    /// instead of asking every think. A switched-off object is asked too (a character that is down still says it is).
    /// </summary>
    public static class CharacterStatuses
    {
        /// <summary>The object's status; null — it says nothing about itself.</summary>
        public static ICharacterStatus Of(UnityEngine.Transform transform) =>
            transform != null ? transform.GetComponentInParent<ICharacterStatus>(true) : null;

        /// <summary>The object's health; null — it has none the module can read.</summary>
        public static IHealth HealthOf(UnityEngine.Transform transform) =>
            transform != null ? transform.GetComponentInParent<IHealth>(true) : null;
    }
}
