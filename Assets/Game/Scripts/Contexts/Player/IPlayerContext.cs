using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// The player context is an entity now. The [EntityDomainBehaviours] marker below makes
    /// EntityDomainGenerator emit IPlayerContextBehaviour / Init / Enable / Disable / Dispose /
    /// Tick / FixedTick / LateTick / Gizmos into this namespace.
    /// </summary>
    public interface IPlayerContext : IEntity
    {
    }

    [EntityDomainBehaviours(typeof(IPlayerContext))]
    public static class PlayerContextDomain
    {
    }
}
