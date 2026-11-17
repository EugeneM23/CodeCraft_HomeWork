using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class CharacterAttackUseCaseTests
    {
        private readonly List<GameObject> _created = new();
        private int _shots;

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _created)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);
            _created.Clear();
        }

        /// <summary>
        /// A MonoEntity built by AddComponent has not been through deserialization, which is
        /// where it allocates its value storage; running that callback by hand stands in for it.
        /// </summary>
        private MonoEntity Weapon()
        {
            var gameObject = new GameObject("Weapon");
            _created.Add(gameObject);
            var weapon = gameObject.AddComponent<MonoEntity>();
            ((ISerializationCallbackReceiver)weapon).OnAfterDeserialize();
            weapon.AddFireCondition(new AndExpression());
            weapon.AddFireAction(new InlineAction(() => _shots++));
            return weapon;
        }

        private IEntity Character(out Health health, System.Func<bool> extraCondition = null)
        {
            health = new Health(10, 10);
            IEntity character = new Entity();
            character.AddHealth(health);
            CharacterAttackUseCase.AddHeldWeapon(character, this.Weapon(), extraCondition);
            return character;
        }

        [Test]
        public void Attack_FiresTheWeaponInHand()
        {
            IEntity character = Character(out _);

            character.GetFireAction().Invoke();

            Assert.That(_shots, Is.EqualTo(1));
        }

        [Test]
        public void Alive_MayAttack()
        {
            IEntity character = Character(out _);

            Assert.That(character.GetFireCondition().Invoke(), Is.True);
        }

        [Test]
        public void Dead_MayNotAttack()
        {
            IEntity character = Character(out Health health);

            health.Reduce(10);

            Assert.That(character.GetFireCondition().Invoke(), Is.False);
        }

        [Test]
        public void ExtraConditionFalse_MayNotAttack_ThoughAlive()
        {
            bool rider = false;
            IEntity character = Character(out _, () => rider);

            Assert.That(character.GetFireCondition().Invoke(), Is.False);

            rider = true;
            Assert.That(character.GetFireCondition().Invoke(), Is.True);
        }
    }
}
