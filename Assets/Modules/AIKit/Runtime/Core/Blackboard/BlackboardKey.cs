namespace AIKit
{
    /// <summary>A resolved blackboard variable: the index of its slot plus the type, so reads stay type-safe.</summary>
    public readonly struct BlackboardKey<T>
    {
        public readonly int Index;

        public BlackboardKey(int index) => Index = index;

        public bool IsValid => Index >= 0;

        public static BlackboardKey<T> None => new(-1);
    }
}
