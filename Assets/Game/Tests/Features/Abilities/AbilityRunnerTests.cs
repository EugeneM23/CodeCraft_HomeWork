using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class AbilityRunnerTests
    {
        private sealed class TestPlayerContext : Entity, IPlayerContext
        {
        }

        private static IPlayerContext Context(params Ability[] abilities)
        {
            IPlayerContext context = new TestPlayerContext();
            var dictionary = new ReactiveDictionary<string, Ability>();
            foreach (Ability ability in abilities)
                dictionary.Add(ability.Name, ability);
            context.AddAbilities(dictionary);
            return context;
        }

        private static Ability TickCounting(string name, List<string> log)
        {
            var ability = new Ability(name);
            ability.WhenTick(dt => log.Add(System.FormattableString.Invariant($"{name} tick {dt}")));
            ability.WhenFixedTick(dt => log.Add(System.FormattableString.Invariant($"{name} fixed {dt}")));
            ability.WhenLateTick(dt => log.Add(System.FormattableString.Invariant($"{name} late {dt}")));
            return ability;
        }

        [Test]
        public void Init_InitializesEveryAbilityOfTheContext()
        {
            var dash = new Ability("Dash");
            var stun = new Ability("Stun");
            IPlayerContext context = Context(dash, stun);

            new AbilityRunner().Init(context);

            Assert.That(dash.Initialized, Is.True);
            Assert.That(stun.Initialized, Is.True);
        }

        [Test]
        public void Enabled_ForwardsTicksWithTheirDeltaTime()
        {
            var log = new List<string>();
            IPlayerContext context = Context(TickCounting("Dash", log));
            var runner = new AbilityRunner();
            runner.Init(context);
            runner.Enable(context);

            runner.Tick(context, 0.5f);
            runner.FixedTick(context, 0.25f);
            runner.LateTick(context, 0.125f);

            Assert.That(log, Is.EqualTo(new[] { "Dash tick 0.5", "Dash fixed 0.25", "Dash late 0.125" }));
        }

        [Test]
        public void NotYetEnabled_TicksAreIgnored()
        {
            var log = new List<string>();
            IPlayerContext context = Context(TickCounting("Dash", log));
            var runner = new AbilityRunner();
            runner.Init(context);

            runner.Tick(context, 0.5f);

            Assert.That(log, Is.Empty);
        }

        [Test]
        public void Disabled_StopsForwardingTicks()
        {
            var log = new List<string>();
            IPlayerContext context = Context(TickCounting("Dash", log));
            var runner = new AbilityRunner();
            runner.Init(context);
            runner.Enable(context);

            runner.Disable(context);
            runner.Tick(context, 0.5f);

            Assert.That(log, Is.Empty);
        }

        [Test]
        public void Dispose_DeinitializesTheAbilities()
        {
            var dash = new Ability("Dash");
            IPlayerContext context = Context(dash);
            var runner = new AbilityRunner();
            runner.Init(context);
            runner.Enable(context);

            runner.Dispose(context);

            Assert.That(dash.Initialized, Is.False);
            Assert.That(dash.Enabled, Is.False);
        }
    }
}
