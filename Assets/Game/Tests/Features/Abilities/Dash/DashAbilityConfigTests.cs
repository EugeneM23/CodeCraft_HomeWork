using System.Reflection;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class DashAbilityConfigTests
    {
        private sealed class TestPlayerContext : Entity, IPlayerContext
        {
        }

        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private DashAbilityConfig _config;
        private MoveSpeedBuffConfig _dashBuff;

        [SetUp]
        public void SetUp()
        {
            _dashBuff = ScriptableObject.CreateInstance<MoveSpeedBuffConfig>();
            typeof(BuffConfig).GetField("<Name>k__BackingField", Private).SetValue(_dashBuff, "Dash");
            typeof(BuffConfig).GetField("<Duration>k__BackingField", Private).SetValue(_dashBuff, 0.5f);
            typeof(MoveSpeedBuffConfig).GetField("<Speed>k__BackingField", Private).SetValue(_dashBuff, 3f);

            _config = ScriptableObject.CreateInstance<DashAbilityConfig>();
            typeof(AbilityConfig).GetField("<Name>k__BackingField", Private).SetValue(_config, "Dash");
            typeof(DashAbilityConfig).GetField("_initialCharges", Private).SetValue(_config, 2);
            typeof(DashAbilityConfig).GetField("_dashBuff", Private).SetValue(_config, _dashBuff);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_dashBuff);
        }

        private static IEntity Character()
        {
            IEntity character = new Entity();
            character.AddMoveSpeed(new ReactiveFloat(2f));
            character.AddBuffsEffects(new ReactiveList<BuffBase>());
            return character;
        }

        private static IPlayerContext Context(IEntity character)
        {
            IPlayerContext context = new TestPlayerContext();
            context.AddCharacter(new ReactiveVariable<IEntity>(character));
            return context;
        }

        [Test]
        public void Create_NamesTheAbilityAndStartsWithTheConfiguredCharges()
        {
            Ability ability = _config.Create(Context(Character()));

            Assert.That(ability.Name, Is.EqualTo("Dash"));
            Assert.That(ability.HasBaseAbilityTag(), Is.True);
            Assert.That(ability.GetCharges().Value, Is.EqualTo(2));
        }

        [Test]
        public void Use_SpendsAChargeAndBuffsTheCharacter()
        {
            IEntity character = Character();
            Ability ability = _config.Create(Context(character));

            AbilityUseCase.Use(ability);

            Assert.That(ability.GetCharges().Value, Is.EqualTo(1));
            Assert.That(character.GetMoveSpeed().Value, Is.EqualTo(6f).Within(1e-4f));
        }

        [Test]
        public void Use_WhileAlreadyDashing_KeepsTheCharge()
        {
            IEntity character = Character();
            Ability ability = _config.Create(Context(character));
            AbilityUseCase.Use(ability);

            AbilityUseCase.Use(ability);

            Assert.That(ability.GetCharges().Value, Is.EqualTo(1));
            Assert.That(character.GetMoveSpeed().Value, Is.EqualTo(6f).Within(1e-4f));
        }

        [Test]
        public void Use_OutOfCharges_DoesNothing()
        {
            IEntity character = Character();
            Ability ability = _config.Create(Context(character));
            ability.GetCharges().Value = 0;

            AbilityUseCase.Use(ability);

            Assert.That(ability.GetCharges().Value, Is.Zero);
            Assert.That(character.GetBuffsEffects(), Is.Empty);
        }

        [Test]
        public void Use_InAVehicle_DoesNothing()
        {
            IEntity vehicle = Character();
            vehicle.AddVehicleTag();
            Ability ability = _config.Create(Context(vehicle));

            AbilityUseCase.Use(ability);

            Assert.That(ability.GetCharges().Value, Is.EqualTo(2));
            Assert.That(vehicle.GetBuffsEffects(), Is.Empty);
        }

        [Test]
        public void Use_PlayerDead_DoesNothing()
        {
            Ability ability = _config.Create(Context(null));

            Assert.DoesNotThrow(() => AbilityUseCase.Use(ability));
            Assert.That(ability.GetCharges().Value, Is.EqualTo(2));
        }

        [Test]
        public void Use_AfterRespawn_BuffsTheNewBody()
        {
            IEntity oldBody = Character();
            IEntity newBody = Character();
            IPlayerContext context = Context(oldBody);
            Ability ability = _config.Create(context);

            context.GetCharacter().Value = newBody;
            AbilityUseCase.Use(ability);

            Assert.That(newBody.GetBuffsEffects(), Is.Not.Empty);
            Assert.That(oldBody.GetBuffsEffects(), Is.Empty);
        }
    }
}
