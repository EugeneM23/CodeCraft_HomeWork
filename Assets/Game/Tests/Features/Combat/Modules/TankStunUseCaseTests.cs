using Atomic.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class TankStunUseCaseTests
    {
        private const float EPS = 1e-4f;

        [Test]
        public void Stun_StunsTheTankForTheSeconds()
        {
            IEntity tank = new Entity();

            TankStunUseCase.Stun(tank, 1f);

            Assert.That(TankStunUseCase.IsStunned(tank), Is.True);
            Assert.That(tank.GetTankStun().Until, Is.EqualTo(GameTicks.Time + 1f).Within(EPS));
        }

        [Test]
        public void ALongerStun_ExtendsTheCurrentOne()
        {
            IEntity tank = new Entity();

            TankStunUseCase.Stun(tank, 1f);
            TankStunUseCase.Stun(tank, 3f);

            Assert.That(tank.GetTankStun().Until, Is.EqualTo(GameTicks.Time + 3f).Within(EPS));
        }

        [Test]
        public void AShorterStun_NeitherAddsUpNorCutsTheCurrentOneShort()
        {
            IEntity tank = new Entity();

            TankStunUseCase.Stun(tank, 3f);
            TankStunUseCase.Stun(tank, 1f);

            Assert.That(tank.GetTankStun().Until, Is.EqualTo(GameTicks.Time + 3f).Within(EPS));
        }

        [Test]
        public void ATankNeverStunned_IsNotStunned()
        {
            Assert.That(TankStunUseCase.IsStunned(new Entity()), Is.False);
            Assert.That(TankStunUseCase.IsStunned(null), Is.False);
        }

        [Test]
        public void AStunThatEnded_NoLongerStuns()
        {
            IEntity tank = new Entity();
            tank.AddTankStun(new TankStun { Until = GameTicks.Time - 0.1f });

            Assert.That(TankStunUseCase.IsStunned(tank), Is.False);
        }

        [Test]
        public void ZeroSeconds_DoesNothing()
        {
            IEntity tank = new Entity();

            TankStunUseCase.Stun(tank, 0f);

            Assert.That(tank.HasTankStun(), Is.False);
        }
    }
}
