using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class RangeWeaponFireActionTests
    {
        private GameContext _context;
        private GameObject _poolsParent;

        [SetUp]
        public void SetUp()
        {
            _context = MonoEntity.Create<GameContext>(name: "GameContext");
            _poolsParent = new GameObject("Pools");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_context.gameObject);
            Object.DestroyImmediate(_poolsParent);
        }

        [Test]
        public void Shot_SpendsARound_RestartsTheCooldown_AndAnnouncesIt()
        {
            _context.AddGameFactory(new GameFactory(_poolsParent.transform));
            var ammo = new Ammo(5);
            var cooldown = new CooldownTimer(0.3f);
            int shots = 0;
            IEntity weapon = new Entity();
            weapon.AddAmmo(ammo);
            weapon.AddWeaponCooldown(cooldown);
            weapon.AddFireEvent(new Atomic.Elements.Event());
            weapon.GetFireEvent().Subscribe(() => shots++);

            new RangeWeaponFireAction(weapon, _context).Invoke();

            Assert.That(ammo.GetCount(), Is.EqualTo(4));
            Assert.That(cooldown.IsExpired(), Is.False);
            Assert.That(cooldown.GetProgress(), Is.EqualTo(1f));
            Assert.That(shots, Is.EqualTo(1));
        }

        [Test]
        public void WeaponWithoutAmmoOrCooldown_StillFires()
        {
            _context.AddGameFactory(new GameFactory(_poolsParent.transform));
            int shots = 0;
            IEntity weapon = new Entity();
            weapon.AddFireEvent(new Atomic.Elements.Event());
            weapon.GetFireEvent().Subscribe(() => shots++);

            new RangeWeaponFireAction(weapon, _context).Invoke();

            Assert.That(shots, Is.EqualTo(1));
        }

        [Test]
        public void FactoryInstalledAfterTheWeapon_IsFoundOnTheFirstShot()
        {
            // A scene weapon (the tank's cannon) is installed before the context has its factory.
            IEntity weapon = new Entity();
            weapon.AddAmmo(new Ammo(1));
            var action = new RangeWeaponFireAction(weapon, _context);
            _context.AddGameFactory(new GameFactory(_poolsParent.transform));

            Assert.DoesNotThrow(action.Invoke);
            Assert.That(weapon.GetAmmo().GetCount(), Is.Zero);
        }
    }
}
