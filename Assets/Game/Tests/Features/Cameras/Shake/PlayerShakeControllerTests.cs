using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class PlayerShakeControllerTests
    {
        private sealed class TestPlayerContext : Entity, IPlayerContext
        {
        }

        private readonly List<CameraShakeArgs> _shakes = new();
        private IPlayerContext _context;
        private PlayerShakeController _controller;

        [SetUp]
        public void SetUp()
        {
            _shakes.Clear();

            var shake = new Event<CameraShakeArgs>();
            shake.OnEvent += _shakes.Add;
            IEntity camera = new Entity();
            camera.AddCameraShakeEvent(shake);

            _context = new TestPlayerContext();
            _context.AddCamera(camera);
            _context.AddCharacter(new ReactiveVariable<IEntity>());
            _controller = new PlayerShakeController();
        }

        private static IEntity Weapon(CameraShakeArgs args)
        {
            IEntity weapon = new Entity();
            weapon.AddFireEvent(new Event());
            if (args != null)
                weapon.AddCameraShakeArgs(args);
            return weapon;
        }

        private static IEntity Character(IEntity weapon)
        {
            IEntity character = new Entity();
            character.AddWeapon(new ReactiveVariable<IEntity>(weapon));
            return character;
        }

        private void Start(IEntity character)
        {
            _context.GetCharacter().Value = character;
            _controller.Init(_context);
        }

        [Test]
        public void WeaponInHandFires_CameraShakesWithItsArgs()
        {
            var args = new CameraShakeArgs();
            IEntity weapon = Weapon(args);
            Start(Character(weapon));

            weapon.GetFireEvent().Invoke();

            Assert.That(_shakes, Is.EqualTo(new[] { args }));
        }

        [Test]
        public void WeaponWithoutShake_Fires_CameraStaysStill()
        {
            IEntity weapon = Weapon(null);
            Start(Character(weapon));

            weapon.GetFireEvent().Invoke();

            Assert.That(_shakes, Is.Empty);
        }

        [Test]
        public void WeaponSwapped_ShakesWithTheNewWeaponsArgs_OldOneIsNotHeard()
        {
            IEntity oldWeapon = Weapon(new CameraShakeArgs());
            var newArgs = new CameraShakeArgs();
            IEntity newWeapon = Weapon(newArgs);
            IEntity character = Character(oldWeapon);
            Start(character);

            character.GetWeapon().Value = newWeapon;
            oldWeapon.GetFireEvent().Invoke();
            newWeapon.GetFireEvent().Invoke();

            Assert.That(_shakes, Is.EqualTo(new[] { newArgs }));
        }

        [Test]
        public void CharacterReplaced_ThePreviousOnesWeaponNoLongerShakes()
        {
            IEntity oldWeapon = Weapon(new CameraShakeArgs());
            var newArgs = new CameraShakeArgs();
            IEntity newWeapon = Weapon(newArgs);
            Start(Character(oldWeapon));

            _context.GetCharacter().Value = Character(newWeapon);
            oldWeapon.GetFireEvent().Invoke();
            newWeapon.GetFireEvent().Invoke();

            Assert.That(_shakes, Is.EqualTo(new[] { newArgs }));
        }

        [Test]
        public void NoCharacter_NothingToListenTo()
        {
            Assert.DoesNotThrow(() => Start(null));
            Assert.That(_shakes, Is.Empty);
        }

        [Test]
        public void AfterDispose_FiringNoLongerShakes()
        {
            IEntity weapon = Weapon(new CameraShakeArgs());
            Start(Character(weapon));

            _controller.Dispose(_context);
            weapon.GetFireEvent().Invoke();

            Assert.That(_shakes, Is.Empty);
        }
    }
}
