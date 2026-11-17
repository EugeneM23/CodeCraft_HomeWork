using Atomic.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class TickCooldownBehaviourTests
    {
        private static IEntity Weapon(CooldownTimer cooldown)
        {
            IEntity weapon = new Entity();
            weapon.AddWeaponCooldown(cooldown);
            weapon.AddBehaviour<TickCooldownBehaviour>();
            weapon.Enable();
            return weapon;
        }

        [Test]
        public void EnabledWeapon_RunsDownItsCooldownEveryTick()
        {
            var cooldown = new CooldownTimer(1f, 1f);
            IEntity weapon = Weapon(cooldown);

            weapon.Tick(0.4f);
            weapon.Tick(0.4f);
            bool expiredEarly = cooldown.IsExpired();
            weapon.Tick(0.4f);

            Assert.That(expiredEarly, Is.False);
            Assert.That(cooldown.IsExpired(), Is.True);
        }

        [Test]
        public void DisabledWeapon_KeepsItsCooldown()
        {
            var cooldown = new CooldownTimer(1f, 1f);
            IEntity weapon = Weapon(cooldown);
            weapon.Disable();

            weapon.Tick(5f);

            Assert.That(cooldown.IsExpired(), Is.False);
        }
    }
}
