using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class HeldWeaponFireListenerTests
    {
        private static IEntity Weapon()
        {
            IEntity weapon = new Entity();
            weapon.AddFireEvent(new Event());
            return weapon;
        }

        [Test]
        public void WeaponHeldAtStart_FiringReachesTheCallbackWithThatWeapon()
        {
            IEntity weapon = Weapon();
            var slot = new ReactiveVariable<IEntity>(weapon);
            var fired = new List<IEntity>();
            using var listener = new HeldWeaponFireListener(slot, fired.Add);

            weapon.GetFireEvent().Invoke();

            Assert.That(fired, Is.EqualTo(new[] { weapon }));
        }

        [Test]
        public void WeaponSwapped_OnlyTheNewOneIsHeard()
        {
            IEntity oldWeapon = Weapon();
            IEntity newWeapon = Weapon();
            var slot = new ReactiveVariable<IEntity>(oldWeapon);
            var fired = new List<IEntity>();
            using var listener = new HeldWeaponFireListener(slot, fired.Add);

            slot.Value = newWeapon;
            oldWeapon.GetFireEvent().Invoke();
            newWeapon.GetFireEvent().Invoke();

            Assert.That(fired, Is.EqualTo(new[] { newWeapon }));
        }

        [Test]
        public void EmptyHands_ThenAWeapon_ReportsBothChanges()
        {
            var slot = new ReactiveVariable<IEntity>(null);
            var changes = new List<IEntity>();
            using var listener = new HeldWeaponFireListener(slot, _ => { }, changes.Add);
            IEntity weapon = Weapon();

            slot.Value = weapon;

            Assert.That(changes, Is.EqualTo(new[] { null, weapon }));
        }

        [Test]
        public void AfterDispose_NothingIsHeard()
        {
            IEntity weapon = Weapon();
            var slot = new ReactiveVariable<IEntity>(weapon);
            var fired = new List<IEntity>();
            var listener = new HeldWeaponFireListener(slot, fired.Add);

            listener.Dispose();
            weapon.GetFireEvent().Invoke();
            slot.Value = Weapon();

            Assert.That(fired, Is.Empty);
        }
    }
}
