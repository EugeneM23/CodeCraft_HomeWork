using System;

namespace Game
{
    /// <summary>A chapter in the save (GDD §14): open, completed, the furthest arena, the best completion time (0 — none).</summary>
    [Serializable]
    public sealed class ChapterSaveData
    {
        public string id;
        public bool unlocked;
        public bool completed;
        public int bestArena;
        public float bestTimeSec;
    }
}
