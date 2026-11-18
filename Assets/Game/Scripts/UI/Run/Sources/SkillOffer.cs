namespace Game
{
    /// <summary>One card of the skill choice: a new skill (FromLevel 0) or the next level of a taken one.</summary>
    public readonly struct SkillOffer
    {
        public readonly string Id;
        public readonly int FromLevel;
        public readonly int ToLevel;
        public readonly bool IsWeapon;

        public SkillOffer(string id, int fromLevel, int toLevel, bool isWeapon)
        {
            Id = id;
            FromLevel = fromLevel;
            ToLevel = toLevel;
            IsWeapon = isWeapon;
        }
    }
}
