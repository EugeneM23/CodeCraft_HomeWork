using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Covers SwitchAnimatorUseCase through the behaviour that drives it: the animator runs the
    /// controller of whatever weapon is in hand.
    /// </summary>
    public sealed class SwitchAnimatorBehaviourTests
    {
        private readonly List<Object> _created = new();
        private Animator _animator;

        [SetUp]
        public void SetUp()
        {
            var gameObject = new GameObject("Character");
            _created.Add(gameObject);
            _animator = gameObject.AddComponent<Animator>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
                if (created != null)
                    Object.DestroyImmediate(created);
            _created.Clear();
        }

        /// <summary>An in-memory controller with the Idle state the switch plays.</summary>
        private AnimatorController Controller(string name)
        {
            var controller = new AnimatorController { name = name };
            controller.AddLayer("Base Layer");
            controller.layers[0].stateMachine.AddState("Idle");
            _created.Add(controller);
            return controller;
        }

        private static IEntity Weapon(RuntimeAnimatorController controller)
        {
            IEntity weapon = new Entity();
            weapon.AddAnimationController(controller);
            return weapon;
        }

        private IEntity Character(IEntity weapon)
        {
            IEntity character = new Entity();
            character.AddAnimator(_animator);
            character.AddWeapon(new ReactiveVariable<IEntity>(weapon));
            return character;
        }

        [Test]
        public void UseCase_PutsTheWeaponsControllerOnTheAnimator()
        {
            AnimatorController rifle = Controller("Rifle");

            SwitchAnimatorUseCase.Switch(_animator, Weapon(rifle));

            Assert.That(_animator.runtimeAnimatorController, Is.SameAs(rifle));
        }

        [Test]
        public void Init_TakesTheControllerOfTheWeaponAlreadyInHand()
        {
            AnimatorController rifle = Controller("Rifle");
            var behaviour = new SwitchAnimatorBehaviour();

            behaviour.Init(Character(Weapon(rifle)));

            Assert.That(_animator.runtimeAnimatorController, Is.SameAs(rifle));
        }

        [Test]
        public void WeaponSwapped_TakesTheNewWeaponsController()
        {
            AnimatorController rifle = Controller("Rifle");
            AnimatorController knife = Controller("Knife");
            IEntity character = Character(Weapon(rifle));
            var behaviour = new SwitchAnimatorBehaviour();
            behaviour.Init(character);

            character.GetWeapon().Value = Weapon(knife);

            Assert.That(_animator.runtimeAnimatorController, Is.SameAs(knife));
        }

        [Test]
        public void AfterDispose_WeaponSwapLeavesTheControllerAlone()
        {
            AnimatorController rifle = Controller("Rifle");
            AnimatorController knife = Controller("Knife");
            IEntity character = Character(Weapon(rifle));
            var behaviour = new SwitchAnimatorBehaviour();
            behaviour.Init(character);

            behaviour.Dispose(character);
            character.GetWeapon().Value = Weapon(knife);

            Assert.That(_animator.runtimeAnimatorController, Is.SameAs(rifle));
        }
    }
}
