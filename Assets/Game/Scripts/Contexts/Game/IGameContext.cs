using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// The game context is an entity now. The [EntityDomainBehaviours] marker below makes
    /// EntityDomainGenerator emit IGameContextBehaviour / Init / Enable / Disable / Dispose /
    /// Tick / FixedTick / LateTick / Gizmos into this namespace.
    /// </summary>
    public interface IGameContext : IEntity
    {
    }

    [EntityDomainBehaviours(typeof(IGameContext))]
    public static class GameContextDomain
    {
    }
}
