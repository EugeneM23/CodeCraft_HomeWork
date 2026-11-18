using Atomic.Entities;
using Game.Arenas;

namespace Game
{
    /// <summary>The arena service in the level's GameContext: <c>GameContext.Instance.GetArena()</c>.</summary>
    [EntityExtensionsAPI]
    public static partial class ArenaAPI
    {
        public static readonly ValueKey<IGameContext, IArenaService> Arena = new(nameof(Arena));
    }
}
