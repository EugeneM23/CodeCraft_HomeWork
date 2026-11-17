using System.Reflection;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// The ability's own wiring — mana and target checks. The stun buff is swapped for a plain
    /// slow so the target needs no Transform or Animator; StunBuff itself has StunBuffTests.
    /// </summary>
    public sealed class StunAbilityConfigTests
    {
        private sealed class TestPlayerContext : Entity, IPlayerContext
        {
        }

        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const int ManaCost = 2;

        private StunAbilityConfig _config;
        private MoveSpeedBuffConfig _stunBuff;

        [SetUp]
        public void SetUp()
        {
            _stunBuff = ScriptableObject.CreateInstance<MoveSpeedBuffConfig>();
            typeof(BuffConfig).GetField("<Name>k__BackingField", Private).SetValue(_stunBuff, "Stun");
            typeof(MoveSpeedBuffConfig).GetField("<Speed>k__BackingField", Private).SetValue(_stunBuff, 0.5f);

            _config = ScriptableObject.CreateInstance<StunAbilityConfig>();
            typeof(StunAbilityConfig).GetField("_manaCost", Private).SetValue(_config, ManaCost);
            typeof(StunAbilityConfig).GetField("_stunBuff", Private).SetValue(_config, _stunBuff);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_stunBuff);
        }

        private static IPlayerContext Context(int mana)
        {
            IPlayerContext context = new TestPlayerContext();
            context.AddCurrentMana(new ReactiveInt(mana));
            return context;
        }

        private static IEntity Target()
        {
            IEntity target = new Entity();
            target.AddMoveSpeed(new ReactiveFloat(4f));
            target.AddBuffsEffects(new ReactiveList<BuffBase>());
            return target;
        }

        [Test]
        public void Use_EnoughMana_SpendsItAndStunsTheTarget()
        {
            IPlayerContext context = Context(5);
            IEntity target = Target();
            Ability ability = _config.Create(context);

            AbilityUseCase.Use(ability, target);

            Assert.That(context.GetCurrentMana().Value, Is.EqualTo(3));
            Assert.That(target.GetMoveSpeed().Value, Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void Use_NotEnoughMana_TargetUntouched()
        {
            IPlayerContext context = Context(1);
            IEntity target = Target();
            Ability ability = _config.Create(context);

            AbilityUseCase.Use(ability, target);

            Assert.That(context.GetCurrentMana().Value, Is.EqualTo(1));
            Assert.That(target.GetBuffsEffects(), Is.Empty);
        }

        [Test]
        public void Use_NoTarget_KeepsTheMana()
        {
            IPlayerContext context = Context(5);
            Ability ability = _config.Create(context);

            Assert.DoesNotThrow(() => AbilityUseCase.Use(ability, (IEntity)null));
            Assert.That(context.GetCurrentMana().Value, Is.EqualTo(5));
        }

        [Test]
        public void Create_PublishesTheManaCost()
        {
            Ability ability = _config.Create(Context(5));

            Assert.That(ability.GetManaCost().Value, Is.EqualTo(ManaCost));
        }

        /// <summary>
        /// The condition checks only the mana, not whether the target can take the stun: a second
        /// cast on a target that is still stunned pays the mana and BuffUseCase.Apply refuses the
        /// duplicate. The dash checks BuffUseCase.CanApply in its condition for exactly this.
        /// </summary>
        [Test]
        public void Use_TargetAlreadyStunned_DoesNotSpendMana()
        {
            IPlayerContext context = Context(5);
            IEntity target = Target();
            Ability ability = _config.Create(context);
            AbilityUseCase.Use(ability, target);

            AbilityUseCase.Use(ability, target);

            Assert.That(context.GetCurrentMana().Value, Is.EqualTo(3));
        }
    }
}
