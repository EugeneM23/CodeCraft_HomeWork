using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class DamageUseCaseTests
    {
        private static IEntity CreateTarget(int hitPoints, out Health health)
        {
            health = new Health(hitPoints, hitPoints);
            IEntity target = new Entity();
            target.AddHealth(health);
            target.AddDamageTakenEvent(new Event<TakeDamageArgs>());
            return target;
        }

        private static IEntity CreateSource(int damage)
        {
            IEntity source = new Entity();
            source.AddDamage(new ReactiveInt(damage));
            return source;
        }

        /// <summary>
        /// What DeathBehaviour does on every character: dispose the entity from inside
        /// OnHealthEmpty. Disposing empties every event the entity holds.
        /// </summary>
        private static void DisposeOnDeath(IEntity target, Health health) =>
            health.OnHealthEmpty += target.Dispose;

        [Test]
        public void KillingBlow_StillReachesDamageTakenSubscribers()
        {
            IEntity target = CreateTarget(10, out Health health);
            DisposeOnDeath(target, health);
            int hits = 0;
            target.GetDamageTakenEvent().Subscribe(_ => hits++);

            DamageUseCase.TakeDamage(target, new TakeDamageArgs(CreateSource(10)));

            Assert.That(hits, Is.EqualTo(1));
        }

        [Test]
        public void KillingBlow_IsAnnouncedWithHealthEmpty_WhileTheEntityIsStillWhole()
        {
            IEntity target = CreateTarget(10, out Health health);
            DisposeOnDeath(target, health);
            bool? aliveWhenHit = null;
            bool? wholeWhenHit = null;
            target.GetDamageTakenEvent().Subscribe(_ =>
            {
                aliveWhenHit = health.Exists();
                wholeWhenHit = target.HasHealth();
            });

            DamageUseCase.TakeDamage(target, new TakeDamageArgs(CreateSource(10)));

            Assert.That(aliveWhenHit, Is.False, "subscribers tell the killing blow by the empty health");
            Assert.That(wholeWhenHit, Is.True, "the entity is disposed only after the hit is announced");
        }

        [Test]
        public void OrdinaryHit_ReducesHealthFirst_ThenRaisesTheEventOnce()
        {
            IEntity target = CreateTarget(10, out Health health);
            int hits = 0;
            int healthWhenHit = -1;
            target.GetDamageTakenEvent().Subscribe(_ =>
            {
                hits++;
                healthWhenHit = health.GetCurrent();
            });

            DamageUseCase.TakeDamage(target, new TakeDamageArgs(CreateSource(3)));

            Assert.That(hits, Is.EqualTo(1));
            Assert.That(healthWhenHit, Is.EqualTo(7));
            Assert.That(health.GetCurrent(), Is.EqualTo(7));
        }
    }
}
