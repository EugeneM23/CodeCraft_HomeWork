using System;

namespace Game.AIKit.Adapters
{
    /// <summary>So many enemies of one kind in a wave.</summary>
    [Serializable]
    public struct ArenaWaveEntry
    {
        public EnemyKind kind;
        public int count;

        public ArenaWaveEntry(EnemyKind kind, int count)
        {
            this.kind = kind;
            this.count = count;
        }
    }
}
