using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// The locomotion blend reads where the character goes relative to where it faces: MoveX is
    /// sideways (right positive), MoveY forward.
    /// </summary>
    public sealed class CharacterMoveAnimBehaviourTests
    {
        private const float TOLERANCE = 1e-4f;

        private readonly List<Object> _created = new();
        private Animator _animator;
        private Transform _body;
        private IEntity _character;
        private bool _canMove = true;

        [SetUp]
        public void SetUp()
        {
            var gameObject = new GameObject("Character");
            _created.Add(gameObject);
            _body = gameObject.transform;
            _animator = gameObject.AddComponent<Animator>();

            var controller = new AnimatorController { name = "Locomotion" };
            _created.Add(controller);
            controller.AddLayer("Base Layer");
            controller.layers[0].stateMachine.AddState("Idle");
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            _animator.runtimeAnimatorController = controller;
            _animator.Rebind();

            _canMove = true;
            _character = new Entity();
            _character.AddAnimator(_animator);
            _character.AddTransform(_body);
            _character.AddMoveDirection(new ReactiveVariable<Vector3>());
            _character.AddRotateDirection(new ReactiveVariable<Vector3>());
            _character.AddMoveCondition(new AndExpression(new ExpressionMember<bool>(() => _canMove)));
            new CharacterMoveAnimBehaviour().Init(_character);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
                if (created != null)
                    Object.DestroyImmediate(created);
            _created.Clear();
        }

        private void Face(Vector3 direction) => _character.GetRotateDirection().Value = direction;

        private void Go(Vector3 direction) => _character.GetMoveDirection().Value = direction;

        [Test]
        public void MovingTheWayItFaces_RunsForward()
        {
            Face(Vector3.right);
            Go(Vector3.right);

            Assert.That(_animator.GetBool("IsMoving"), Is.True);
            Assert.That(_animator.GetFloat("MoveX"), Is.EqualTo(0f).Within(TOLERANCE));
            Assert.That(_animator.GetFloat("MoveY"), Is.EqualTo(1f).Within(TOLERANCE));
        }

        [Test]
        public void FacingRight_MovingWorldForward_StrafesLeft()
        {
            Face(Vector3.right);
            Go(Vector3.forward);

            Assert.That(_animator.GetFloat("MoveX"), Is.EqualTo(-1f).Within(TOLERANCE));
            Assert.That(_animator.GetFloat("MoveY"), Is.EqualTo(0f).Within(TOLERANCE));
        }

        [Test]
        public void FacingChangesWhileMoving_BlendFollowsTheNewFacing()
        {
            Face(Vector3.forward);
            Go(Vector3.forward);

            Face(Vector3.back);

            Assert.That(_animator.GetFloat("MoveY"), Is.EqualTo(-1f).Within(TOLERANCE));
        }

        [Test]
        public void NoFacingSet_UsesTheBodysOwnFacing()
        {
            _body.rotation = Quaternion.Euler(0f, 180f, 0f);

            Go(Vector3.forward);

            Assert.That(_animator.GetFloat("MoveX"), Is.EqualTo(0f).Within(TOLERANCE));
            Assert.That(_animator.GetFloat("MoveY"), Is.EqualTo(-1f).Within(TOLERANCE));
        }

        [Test]
        public void Stopping_ClearsIsMoving()
        {
            Face(Vector3.forward);
            Go(Vector3.forward);

            Go(Vector3.zero);

            Assert.That(_animator.GetBool("IsMoving"), Is.False);
        }

        [Test]
        public void NotAllowedToMove_DoesNotStartTheRun()
        {
            _canMove = false;
            Face(Vector3.forward);

            Go(Vector3.forward);

            Assert.That(_animator.GetBool("IsMoving"), Is.False);
        }
    }
}
