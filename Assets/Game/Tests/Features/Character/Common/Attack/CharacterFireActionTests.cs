using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class CharacterFireActionTests
    {
        private sealed class Gun
        {
            public readonly IEntity Entity = new Entity();
            public bool Ready = true;
            public int Shots;

            public Gun()
            {
                this.Entity.AddFireCondition(new AndExpression(new ExpressionMember<bool>(() => this.Ready)));
                this.Entity.AddFireAction(new InlineAction(() => this.Shots++));
            }
        }

        private static IEntity Holding(IEntity weapon)
        {
            IEntity character = new Entity();
            character.AddWeapon(new ReactiveVariable<IEntity>(weapon));
            return character;
        }

        [Test]
        public void WeaponReady_Fires()
        {
            var gun = new Gun();
            var action = new CharacterFireAction(Holding(gun.Entity));

            action.Invoke();

            Assert.That(gun.Shots, Is.EqualTo(1));
        }

        [Test]
        public void WeaponNotReady_DoesNotFire()
        {
            var gun = new Gun { Ready = false };
            var action = new CharacterFireAction(Holding(gun.Entity));

            action.Invoke();

            Assert.That(gun.Shots, Is.Zero);
        }

        [Test]
        public void WeaponSwappedAfterTheActionWasMade_FiresTheOneInHandNow()
        {
            var oldGun = new Gun();
            var newGun = new Gun();
            IEntity character = Holding(oldGun.Entity);
            var action = new CharacterFireAction(character);

            character.GetWeapon().Value = newGun.Entity;
            action.Invoke();

            Assert.That(oldGun.Shots, Is.Zero);
            Assert.That(newGun.Shots, Is.EqualTo(1));
        }
    }
}
