namespace Game
{
    /// <summary>A skill taken in the run: its id (texts and icon by id in the UI) and level 1..5.</summary>
    public readonly struct RunSkill
    {
        public readonly string Id;
        public readonly int Level;
        /// <summary>An automatic weapon (up to 4) rather than an upgrade (up to 6).</summary>
        public readonly bool IsWeapon;

        public RunSkill(string id, int level, bool isWeapon)
        {
            Id = id;
            Level = level;
            IsWeapon = isWeapon;
        }
    }
}
