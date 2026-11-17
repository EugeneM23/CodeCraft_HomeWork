using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class CameraFollowControllerTests
    {
        private sealed class TestPlayerContext : Entity, IPlayerContext
        {
        }

        private static readonly Vector3 Offset = new(0f, 10f, -6f);
        private static readonly Vector3 PointPosition = new(500f, 0f, 500f);
        private static readonly CameraLookSettings LookSettings = new(maxShift: 3f, maxZoom: 0.15f, deadZone: 0.2f);

        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _objects)
                Object.DestroyImmediate(gameObject);

            _objects.Clear();
        }

        private Transform NewTransform(string name, Vector3 position)
        {
            GameObject gameObject = new(name);
            gameObject.transform.position = position;
            _objects.Add(gameObject);
            return gameObject.transform;
        }

        /// <summary>
        /// Follows the character's camera point for one long frame and returns where the root ended up.
        /// A look vector, when given, is installed with the look settings the way CameraSystemInstaller does.
        /// </summary>
        private Vector3 FollowOnce(IEntity character, Vector3? look = null)
        {
            Transform root = NewTransform("CameraRoot", Vector3.zero);

            IPlayerContext context = new TestPlayerContext();
            context.AddCharacter(new ReactiveVariable<IEntity>(character));
            context.AddCameraOffset(new Const<Vector3>(Offset));
            context.AddCameraRoot(root);
            // Fast enough that one frame closes the gap completely: 1 - e^-1000 is 1.
            context.AddCameraSpeed(new Variable<int>(1000));

            if (look.HasValue)
            {
                context.AddCameraLook(new Variable<Vector3>(look.Value));
                context.AddCameraLookSettings(LookSettings);
            }

            CameraFollowController controller = new();
            controller.Init(context);
            controller.LateTick(context, 1f);

            return root.position;
        }

        private IEntity NewTarget()
        {
            IEntity entity = new Entity();
            entity.AddCameraPoint(NewTransform("CameraPoint", PointPosition));
            return entity;
        }

        [Test]
        public void OnFoot_CameraKeepsTheSetOffset()
        {
            Vector3 position = FollowOnce(NewTarget());

            Assert.That(Vector3.Distance(position, PointPosition + Offset), Is.LessThan(1e-3f));
        }

        [Test]
        public void InAVehicle_CameraPullsBackByItsDistance()
        {
            IEntity vehicle = NewTarget();
            vehicle.AddCameraDistance(1.25f);

            Vector3 position = FollowOnce(vehicle);

            Assert.That(Vector3.Distance(position, PointPosition + Offset * 1.25f), Is.LessThan(1e-3f));
        }

        [Test]
        public void OnFoot_LookingAhead_CameraShiftsAndPullsBack()
        {
            Vector3 position = FollowOnce(NewTarget(), Vector3.right);

            Vector3 expected = PointPosition + Offset * 1.15f + Vector3.right * 3f;
            Assert.That(Vector3.Distance(position, expected), Is.LessThan(1e-3f));
        }

        [Test]
        public void InAVehicle_LookAheadScalesWithItsDistance()
        {
            IEntity vehicle = NewTarget();
            vehicle.AddCameraDistance(1.25f);

            Vector3 position = FollowOnce(vehicle, Vector3.right);

            Vector3 expected = PointPosition + Offset * (1.25f * 1.15f) + Vector3.right * (3f * 1.25f);
            Assert.That(Vector3.Distance(position, expected), Is.LessThan(1e-3f));
        }
    }
}
