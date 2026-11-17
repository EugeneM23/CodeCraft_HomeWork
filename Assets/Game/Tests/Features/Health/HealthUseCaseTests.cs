using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class HealthUseCaseTests
    {
        [Test]
        public void AddHealthWithEvents_GivesFullHealthAndBothEvents()
        {
            IEntity entity = new Entity();

            Health health = HealthUseCase.AddHealthWithEvents(entity, 25);

            Assert.That(entity.GetHealth(), Is.SameAs(health));
            Assert.That(health.GetCurrent(), Is.EqualTo(25));
            Assert.That(health.IsFull(), Is.True);
            Assert.That(entity.HasDamageTakenEvent(), Is.True);
            Assert.That(entity.HasDeathTakenEvent(), Is.True);
        }

        [Test]
        public void AddHealthWithEvents_HitAndDeathAreSeparateEvents()
        {
            IEntity entity = new Entity();
            HealthUseCase.AddHealthWithEvents(entity, 10);
            int deaths = 0;
            entity.GetDeathTakenEvent().Subscribe(_ => deaths++);

            entity.GetDamageTakenEvent().Invoke(new TakeDamageArgs(entity));

            Assert.That(deaths, Is.Zero);
        }
    }
}
