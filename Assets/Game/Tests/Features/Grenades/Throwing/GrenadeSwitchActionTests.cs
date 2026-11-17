using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class GrenadeSwitchActionTests
    {
        private static IEntity Thrower(GrenadeType selected, params GrenadeType[] carried)
        {
            var prefabs = new Dictionary<GrenadeType, MonoEntity>();
            foreach (GrenadeType type in carried)
                prefabs[type] = null; // the switch only asks whether a type is carried

            IEntity character = new Entity();
            character.AddGrenadePrefabs(prefabs);
            character.AddGrenadeSelected(new ReactiveVariable<GrenadeType>(selected));
            return character;
        }

        [Test]
        public void BothTypesCarried_SelectsTheNext_AndWrapsRound()
        {
            IEntity character = Thrower(GrenadeType.Frag, GrenadeType.Frag, GrenadeType.Flash);
            var action = new GrenadeSwitchAction(character);

            action.Invoke();
            GrenadeType afterFirst = character.GetGrenadeSelected().Value;
            action.Invoke();

            Assert.That(afterFirst, Is.EqualTo(GrenadeType.Flash));
            Assert.That(character.GetGrenadeSelected().Value, Is.EqualTo(GrenadeType.Frag));
        }

        [Test]
        public void OnlyOneTypeCarried_SelectionStays()
        {
            IEntity character = Thrower(GrenadeType.Frag, GrenadeType.Frag);

            new GrenadeSwitchAction(character).Invoke();

            Assert.That(character.GetGrenadeSelected().Value, Is.EqualTo(GrenadeType.Frag));
        }

        [Test]
        public void SelectedTypeNotCarried_MovesToOneThatIs()
        {
            IEntity character = Thrower(GrenadeType.Flash, GrenadeType.Frag);

            new GrenadeSwitchAction(character).Invoke();

            Assert.That(character.GetGrenadeSelected().Value, Is.EqualTo(GrenadeType.Frag));
        }

        [Test]
        public void NothingCarried_SelectionIsLeftAlone()
        {
            IEntity character = Thrower(GrenadeType.Flash);

            new GrenadeSwitchAction(character).Invoke();

            Assert.That(character.GetGrenadeSelected().Value, Is.EqualTo(GrenadeType.Flash));
        }
    }
}
