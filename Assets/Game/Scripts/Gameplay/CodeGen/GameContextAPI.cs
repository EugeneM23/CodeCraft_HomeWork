using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// Game context API definition. A context is just an entity type now, so the former
    /// "header: ContextAPI" yaml becomes an [EntityExtensionsAPI] class keyed on IGameContext.
    /// </summary>
    [EntityExtensionsAPI]
    public static partial class GameContextAPI
    {
        public static readonly ValueKey<IGameContext, WeaponCatalog> WeaponCatalog = new(nameof(WeaponCatalog));
        public static readonly ValueKey<IGameContext, PlayerContext> PlayerContext = new(nameof(PlayerContext));
        public static readonly ValueKey<IGameContext, GameFactory> GameFactory = new(nameof(GameFactory));
        public static readonly ValueKey<IGameContext, IEntityWorld<IEntity>> EntityWorld = new(nameof(EntityWorld));

        // IEntityFilter is gone; EntityFilter implements IReadOnlyEntityCollection<IEntity>.
        public static readonly ValueKey<IGameContext, EntityFilter> EnemyFilter = new(nameof(EnemyFilter));
    }
}
