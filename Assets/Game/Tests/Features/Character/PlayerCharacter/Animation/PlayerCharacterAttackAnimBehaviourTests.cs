using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Tests
{
    public sealed class PlayerCharacterAttackAnimBehaviourTests
    {
        private static readonly int FireState = Animator.StringToHash("Fire");
        private static readonly int MeleeState = Animator.StringToHash("Melee");

        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
                if (created != null)
                    Object.DestroyImmediate(created);
            _created.Clear();
        }

        /// <summary>
        /// AC_BaseCharacter in miniature when <paramref name="withAttackLayers"/>: Base, Fire (layer 1)
        /// and Melee (layer 2), each resting in an Empty state. Without them it is AC_Rapax: one layer.
        /// </summary>
        private Animator CreateAnimator(bool withAttackLayers)
        {
            var controller = new AnimatorController { name = "Character" };
            _created.Add(controller);
            controller.AddLayer("Base Layer");
            controller.layers[0].stateMachine.AddState("Idle");

            if (withAttackLayers)
            {
                controller.AddLayer("Fire");
                controller.layers[1].stateMachine.AddState("Empty");
                controller.layers[1].stateMachine.AddState("Fire");
                controller.AddLayer("Melee");
                controller.layers[2].stateMachine.AddState("Empty");
                controller.layers[2].stateMachine.AddState("Melee");
            }

            var gameObject = new GameObject("Player");
            _created.Add(gameObject);
            var animator = gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Update(0f);
            return animator;
        }

        private static IEntity Fire(Animator animator, System.Action<IEntity> tag)
        {
            IEntity weapon = new Entity();
            weapon.AddFireEvent(new Atomic.Elements.Event());
            tag(weapon);

            IEntity character = new Entity();
            character.AddAnimator(animator);
            character.AddWeapon(new ReactiveVariable<IEntity>(weapon));
            new PlayerCharacterAttackAnimBehaviour().Init(character);

            weapon.GetFireEvent().Invoke();
            animator.Update(0f);
            return weapon;
        }

        [Test]
        public void RangeWeaponFires_PlaysFireOnTheFireLayer()
        {
            Animator animator = CreateAnimator(withAttackLayers: true);

            Fire(animator, weapon => weapon.AddRangeWeaponTag());

            Assert.That(animator.GetCurrentAnimatorStateInfo(1).shortNameHash, Is.EqualTo(FireState));
            Assert.That(animator.GetCurrentAnimatorStateInfo(2).shortNameHash, Is.Not.EqualTo(MeleeState));
        }

        [Test]
        public void MeleeWeaponFires_PlaysMeleeOnTheMeleeLayer()
        {
            Animator animator = CreateAnimator(withAttackLayers: true);

            Fire(animator, weapon => weapon.AddMeleeWeaponTag());

            Assert.That(animator.GetCurrentAnimatorStateInfo(2).shortNameHash, Is.EqualTo(MeleeState));
            Assert.That(animator.GetCurrentAnimatorStateInfo(1).shortNameHash, Is.Not.EqualTo(FireState));
        }

        [Test]
        public void ControllerWithoutAttackLayers_ShotPlaysNothing_AndLogsNoInvalidLayer()
        {
            Animator animator = CreateAnimator(withAttackLayers: false);

            Fire(animator, weapon =>
            {
                weapon.AddRangeWeaponTag();
                weapon.AddMeleeWeaponTag();
            });

            // An "Invalid Layer Index" error log would fail the test on its own.
            Assert.That(animator.layerCount, Is.EqualTo(1));
        }
    }
}
