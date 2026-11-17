using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class RestoreManaBehaviourTests
    {
        private sealed class TestPlayerContext : Entity, IPlayerContext
        {
        }

        private const int MaxMana = 10;

        private static IPlayerContext Context(int mana)
        {
            IPlayerContext context = new TestPlayerContext();
            context.AddCurrentMana(new ReactiveInt(mana));
            context.AddMaxMana(new Const<int>(MaxMana));
            return context;
        }

        /// <summary>Restores <paramref name="restore"/> mana once a second; the first second is still to run.</summary>
        private static RestoreManaBehaviour Restore(IPlayerContext context, int restore)
        {
            var behaviour = new RestoreManaBehaviour(new CooldownTimer(1f, 1f), new Const<int>(restore));
            behaviour.Init(context);
            return behaviour;
        }

        [Test]
        public void BeforeThePeriod_NothingRestored()
        {
            IPlayerContext context = Context(4);
            RestoreManaBehaviour behaviour = Restore(context, 2);

            behaviour.FixedTick(context, 0.5f);

            Assert.That(context.GetCurrentMana().Value, Is.EqualTo(4));
        }

        [Test]
        public void EachPeriod_RestoresOnePortion()
        {
            IPlayerContext context = Context(4);
            RestoreManaBehaviour behaviour = Restore(context, 2);

            behaviour.FixedTick(context, 0.5f);
            behaviour.FixedTick(context, 0.5f);
            int afterOne = context.GetCurrentMana().Value;
            behaviour.FixedTick(context, 1f);

            Assert.That(afterOne, Is.EqualTo(6));
            Assert.That(context.GetCurrentMana().Value, Is.EqualTo(8));
        }

        [Test]
        public void PortionLargerThanTheMissingMana_StopsAtTheMaximum()
        {
            IPlayerContext context = Context(9);
            RestoreManaBehaviour behaviour = Restore(context, 3);

            behaviour.FixedTick(context, 1f);

            Assert.That(context.GetCurrentMana().Value, Is.EqualTo(MaxMana));
        }

        [Test]
        public void AboveTheMaximum_NeverGrows()
        {
            IPlayerContext context = Context(MaxMana + 2);
            RestoreManaBehaviour behaviour = Restore(context, 3);

            for (int i = 0; i < 5; i++)
                behaviour.FixedTick(context, 1f);

            Assert.That(context.GetCurrentMana().Value, Is.EqualTo(MaxMana + 2));
        }

        [Test]
        public void WhileFull_ThePeriodDoesNotRunDown()
        {
            IPlayerContext context = Context(MaxMana);
            RestoreManaBehaviour behaviour = Restore(context, 2);
            behaviour.FixedTick(context, 5f);

            context.GetCurrentMana().Value = 4;
            behaviour.FixedTick(context, 0.5f);

            Assert.That(context.GetCurrentMana().Value, Is.EqualTo(4));
        }
    }
}
