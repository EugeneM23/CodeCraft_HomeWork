using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MoveUseCaseTests
    {
        private const float TOLERANCE = 1e-4f;

        private readonly List<GameObject> _created = new();
        private bool _canMove;

        [SetUp]
        public void SetUp() => _canMove = true;

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

        private IEntity Mover(GameObject body, float speed, Vector3 direction, bool withCondition = true)
        {
            IEntity entity = new Entity();
            entity.AddTransform(body.transform);
            entity.AddMoveSpeed(new ReactiveFloat(speed));
            entity.AddMoveDirection(new ReactiveVariable<Vector3>(direction));
            if (withCondition)
                entity.AddMoveCondition(new AndExpression(new ExpressionMember<bool>(() => _canMove)));
            return entity;
        }

        [Test]
        public void Move_GoesSpeedTimesDeltaTimeAlongTheDirection()
        {
            GameObject body = Track(new GameObject("Body"));
            IEntity entity = Mover(body, 4f, Vector3.zero);

            entity.Move(Vector3.forward, 0.5f);

            Assert.That(Vector3.Distance(body.transform.position, new Vector3(0f, 0f, 2f)), Is.LessThan(TOLERANCE));
        }

        [Test]
        public void Move_NotAllowed_StaysPut()
        {
            GameObject body = Track(new GameObject("Body"));
            IEntity entity = Mover(body, 4f, Vector3.zero);
            _canMove = false;

            entity.Move(Vector3.forward, 0.5f);

            Assert.That(body.transform.position, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Move_WithNoConditionInstalled_Moves()
        {
            GameObject body = Track(new GameObject("Bullet"));
            IEntity entity = Mover(body, 10f, Vector3.zero, withCondition: false);

            entity.Move(Vector3.right, 0.1f);

            Assert.That(Vector3.Distance(body.transform.position, Vector3.right), Is.LessThan(TOLERANCE));
        }

        [Test]
        public void MoveSelf_UsesTheEntitysOwnDirection()
        {
            GameObject body = Track(new GameObject("Body"));
            IEntity entity = Mover(body, 2f, Vector3.left);

            entity.MoveSelf(1f);

            Assert.That(Vector3.Distance(body.transform.position, new Vector3(-2f, 0f, 0f)), Is.LessThan(TOLERANCE));
        }

        private Rigidbody SweptBody(out GameObject body)
        {
            body = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            var rigidbody = body.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            return rigidbody;
        }

        [Test]
        public void MoveSelfSwept_OpenPath_GoesTheFullStep()
        {
            Rigidbody rigidbody = SweptBody(out GameObject body);
            IEntity entity = Mover(body, 10f, Vector3.forward);
            Physics.SyncTransforms();

            entity.MoveSelfSwept(rigidbody, 1f);

            Assert.That(body.transform.position.z, Is.EqualTo(10f).Within(TOLERANCE));
        }

        [Test]
        public void MoveSelfSwept_StepLongerThanTheGap_StopsAtTheWall_InsteadOfJumpingIt()
        {
            Rigidbody rigidbody = SweptBody(out GameObject body);
            GameObject wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position = new Vector3(0f, 0f, 3f);
            wall.transform.localScale = new Vector3(4f, 4f, 0.1f);
            IEntity entity = Mover(body, 10f, Vector3.forward);
            Physics.SyncTransforms();

            entity.MoveSelfSwept(rigidbody, 1f);

            // Body's front face starts at 0.5 and the wall's face is at 2.95: a 2.45 gap, plus the skin.
            Assert.That(body.transform.position.z, Is.EqualTo(2.46f).Within(0.03f));
        }

        [Test]
        public void MoveSelfSwept_NotAllowed_StaysPut()
        {
            Rigidbody rigidbody = SweptBody(out GameObject body);
            IEntity entity = Mover(body, 10f, Vector3.forward);
            _canMove = false;

            entity.MoveSelfSwept(rigidbody, 1f);

            Assert.That(body.transform.position, Is.EqualTo(Vector3.zero));
        }
    }
}
