using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Tests
{
    public sealed class CharacterAttackAnimBehaviourTests
    {
        private readonly List<Object> _created = new();
        private Animator _animator;
        private IEntity _character;
        private IEntity _weapon;
        private CharacterAttackAnimBehaviour _behaviour;

        [SetUp]
        public void SetUp()
        {
            var gameObject = new GameObject("Zombie");
            _created.Add(gameObject);
            _animator = gameObject.AddComponent<Animator>();

            // AC_Zombie in miniature: an Attack trigger and somewhere for it to lead.
            var controller = new AnimatorController { name = "Zombie" };
            _created.Add(controller);
            controller.AddLayer("Base Layer");
            controller.layers[0].stateMachine.AddState("Idle");
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            _animator.runtimeAnimatorController = controller;
            _animator.Rebind();

            _weapon = Weapon();
            _character = new Entity();
            _character.AddAnimator(_animator);
            _character.AddWeapon(new ReactiveVariable<IEntity>(_weapon));
            _behaviour = new CharacterAttackAnimBehaviour();
            _behaviour.Init(_character);
        }

        [TearDown]
        public void TearDown()
        {
            _behaviour.Dispose(_character);
            foreach (Object created in _created)
                if (created != null)
                    Object.DestroyImmediate(created);
            _created.Clear();
        }

        private static IEntity Weapon()
        {
            IEntity weapon = new Entity();
            weapon.AddFireEvent(new Atomic.Elements.Event());
            return weapon;
        }

        [Test]
        public void WeaponInHandFires_SetsTheAttackTrigger()
        {
            _weapon.GetFireEvent().Invoke();

            Assert.That(_animator.GetBool("Attack"), Is.True);
        }

        [Test]
        public void NothingFired_TriggerStaysOff()
        {
            Assert.That(_animator.GetBool("Attack"), Is.False);
        }

        [Test]
        public void WeaponPutAwayThenFired_DoesNotSetTheTrigger()
        {
            IEntity dropped = _weapon;
            _character.GetWeapon().Value = Weapon();

            dropped.GetFireEvent().Invoke();

            Assert.That(_animator.GetBool("Attack"), Is.False);
        }

        [Test]
        public void AfterDispose_FiringDoesNotSetTheTrigger()
        {
            _behaviour.Dispose(_character);

            _weapon.GetFireEvent().Invoke();

            Assert.That(_animator.GetBool("Attack"), Is.False);
        }
    }
}
