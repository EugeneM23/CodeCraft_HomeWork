using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class PlayerVehicleControllerTests
    {
        private sealed class TestPlayerContext : Entity, IPlayerContext
        {
        }

        private static IEntity Vehicle()
        {
            IEntity vehicle = new Entity();
            vehicle.AddVehicleTag();
            vehicle.AddDriver(new ReactiveVariable<IEntity>());
            return vehicle;
        }

        private static IPlayerContext Context(IEntity character, out PlayerVehicleController controller)
        {
            IPlayerContext context = new TestPlayerContext();
            context.AddCharacter(new ReactiveVariable<IEntity>(character));
            controller = new PlayerVehicleController();
            controller.Init(context);
            return context;
        }

        [Test]
        public void TheTankInTheSlotFromTheStart_IsDrivenByThePlayer()
        {
            IEntity tank = Vehicle();

            IPlayerContext context = Context(tank, out _);

            Assert.That(tank.GetDriver().Value, Is.SameAs(context));
            Assert.That(tank.HasPlayerTag(), Is.True);
        }

        [Test]
        public void SwitchingVehicles_HandsTheDriverAndTheTagOver()
        {
            IEntity left = Vehicle();
            IEntity taken = Vehicle();
            IPlayerContext context = Context(left, out _);

            context.GetCharacter().Value = taken;

            Assert.That(left.GetDriver().Value, Is.Null);
            Assert.That(left.HasPlayerTag(), Is.False);
            Assert.That(taken.GetDriver().Value, Is.SameAs(context));
            Assert.That(taken.HasPlayerTag(), Is.True);
        }

        [Test]
        public void SomethingWithoutADriver_IsLeftAlone()
        {
            IEntity thing = new Entity();

            Context(thing, out _);

            Assert.That(thing.HasPlayerTag(), Is.False);
        }

        [Test]
        public void Dispose_ReleasesTheVehicle()
        {
            IEntity tank = Vehicle();
            IPlayerContext context = Context(tank, out PlayerVehicleController controller);

            controller.Dispose(context);

            Assert.That(tank.GetDriver().Value, Is.Null);
            Assert.That(tank.HasPlayerTag(), Is.False);
        }

        [Test]
        public void AnotherDriver_IsNotTakenOutOnRelease()
        {
            IEntity tank = Vehicle();
            IPlayerContext context = Context(tank, out _);
            IEntity other = new Entity();
            tank.GetDriver().Value = other;

            context.GetCharacter().Value = null;

            Assert.That(tank.GetDriver().Value, Is.SameAs(other));
        }
    }
}
