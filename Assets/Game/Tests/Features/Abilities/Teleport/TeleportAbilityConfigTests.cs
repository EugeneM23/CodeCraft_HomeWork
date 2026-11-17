using System.Reflection;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class TeleportAbilityConfigTests
    {
        private sealed class TestPlayerContext : Entity, IPlayerContext
        {
        }

        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const int ManaCost = 2;
        private const float Radius = 5f;

        private TeleportAbilityConfig _config;
        private GameObject _body;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<TeleportAbilityConfig>();
            typeof(TeleportAbilityConfig).GetField("_manaCost", Private).SetValue(_config, ManaCost);
            typeof(TeleportAbilityConfig).GetField("_radius", Private).SetValue(_config, Radius);
            _body = new GameObject("Character");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_body);
        }

        private IEntity Character()
        {
            IEntity character = new Entity();
            character.AddTransform(_body.transform);
            return character;
        }

        private static IPlayerContext Context(IEntity character, int mana = 5)
        {
            IPlayerContext context = new TestPlayerContext();
            context.AddCharacter(new ReactiveVariable<IEntity>(character));
            context.AddCurrentMana(new ReactiveInt(mana));
            return context;
        }

        [Test]
        public void Use_WithinRadius_MovesTheCharacterAndSpendsMana()
        {
            IPlayerContext context = Context(Character());
            Ability ability = _config.Create(context);
            var point = new Vector3(3f, 0f, 4f);

            AbilityUseCase.Use(ability, point);

            Assert.That(_body.transform.position, Is.EqualTo(point));
            Assert.That(context.GetCurrentMana().Value, Is.EqualTo(3));
        }

        [Test]
        public void Use_BeyondRadius_StaysAndKeepsMana()
        {
            IPlayerContext context = Context(Character());
            Ability ability = _config.Create(context);

            AbilityUseCase.Use(ability, new Vector3(Radius + 0.1f, 0f, 0f));

            Assert.That(_body.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(context.GetCurrentMana().Value, Is.EqualTo(5));
        }

        [Test]
        public void Use_NotEnoughMana_Stays()
        {
            IPlayerContext context = Context(Character(), mana: 1);
            Ability ability = _config.Create(context);

            AbilityUseCase.Use(ability, new Vector3(1f, 0f, 0f));

            Assert.That(_body.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(context.GetCurrentMana().Value, Is.EqualTo(1));
        }

        [Test]
        public void Use_InAVehicle_Stays()
        {
            IEntity vehicle = Character();
            vehicle.AddVehicleTag();
            IPlayerContext context = Context(vehicle);
            Ability ability = _config.Create(context);

            AbilityUseCase.Use(ability, new Vector3(1f, 0f, 0f));

            Assert.That(_body.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(context.GetCurrentMana().Value, Is.EqualTo(5));
        }

        [Test]
        public void Use_PlayerDead_DoesNothing()
        {
            IPlayerContext context = Context(null);
            Ability ability = _config.Create(context);

            Assert.DoesNotThrow(() => AbilityUseCase.Use(ability, new Vector3(1f, 0f, 0f)));
            Assert.That(context.GetCurrentMana().Value, Is.EqualTo(5));
        }

        [Test]
        public void Use_PhysicsBody_MovesTheBodyAndDropsItsVelocity()
        {
            var body = _body.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.linearVelocity = new Vector3(4f, 0f, 0f);
            IEntity character = Character();
            character.AddRiggedBody(body);
            Ability ability = _config.Create(Context(character));
            var point = new Vector3(0f, 0f, 3f);

            AbilityUseCase.Use(ability, point);

            Assert.That(body.position, Is.EqualTo(point));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Create_PublishesManaCostAndRadius()
        {
            Ability ability = _config.Create(Context(Character()));

            Assert.That(ability.GetManaCost().Value, Is.EqualTo(ManaCost));
            Assert.That(ability.GetRadius().Value, Is.EqualTo(Radius));
        }
    }
}
