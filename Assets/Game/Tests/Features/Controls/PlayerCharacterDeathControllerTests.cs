using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class PlayerCharacterDeathControllerTests
    {
        private sealed class TestPlayerContext : Entity, IPlayerContext
        {
        }

        private static IEntity CreateCharacter(out Health health)
        {
            health = new Health(10, 10);
            IEntity character = new Entity();
            character.AddHealth(health);
            return character;
        }

        private static IPlayerContext CreateContext(IEntity character)
        {
            IPlayerContext context = new TestPlayerContext();
            context.AddCharacter(new ReactiveVariable<IEntity>(character));
            return context;
        }

        [Test]
        public void CharacterDies_ContextLetsGoOfIt()
        {
            IEntity character = CreateCharacter(out Health health);
            IPlayerContext context = CreateContext(character);
            new PlayerCharacterDeathController().Init(context);

            health.Reduce(10);

            Assert.That(context.GetCharacter().Value, Is.Null);
        }

        [Test]
        public void CharacterHurtButAlive_StaysInContext()
        {
            IEntity character = CreateCharacter(out Health health);
            IPlayerContext context = CreateContext(character);
            new PlayerCharacterDeathController().Init(context);

            health.Reduce(5);

            Assert.That(context.GetCharacter().Value, Is.SameAs(character));
        }

        [Test]
        public void ReplacedCharacterDies_NewCharacterStays()
        {
            IEntity oldCharacter = CreateCharacter(out Health oldHealth);
            IEntity newCharacter = CreateCharacter(out _);
            IPlayerContext context = CreateContext(oldCharacter);
            new PlayerCharacterDeathController().Init(context);

            context.GetCharacter().Value = newCharacter;
            oldHealth.Reduce(10);

            Assert.That(context.GetCharacter().Value, Is.SameAs(newCharacter));
        }

        [Test]
        public void VehicleDies_ContextKeepsIt_TheVehicleEjectsItsDriverItself()
        {
            IEntity vehicle = CreateCharacter(out Health health);
            vehicle.AddVehicleTag();
            IPlayerContext context = CreateContext(vehicle);
            new PlayerCharacterDeathController().Init(context);

            health.Reduce(10);

            Assert.That(context.GetCharacter().Value, Is.SameAs(vehicle));
        }

        [Test]
        public void AfterDispose_DeathIsIgnored()
        {
            IEntity character = CreateCharacter(out Health health);
            IPlayerContext context = CreateContext(character);
            var controller = new PlayerCharacterDeathController();
            controller.Init(context);

            controller.Dispose(context);
            health.Reduce(10);

            Assert.That(context.GetCharacter().Value, Is.SameAs(character));
        }
    }
}
