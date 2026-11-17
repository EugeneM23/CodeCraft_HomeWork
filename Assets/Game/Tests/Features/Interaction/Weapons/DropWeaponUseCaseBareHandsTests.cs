using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class DropWeaponUseCaseBareHandsTests
    {
        [Test]
        public void CharacterHoldingOnlyItsHands_HasNothingToDrop()
        {
            IEntity hand = new Entity();
            hand.AddWeaponId(WeaponID.Hand);

            IEntity character = new Entity();
            character.AddHandWeapon(new ReactiveVariable<IEntity>(hand));
            character.AddWeapon(new ReactiveVariable<IEntity>(hand));

            // No context: a drop that went ahead would need the weapon catalog and throw.
            bool dropped = DropWeaponUseCase.DropWeapon(character, null);

            Assert.That(dropped, Is.False);
            Assert.That(character.GetWeapon().Value, Is.SameAs(hand));
        }
    }
}
