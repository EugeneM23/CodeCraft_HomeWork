namespace AIKit
{
    /// <summary>A resolved tag: a blackboard fact without a value — present or not ("Stunned", "InCover").</summary>
    public readonly struct TagKey
    {
        public readonly int Index;

        public TagKey(int index) => Index = index;

        public bool IsValid => Index >= 0;

        public static TagKey None => new(-1);
    }
}
