using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    public static class HealthUseCase
    {
        /// <summary>
        /// Full health and the two events every damageable entity raises: the hit
        /// (DamageTakenEvent) and the death (DeathTakenEvent). The installer adds what happens on
        /// death — DeathBehaviour, a wreck, a thrown-off rider.
        /// </summary>
        public static Health AddHealthWithEvents(IEntity entity, int hitPoints)
        {
            Health health = new Health(hitPoints, hitPoints);
            entity.AddHealth(health);
            entity.AddDamageTakenEvent(new Event<TakeDamageArgs>());
            entity.AddDeathTakenEvent(new Event<TakeDamageArgs>());
            return health;
        }
    }
}
