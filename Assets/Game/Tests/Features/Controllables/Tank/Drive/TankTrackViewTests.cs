using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class TankTrackViewTests
    {
        private static readonly Vector3 Origin = new(-4600f, 300f, 4600f);

        private const float WHEEL_RADIUS = 0.4f;
        private const float MAX_LENGTH = 1f;
        private const float MIN_LENGTH = 0.2f;

        /// <summary>MAX_LENGTH minus half the travel: where a wheel starts.</summary>
        private const float REST_LENGTH = 0.6f;

        private const float LINK_WIDTH = 0.3f;
        private const float LINK_HEIGHT = 0.06f;

        private GameObject _hull;
        private Rigidbody _body;
        private TankSuspension _suspension;
        private TankWheel _left;
        private TankWheel _right;
        private TankTrackRig _leftRig;
        private IEntity _tank;

        [SetUp]
        public void SetUp()
        {
            _hull = new GameObject("Tank");
            _hull.transform.position = Origin;
            _body = _hull.AddComponent<Rigidbody>();
            _body.useGravity = false;
            Physics.SyncTransforms();

            Transform leftMount = Child("LeftMount", new Vector3(-1f, 0f, 0f));
            Transform rightMount = Child("RightMount", new Vector3(1f, 0f, 0f));
            Transform leftWheel = Child("LeftWheel", Vector3.zero);
            Transform rightWheel = Child("RightWheel", Vector3.zero);

            _suspension = new TankSuspension(new[] { leftMount, rightMount }, new[] { leftWheel, rightWheel },
                WHEEL_RADIUS, MAX_LENGTH, MIN_LENGTH, 0.5f, 0.5f);
            _left = _suspension.Wheels[0];
            _right = _suspension.Wheels[1];

            Transform frontLink = Child("Link0", Vector3.zero);
            frontLink.localScale = new Vector3(LINK_WIDTH, LINK_HEIGHT, 1f);
            Transform rearLink = Child("Link1", Vector3.zero);
            rearLink.localScale = new Vector3(LINK_WIDTH, LINK_HEIGHT, 1f);

            _leftRig = new TankTrackRig
            {
                Front = Child("Idler", new Vector3(-1f, -0.6f, 1f)),
                Rear = Child("Sprocket", new Vector3(-1f, -0.6f, -1f)),
                Links = new[] { frontLink, rearLink }
            };

            _tank = new Entity();
            _tank.AddTransform(_hull.transform);
            _tank.AddRiggedBody(_body);
            _tank.AddSuspension(_suspension);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_hull);

        private Transform Child(string name, Vector3 localPosition)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(_hull.transform, false);
            child.localPosition = localPosition;
            return child;
        }

        private TankTrackView CreateView()
        {
            var view = new TankTrackView(_leftRig, null);
            view.Init(_tank);
            return view;
        }

        private static void AssertNear(Vector3 actual, Vector3 expected, float tolerance = 1e-4f) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(tolerance), $"{actual} vs {expected}");

        [Test]
        public void WheelHangsItsSpringLengthBelowTheMount()
        {
            CreateView().Tick(_tank, 0.02f);

            AssertNear(_left.Visual.position, _left.Mount.position + Vector3.down * REST_LENGTH);
            AssertNear(_right.Visual.position, _right.Mount.position + Vector3.down * REST_LENGTH);
        }

        [Test]
        public void SpringJumps_DrawnWheelEasesAfterIt()
        {
            TankTrackView view = CreateView();
            _left.Length = 0.9f;

            view.Tick(_tank, 0.02f);

            float drawn = REST_LENGTH + (0.9f - REST_LENGTH) * (1f - Mathf.Exp(-30f * 0.02f));
            AssertNear(_left.Visual.position, _left.Mount.position + Vector3.down * drawn);
        }

        [Test]
        public void SpringPastItsTravel_WheelIsDrawnAtTheStop()
        {
            TankTrackView view = CreateView();
            _left.Length = 5f;
            _right.Length = 0f;

            view.Tick(_tank, 10f);

            AssertNear(_left.Visual.position, _left.Mount.position + Vector3.down * MAX_LENGTH);
            AssertNear(_right.Visual.position, _right.Mount.position + Vector3.down * MIN_LENGTH);
        }

        [Test]
        public void DrivingForward_BothSidesRollForward()
        {
            _body.linearVelocity = new Vector3(0f, 0f, 2f);

            CreateView().Tick(_tank, 0.1f);

            float degrees = 2f * 0.1f / WHEEL_RADIUS * Mathf.Rad2Deg;
            Assert.That(Quaternion.Angle(_left.Visual.localRotation, Quaternion.Euler(degrees, 0f, 0f)), Is.LessThan(0.01f));
            Assert.That(Quaternion.Angle(_right.Visual.localRotation, Quaternion.Euler(degrees, 0f, 0f)), Is.LessThan(0.01f));
        }

        [Test]
        public void TurningOnTheSpot_SidesRollAgainstEachOther()
        {
            // Turning right: the left track runs forward, the right one back.
            _body.angularVelocity = new Vector3(0f, 1f, 0f);

            CreateView().Tick(_tank, 0.1f);

            float degrees = 1f * 1f * 0.1f / WHEEL_RADIUS * Mathf.Rad2Deg;
            Assert.That(Quaternion.Angle(_left.Visual.localRotation, Quaternion.Euler(degrees, 0f, 0f)), Is.LessThan(0.01f));
            Assert.That(Quaternion.Angle(_right.Visual.localRotation, Quaternion.Euler(-degrees, 0f, 0f)), Is.LessThan(0.01f));
        }

        [Test]
        public void Links_RunFromIdlerUnderTheWheelToSprocket()
        {
            CreateView().Tick(_tank, 0.02f);

            // The track's lower run passes under the wheel by the wheel's radius less half a link.
            Vector3 underWheel = _left.Visual.position + Vector3.down * (WHEEL_RADIUS - LINK_HEIGHT * 0.5f);
            Vector3 idler = _leftRig.Front.position;
            Vector3 sprocket = _leftRig.Rear.position;

            Transform front = _leftRig.Links[0];
            AssertNear(front.position, (idler + underWheel) * 0.5f);
            AssertNear(front.forward, (underWheel - idler).normalized);
            Assert.That(front.localScale.x, Is.EqualTo(LINK_WIDTH).Within(1e-5f));
            Assert.That(front.localScale.y, Is.EqualTo(LINK_HEIGHT).Within(1e-5f));
            Assert.That(front.localScale.z, Is.EqualTo(Vector3.Distance(idler, underWheel) + 0.04f).Within(1e-4f));

            Transform rear = _leftRig.Links[1];
            AssertNear(rear.position, (underWheel + sprocket) * 0.5f);
            AssertNear(rear.forward, (sprocket - underWheel).normalized);
        }
    }
}
