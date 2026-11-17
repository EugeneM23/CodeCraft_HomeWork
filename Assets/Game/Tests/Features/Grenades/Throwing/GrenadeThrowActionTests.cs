using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Only the refusals: a real throw rents from GameContext's factory, which does not exist in EditMode.
    /// </summary>
    public sealed class GrenadeThrowActionTests
    {
        private static IEntity Thrower(Dictionary<GrenadeType, MonoEntity> prefabs, out Ammo flashes,
            out CooldownTimer cooldown)
        {
            flashes = new Ammo(3);
            cooldown = new CooldownTimer(0.8f);

            IEntity character = new Entity();
            character.AddGrenadePrefabs(prefabs);
            character.AddGrenadeSelected(new ReactiveVariable<GrenadeType>(GrenadeType.Flash));
            character.AddGrenadeAmmo(new Dictionary<GrenadeType, Ammo> { [GrenadeType.Flash] = flashes });
            character.AddGrenadeCooldown(cooldown);
            return character;
        }

        private static void Throw(IEntity character) =>
            new GrenadeThrowAction(character, null, 15f, 12f, 0.6f).Invoke(new Vector3(5f, 0f, 5f));

        [Test]
        public void SelectedTypeNotCarried_NothingIsSpent()
        {
            IEntity character = Thrower(new Dictionary<GrenadeType, MonoEntity> { [GrenadeType.Frag] = null },
                out Ammo flashes, out CooldownTimer cooldown);

            Throw(character);

            Assert.That(flashes.GetCount(), Is.EqualTo(3));
            Assert.That(cooldown.IsExpired(), Is.True, "the cooldown was not started");
        }

        [Test]
        public void SelectedTypesPrefabIsMissing_NothingIsSpent()
        {
            IEntity character = Thrower(new Dictionary<GrenadeType, MonoEntity> { [GrenadeType.Flash] = null },
                out Ammo flashes, out CooldownTimer cooldown);

            Throw(character);

            Assert.That(flashes.GetCount(), Is.EqualTo(3));
            Assert.That(cooldown.IsExpired(), Is.True, "the cooldown was not started");
        }
    }
}
