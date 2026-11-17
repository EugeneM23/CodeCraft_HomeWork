using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class ControllableFireControllerTests
    {
        private sealed class TestPlayerContext : Entity, IPlayerContext
        {
        }

        private int _main;
        private int _secondary;

        private (IPlayerContext context, ControllableFireController controller, FakePlayerInput input) Setup()
        {
            IEntity tank = new Entity();
            tank.AddVehicleTag();
            tank.AddFireAction(new InlineAction(() => _main++));
            tank.AddSecondaryFireAction(new InlineAction(() => _secondary++));

            var input = new FakePlayerInput();
            IPlayerContext context = new TestPlayerContext();
            context.AddCharacter(new ReactiveVariable<IEntity>(tank));
            context.AddPlayerInput(input);
            var controller = new ControllableFireController();
            controller.Init(context);
            return (context, controller, input);
        }

        [SetUp]
        public void SetUp() => _main = _secondary = 0;

        [Test]
        public void FireMainHeld_FiresTheMainGunOnly()
        {
            var (context, controller, input) = Setup();
            input.FireMain = true;

            controller.LateTick(context, 0.02f);

            Assert.AreEqual(1, _main);
            Assert.AreEqual(0, _secondary);
        }

        [Test]
        public void FireSecondaryHeld_FiresTheSecondaryGun()
        {
            var (context, controller, input) = Setup();
            input.FireSecondary = true;

            controller.LateTick(context, 0.02f);

            Assert.AreEqual(0, _main);
            Assert.AreEqual(1, _secondary);
        }

        [Test]
        public void NothingHeld_NothingFires()
        {
            var (context, controller, _) = Setup();

            controller.LateTick(context, 0.02f);

            Assert.AreEqual(0, _main + _secondary);
        }
    }
}
