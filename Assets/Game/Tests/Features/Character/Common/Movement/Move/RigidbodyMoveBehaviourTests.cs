using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// The velocity the behaviour hands the body each physics step. The wall cast itself is
    /// covered by RigidbodyMoveCastTests; these cover what is done with it.
    /// </summary>
    public sealed class RigidbodyMoveBehaviourTests
    {
        private const float SPEED = 5f;
        private const float TOLERANCE = 1e-3f;

        private readonly List<GameObject> _created = new();
        private Rigidbody _body;
        private CapsuleCollider _capsule;
        private IEntity _character;
        private RigidbodyMoveBehaviour _behaviour;
        private bool _canMove;

        [SetUp]
        public void SetUp()
        {
            _canMove = true;
            GameObject character = Track(new GameObject("Character"));
            character.layer = PhysicsLayers.Character;
            _capsule = character.AddComponent<CapsuleCollider>();
            _capsule.radius = 0.38f;
            _capsule.height = 2.16f;
            _capsule.center = new Vector3(0f, 1.08f, 0f);
            _body = character.AddComponent<Rigidbody>();
            _body.useGravity = false;

            _character = new Entity();
            _character.AddRiggedBody(_body);
            _character.AddMoveSpeed(new ReactiveFloat(SPEED));
            _character.AddMoveDirection(new ReactiveVariable<Vector3>());
            _character.AddMoveCondition(new AndExpression(new ExpressionMember<bool>(() => _canMove)));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _created)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);
            _created.Clear();
        }

        private GameObject Track(GameObject gameObject)
        {
            _created.Add(gameObject);
            return gameObject;
        }

        private void Init()
        {
            _behaviour = new RigidbodyMoveBehaviour();
            _behaviour.Init(_character);
            Physics.SyncTransforms();
        }

        private Vector3 Step(Vector3 direction, float deltaTime = 0.02f)
        {
            _character.GetMoveDirection().Value = direction;
            _behaviour.FixedTick(_character, deltaTime);
            return _body.linearVelocity;
        }

        /// <summary>A tall wall across the way forward, its face <paramref name="gap"/> past the capsule.</summary>
        private void WallAhead(float gap)
        {
            GameObject wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.localScale = new Vector3(20f, 4f, 1f);
            wall.transform.position = new Vector3(0f, 2f, _capsule.radius + gap + 0.5f);
            Physics.SyncTransforms();
        }

        [Test]
        public void Init_GivesABareCapsuleAFrictionlessMaterial()
        {
            Init();

            Assert.That(_capsule.sharedMaterial, Is.Not.Null);
            Assert.That(_capsule.sharedMaterial.dynamicFriction, Is.Zero);
            Assert.That(_capsule.sharedMaterial.staticFriction, Is.Zero);
            Assert.That(_capsule.sharedMaterial.frictionCombine, Is.EqualTo(PhysicsMaterialCombine.Minimum));
        }

        [Test]
        public void Init_KeepsAMaterialTheCapsuleAlreadyHas()
        {
            var own = new PhysicsMaterial("Own");
            _capsule.sharedMaterial = own;

            Init();

            Assert.That(_capsule.sharedMaterial, Is.SameAs(own));
            Object.DestroyImmediate(own);
        }

        [Test]
        public void OpenGround_DrivesTheBodyAtMoveSpeed_LeavingFallingAlone()
        {
            Init();
            _body.linearVelocity = new Vector3(0f, -2f, 0f);

            Vector3 velocity = Step(Vector3.forward);

            Assert.That(velocity.x, Is.EqualTo(0f).Within(TOLERANCE));
            Assert.That(velocity.y, Is.EqualTo(-2f).Within(TOLERANCE));
            Assert.That(velocity.z, Is.EqualTo(SPEED).Within(TOLERANCE));
        }

        [Test]
        public void UpwardPartOfTheDirection_IsIgnored()
        {
            Init();

            Vector3 velocity = Step(new Vector3(1f, 1f, 0f));

            Assert.That(velocity.x, Is.EqualTo(SPEED).Within(TOLERANCE));
            Assert.That(velocity.y, Is.EqualTo(0f).Within(TOLERANCE));
        }

        [Test]
        public void NotAllowedToMove_BrakesToAStop_InsteadOfGlidingOn()
        {
            Init();
            Step(Vector3.forward);
            _canMove = false;

            Vector3 velocity = Step(Vector3.forward);

            Assert.That(new Vector2(velocity.x, velocity.z).magnitude, Is.EqualTo(0f).Within(TOLERANCE));
        }

        [Test]
        public void SolverLaunchingTheBodyUp_IsCappedAtTheDepenetrationAllowance()
        {
            Init();
            _body.linearVelocity = new Vector3(0f, 10f, 0f);

            Vector3 velocity = Step(Vector3.zero);

            Assert.That(velocity.y, Is.EqualTo(_body.maxDepenetrationVelocity).Within(TOLERANCE));
            Assert.That(_body.maxDepenetrationVelocity, Is.EqualTo(1f));
        }

        [Test]
        public void JumpingCharacter_MayRiseAsFastAsTheJumpImpulseAllows()
        {
            _character.AddJumpForce(6f);
            _body.mass = 2f;
            Init();
            _body.linearVelocity = new Vector3(0f, 10f, 0f);

            Vector3 velocity = Step(Vector3.zero);

            Assert.That(velocity.y, Is.EqualTo(3f).Within(TOLERANCE));
        }

        [Test]
        public void RisingSlowerThanTheCap_IsLeftAlone()
        {
            Init();
            _body.linearVelocity = new Vector3(0f, 0.5f, 0f);

            Vector3 velocity = Step(Vector3.zero);

            Assert.That(velocity.y, Is.EqualTo(0.5f).Within(TOLERANCE));
        }

        [Test]
        public void HeadOnIntoAWall_Stops()
        {
            Init();
            WallAhead(0.05f);

            Vector3 velocity = Step(Vector3.forward, 0.1f);

            Assert.That(new Vector2(velocity.x, velocity.z).magnitude, Is.EqualTo(0f).Within(TOLERANCE));
        }

        [Test]
        public void DiagonallyIntoAWall_SlidesAlongIt_WithTheAlongTheWallSpeed()
        {
            Init();
            WallAhead(0.05f);

            Vector3 velocity = Step(new Vector3(1f, 0f, 1f).normalized, 0.1f);

            Assert.That(velocity.x, Is.EqualTo(SPEED * Mathf.Sqrt(0.5f)).Within(TOLERANCE));
            Assert.That(velocity.z, Is.EqualTo(0f).Within(TOLERANCE));
        }

        [Test]
        public void WallFurtherThanThisStepReaches_DoesNotSlowTheCharacter()
        {
            Init();
            WallAhead(1f);

            Vector3 velocity = Step(Vector3.forward, 0.02f);

            Assert.That(velocity.z, Is.EqualTo(SPEED).Within(TOLERANCE));
        }

        [Test]
        public void LowLedgeUnderTheFoot_IsSteppedOver_NotSlidAlong()
        {
            Init();
            // A crater rim: 4 cm high, right in front of the capsule.
            GameObject ledge = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            ledge.transform.localScale = new Vector3(20f, 0.04f, 1f);
            ledge.transform.position = new Vector3(0f, 0.02f, _capsule.radius + 0.05f + 0.5f);
            Physics.SyncTransforms();

            Vector3 velocity = Step(Vector3.forward, 0.1f);

            Assert.That(velocity.z, Is.EqualTo(SPEED).Within(TOLERANCE));
        }
    }
}
