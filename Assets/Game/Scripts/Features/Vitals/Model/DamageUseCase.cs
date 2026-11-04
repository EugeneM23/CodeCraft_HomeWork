using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    public static class DamageUseCase
    {
        public static void TakeDamage(IEntity target, TakeDamageArgs args)
        {
            // A dead target has already been disposed by DeathBehaviour, and disposing clears the
            // entity's values while leaving its tags. So a hit that lands in the same frame still
            // passes the caller's HasDamageableTag check and then finds no Health and no event.
            // DeathBehaviour disables the capsule too, but a contact already in flight arrives
            // anyway. Reading through TryGet makes a hit on a corpse a no-op instead of a throw
            // out of the physics callback, which would abort the rest of that contact.
            if (!target.TryGetHealth(out Health health)) return;
            if (!target.TryGetDamageTakenEvent(out IEvent<TakeDamageArgs> damageEvent)) return;

            IEntity source = args.source;
            health.Reduce(source.GetDamage().Value);
            damageEvent.Invoke(args);
        }
    }
}
