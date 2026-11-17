using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.AIKit.Adapters.Tests
{
    public sealed class AtomicActuatorTests
    {
        private GameObject _body;
        private Entity _entity;
        private IReactiveVariable<Vector3> _move;
        private IReactiveVariable<Vector3> _rotate;
        private AtomicActuator _actuator;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Body");
            _body.transform.position = new Vector3(1f, 2f, 3f);
            _entity = new Entity("Walker");
            _entity.AddTransform(_body.transform);
            _move = new ReactiveVariable<Vector3>();
            _rotate = new ReactiveVariable<Vector3>();
            _entity.AddMoveDirection(_move);
            _entity.AddRotateDirection(_rotate);
            _entity.AddMoveSpeed(new ReactiveFloat(5f));
            _actuator = new AtomicActuator(_body.transform);
            _actuator.Bind(_entity);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_body);

        [Test]
        public void Move_IsWrittenOnApply_FlatAndAtMostUnit()
        {
            _actuator.Move(new Vector3(3f, 2f, 0f));
            Assert.That(_move.Value, Is.EqualTo(Vector3.zero), "a press waits for Apply");

            _actuator.Apply(0.02f);

            Assert.That(_move.Value.x, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(_move.Value.y, Is.EqualTo(0f));
            Assert.That(_rotate.Value.x, Is.EqualTo(1f).Within(1e-4f), "it turns the way it walks");
        }

        [Test]
        public void Face_TurnsWithoutWalking_AndWinsOverTheWalkingWay()
        {
            _actuator.Move(Vector3.right);
            _actuator.Face(Vector3.forward);
            _actuator.Apply(0.02f);

            Assert.That(_move.Value, Is.EqualTo(Vector3.right));
            Assert.That(_rotate.Value, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void SpeedScale_SlowsTheWalk()
        {
            _actuator.Move(Vector3.forward, 0.5f);
            _actuator.Apply(0.02f);

            Assert.That(_move.Value.z, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void NothingPressed_TheBodyStands()
        {
            _actuator.Move(Vector3.right);
            _actuator.Apply(0.02f);
            _actuator.Apply(0.02f);

            Assert.That(_move.Value, Is.EqualTo(Vector3.zero), "presses are one-shot");
            Assert.That(_rotate.Value, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Stop_ZeroesAtOnce()
        {
            _actuator.Move(Vector3.right);
            _actuator.Apply(0.02f);
            _actuator.Stop();

            Assert.That(_move.Value, Is.EqualTo(Vector3.zero));
            Assert.That(_rotate.Value, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Body_ReportsTheEntity()
        {
            var info = _actuator.Body;

            Assert.That(info.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(info.MaxSpeed, Is.EqualTo(5f));
            Assert.That(info.Radius, Is.EqualTo(AtomicActuator.BODY_RADIUS));
            Assert.That(info.CanJump, Is.False);
            Assert.That(info.IsGrounded, Is.True);
        }

        [Test]
        public void Jumps_DoNothing()
        {
            _actuator.Jump(1f);
            _actuator.Crouch(true);
            _actuator.DropThrough();
            _actuator.Apply(0.02f);

            Assert.That(_body.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(_actuator.HoldTimeForHeight(2f), Is.EqualTo(0f));
        }
    }
}
