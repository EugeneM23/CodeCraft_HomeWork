using System.Collections.Generic;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class TankSuspensionBehaviourTests
    {
        // Far from anything a scene open in the editor might hold: the wheels only meet what a
        // test puts under them.
        private static readonly Vector3 Origin = new(-4500f, 300f, 4500f);

        private const float MASS = 400f;
        private const float WHEEL_RADIUS = 0.3f;
        private const float MAX_LENGTH = 1.5f;
        private const float MIN_LENGTH = 0.3f;
        private const float SAG = 0.5f;
        private const float DAMPING_RATIO = 0.5f;
        private const float STEP = 0.02f;

        /// <summary>Floor top this far below the mounts.</summary>
        private const float FLOOR_DROP = 1f;

        private readonly List<GameObject> _objects = new();

        private GameObject _hull;
        private Rigidbody _body;
        private TankSuspension _suspension;
        private IEntity _tank;
        private TankSuspensionBehaviour _behaviour;

        [SetUp]
        public void SetUp()
        {
            _hull = Track(new GameObject("Tank"));
            _hull.transform.position = Origin;
            _body = _hull.AddComponent<Rigidbody>();
            _body.useGravity = false;
            _body.mass = MASS;

            var mounts = new Transform[4];
            int i = 0;
            foreach (float x in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
            {
                Transform mount = new GameObject($"Mount{i}").transform;
                mount.SetParent(_hull.transform, false);
                mount.localPosition = new Vector3(x, 0f, z);
                mounts[i++] = mount;
            }

            _suspension = new TankSuspension(mounts, mounts, WHEEL_RADIUS, MAX_LENGTH, MIN_LENGTH, SAG, DAMPING_RATIO);

            _tank = new Entity();
            _tank.AddRiggedBody(_body);
            _tank.AddSuspension(_suspension);

            _behaviour = new TankSuspensionBehaviour();
            _behaviour.Init(_tank);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _objects)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);

            _objects.Clear();
        }

        private GameObject Track(GameObject gameObject)
        {
            _objects.Add(gameObject);
            return gameObject;
        }

        private Collider AddFloor(int layer = 0)
        {
            GameObject floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.layer = layer;
            floor.transform.position = Origin + Vector3.down * (FLOOR_DROP + 0.5f);
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            return floor.GetComponent<Collider>();
        }

        /// <summary>A small block under the first wheel, its top <paramref name="drop"/> below the mount.</summary>
        private Collider AddBlockUnderFirstWheel(float drop, bool loose)
        {
            GameObject block = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            block.transform.localScale = Vector3.one * 0.4f;
            block.transform.position = _suspension.Wheels[0].Mount.position + Vector3.down * (drop + 0.2f);

            Rigidbody body = block.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = !loose;
            return block.GetComponent<Collider>();
        }

        private void Tick()
        {
            Physics.SyncTransforms();
            _behaviour.FixedTick(_tank, STEP);
        }

        [Test]
        public void Init_SpringsCarryTheWeightAtTheStaticSag()
        {
            float sag = SAG * (MAX_LENGTH - MIN_LENGTH);
            float perWheel = MASS * Mathf.Abs(Physics.gravity.y) / 4f;

            Assert.That(_suspension.Stiffness * sag, Is.EqualTo(perWheel).Within(perWheel * 1e-4f));
            Assert.That(_suspension.Damping,
                Is.EqualTo(2f * DAMPING_RATIO * Mathf.Sqrt(_suspension.Stiffness * MASS / 4f)).Within(1e-2f));
            Assert.That(_body.maxDepenetrationVelocity, Is.EqualTo(1f));
        }

        [Test]
        public void OverAFloor_EveryWheelStandsOnIt()
        {
            Collider floor = AddFloor();

            Tick();

            Assert.That(_suspension.GroundedCount, Is.EqualTo(4));
            Assert.That(_suspension.Grip, Is.EqualTo(1f));
            foreach (TankWheel wheel in _suspension.Wheels)
            {
                Assert.That(wheel.Grounded, Is.True);
                Assert.That(wheel.Ground, Is.SameAs(floor));
                Assert.That(wheel.Length, Is.EqualTo(FLOOR_DROP - WHEEL_RADIUS).Within(1e-3f));
                Assert.That(wheel.Contact.y, Is.EqualTo(Origin.y - FLOOR_DROP).Within(1e-3f));
            }

            Vector3 centre = _suspension.ContactCentre;
            Assert.That(Vector3.Distance(centre, Origin + Vector3.down * FLOOR_DROP), Is.LessThan(1e-3f));
            Assert.That(Vector3.Angle(_suspension.GroundNormal, Vector3.up), Is.LessThan(0.01f));
        }

        [Test]
        public void OverAFloor_SpringsPushTheHullUp()
        {
            AddFloor();

            Tick();

            Vector3 force = _body.GetAccumulatedForce();
            Assert.That(force.y, Is.GreaterThan(0f));
            Assert.That(new Vector2(force.x, force.z).magnitude, Is.LessThan(force.y * 1e-3f));
        }

        [Test]
        public void NothingBelow_WheelsHangFree()
        {
            Tick();

            Assert.That(_suspension.GroundedCount, Is.EqualTo(0));
            Assert.That(_suspension.Grip, Is.EqualTo(0f));
            Assert.That(_suspension.ContactCentre, Is.EqualTo(_body.worldCenterOfMass));
            Assert.That(_suspension.GroundNormal, Is.EqualTo(Vector3.up));
            foreach (TankWheel wheel in _suspension.Wheels)
            {
                Assert.That(wheel.Grounded, Is.False);
                Assert.That(wheel.Ground, Is.Null);
                Assert.That(wheel.Length, Is.EqualTo(MAX_LENGTH));
            }
        }

        [Test]
        public void TakingOff_PreviousLengthRemembersTheLastStep()
        {
            AddFloor();
            Tick();
            float onTheGround = _suspension.Wheels[0].Length;

            _hull.transform.position += Vector3.up * 5f;
            Tick();

            Assert.That(_suspension.Wheels[0].PreviousLength, Is.EqualTo(onTheGround));
            Assert.That(_suspension.Wheels[0].Length, Is.EqualTo(MAX_LENGTH));
        }

        [Test]
        public void LooseRubbleUnderAWheel_IsRolledOver_NotStoodOn()
        {
            Collider floor = AddFloor();
            AddBlockUnderFirstWheel(drop: 0.65f, loose: true);

            Tick();

            Assert.That(_suspension.Wheels[0].Ground, Is.SameAs(floor));
            Assert.That(_suspension.Wheels[0].Length, Is.EqualTo(FLOOR_DROP - WHEEL_RADIUS).Within(1e-3f));
        }

        [Test]
        public void FixedBlockUnderAWheel_IsStoodOn()
        {
            AddFloor();
            Collider block = AddBlockUnderFirstWheel(drop: 0.65f, loose: false);

            Tick();

            Assert.That(_suspension.Wheels[0].Ground, Is.SameAs(block));
            Assert.That(_suspension.Wheels[0].Length, Is.EqualTo(0.65f - WHEEL_RADIUS).Within(1e-3f));
        }

        [Test]
        public void EnemyUnderTheTracks_IsNotGround()
        {
            AddFloor(PhysicsLayers.Enemy);

            Tick();

            Assert.That(_suspension.GroundedCount, Is.EqualTo(0));
        }

        [Test]
        public void LeaningPastTheSoftLimit_IsPulledBackUp()
        {
            // Rolled 60° about Z: the left side is up, so righting turns it back about -Z.
            _hull.transform.rotation = Quaternion.Euler(0f, 0f, 60f);

            Tick();

            Assert.That(_body.GetAccumulatedTorque().z, Is.LessThan(0f));
        }

        [Test]
        public void LeaningWithinTheSoftLimit_IsLeftToTheSprings()
        {
            _hull.transform.rotation = Quaternion.Euler(0f, 0f, 20f);

            Tick();

            Assert.That(_body.GetAccumulatedTorque(), Is.EqualTo(Vector3.zero));
        }
    }
}
