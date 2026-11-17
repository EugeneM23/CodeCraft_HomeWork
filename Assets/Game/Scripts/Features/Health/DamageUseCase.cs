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
            int damage = source.GetDamage().Value;
            if (args.damageFactor < 1f)
                damage = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(damage * args.damageFactor));

            // The hit is announced from inside Reduce: after the new value is set, before
            // OnHealthEmpty. DeathBehaviour disposes the entity on OnHealthEmpty, which empties
            // DamageTakenEvent, so raising the event after Reduce returned reached no one on the
            // killing blow — no blood, no hurt sound. Subscribers still see the reduced health and
            // tell the killing blow by it (TakeDamageAnimBehaviour does).
            if (!health.Reduce(damage, () => damageEvent.Invoke(args)))
                damageEvent.Invoke(args); // nothing left to take, but it is still a hit
        }
    }
}
