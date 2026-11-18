using System;

namespace Game
{
    /// <summary>Hints the HUD shows on an arena (the menu team draws them).</summary>
    [Flags]
    public enum ArenaHint
    {
        None = 0,
        Controls = 1,
        Crush = 2,
        DestroyCover = 4
    }
}
