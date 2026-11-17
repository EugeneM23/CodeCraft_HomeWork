using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// Ticks the weapon cooldown.
    /// </summary>
    /// <remarks>
    /// Both weapon installers used to do this with a raw <c>entity.OnTicked +=</c> subscription and
    /// a lambda capturing the entity: a display class and an Action&lt;float&gt; allocated per
    /// weapon, a dictionary lookup plus unbox per frame, and no way to unsubscribe, so the handlers of
    /// weapon entities made again and again accumulated.
    /// </remarks>
    public sealed class TickCooldownBehaviour : IEntityInit, IEntityTick
    {
        private CooldownTimer _cooldown;

        public void Init(IEntity entity) => _cooldown = entity.GetWeaponCooldown();

        public void Tick(IEntity entity, float deltaTime) => _cooldown.Tick(deltaTime);
    }
}
