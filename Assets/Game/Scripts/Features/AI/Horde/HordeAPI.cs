using Atomic.Entities;

namespace Game.AIKit.Adapters
{
    /// <summary>Entity keys of the horde's arena side (Docs/Rules/Project.md, "Ключи сущностей").</summary>
    [EntityExtensionsAPI]
    public static partial class HordeAPI
    {
        /// <summary>The level's horde as the arena director sees it; set on the level's AI entity by the horde while it lives.</summary>
        public static readonly ValueKey<IEntity, IArenaHorde> LevelHorde = new(nameof(LevelHorde));

        /// <summary>The level's arena director (on the level's AI entity).</summary>
        public static readonly ValueKey<IEntity, ArenaDirector> LevelArenaDirector = new(nameof(LevelArenaDirector));
    }
}
