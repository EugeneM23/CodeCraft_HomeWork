namespace Game
{
    /// <summary>
    /// An enemy to shoot at: a slot of a registered target source (entity enemies or the horde). Version guards
    /// against a slot reused by a new enemy.
    /// </summary>
    public readonly struct EnemyRef
    {
        public readonly int Source;
        public readonly int Index;
        public readonly int Version;

        public EnemyRef(int source, int index, int version)
        {
            Source = source;
            Index = index;
            Version = version;
        }

        public bool IsValid => Source >= 0;

        public static EnemyRef None => new(-1, -1, 0);
    }
}
