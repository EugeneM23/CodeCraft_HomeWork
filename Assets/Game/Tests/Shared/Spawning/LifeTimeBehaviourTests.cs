using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class LifeTimeBehaviourTests
    {
        private int _destroys;

        [SetUp]
        public void SetUp() => _destroys = 0;

        private IEntity Effect(float lifeTime)
        {
            IEntity effect = new Entity();
            // Created expired, the way every installer creates it.
            effect.AddLifeTime(new CooldownTimer(lifeTime));
            effect.AddDestroyAction(new InlineAction(() => _destroys++));
            effect.AddBehaviour<LifeTimeBehaviour>();
            return effect;
        }

        [Test]
        public void Enable_StartsTheLifeOver_SoAnExpiredTimerDoesNotSendItStraightBack()
        {
            IEntity effect = Effect(1f);
            effect.Enable();

            effect.Tick(0.5f);

            Assert.That(_destroys, Is.Zero);
        }

        [Test]
        public void LifeRunsOut_DestroyActionIsInvoked()
        {
            IEntity effect = Effect(1f);
            effect.Enable();

            effect.Tick(0.6f);
            effect.Tick(0.6f);

            Assert.That(_destroys, Is.EqualTo(1));
        }

        [Test]
        public void RentedAgain_GetsAWholeNewLife()
        {
            IEntity effect = Effect(1f);
            effect.Enable();
            effect.Tick(0.9f);

            effect.Disable();
            effect.Enable();
            effect.Tick(0.9f);

            Assert.That(_destroys, Is.Zero);
        }
    }
}
