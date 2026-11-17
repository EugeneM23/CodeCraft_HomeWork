using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class ManaUseCaseTests
    {
        private sealed class TestPlayerContext : Entity, IPlayerContext
        {
        }

        private static IPlayerContext Context(int mana)
        {
            IPlayerContext context = new TestPlayerContext();
            context.AddCurrentMana(new ReactiveInt(mana));
            return context;
        }

        [Test]
        public void Enough_ExactlyTheCost_True()
        {
            Assert.That(ManaUseCase.Enough(Context(3), 3), Is.True);
        }

        [Test]
        public void Enough_LessThanTheCost_False()
        {
            Assert.That(ManaUseCase.Enough(Context(2), 3), Is.False);
        }

        [Test]
        public void Spend_Enough_TakesTheCost()
        {
            IPlayerContext context = Context(5);

            bool spent = ManaUseCase.Spend(context, 3);

            Assert.That(spent, Is.True);
            Assert.That(context.GetCurrentMana().Value, Is.EqualTo(2));
        }

        [Test]
        public void Spend_NotEnough_LeavesManaAlone()
        {
            IPlayerContext context = Context(2);

            bool spent = ManaUseCase.Spend(context, 3);

            Assert.That(spent, Is.False);
            Assert.That(context.GetCurrentMana().Value, Is.EqualTo(2));
        }
    }
}
