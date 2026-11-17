using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// What a projectile does with what it struck, before anything is dealt out.
    /// </summary>
    public static class BulletHitUseCase
    {
        /// <summary>
        /// A projectile that can ricochet tries it off anything that takes no damage — walls,
        /// floor — whether or not it explodes: the tank shell bounces exactly as bullets do and
        /// goes off only once it is out of ricochets. Something that takes damage is hit outright:
        /// a bullet wounds it, a shell explodes on it.
        /// </summary>
        /// <param name="target">The struck object's entity; null when it has none.</param>
        public static bool ShouldTryRicochet(IEntity bullet, IEntity target) =>
            RicochetUseCase.IsEnabled(bullet) && (target == null || !target.HasDamageableTag());

        /// <summary>
        /// Whether a hit on a damageable target needs the bullet's own hit effect to be seen.
        /// Flesh answers a hit by itself (blood on DamageTakenEvent); armour has nothing of its
        /// own, so a vehicle gets the sparks a wall gets.
        /// </summary>
        public static bool ShowsHitOnTarget(IEntity target) => target != null && target.HasVehicleTag();
    }
}
