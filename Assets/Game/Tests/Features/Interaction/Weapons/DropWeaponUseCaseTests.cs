using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class DropWeaponUseCaseTests
    {
        [Test]
        public void VehicleWithoutHandWeapon_KeepsItsCannon()
        {
            IEntity cannon = new Entity();
            cannon.AddWeaponId(WeaponID.TankCannon);

            IEntity vehicle = new Entity();
            vehicle.AddVehicleTag();
            vehicle.AddWeapon(new ReactiveVariable<IEntity>(cannon));

            bool dropped = DropWeaponUseCase.DropWeapon(vehicle, null);

            Assert.That(dropped, Is.False);
            Assert.That(vehicle.GetWeapon().Value, Is.SameAs(cannon));
        }
    }
}
